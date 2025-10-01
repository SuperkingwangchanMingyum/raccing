using UnityEngine;

public class KartCamera : KartComponent, ICameraController
{
    public ParticleSystem speedLines;

    public Transform rig;
    public Transform camNode;
    public Transform forwardVP;
    public Transform backwardVP;
    public Transform forwardBoostVP;
    public Transform backwardBoostVP;
    public Transform finishVP;
    public float lerpFactorVP = 3f;
    public float lerpFactorFOV = 1.5f;
    public float normalFOV = 60;
    public float boostFOV = 70;
    public float finishFOV = 45;
    public bool useFinishVP;
    
    [Header("Camera Offset Settings")]
    [Tooltip("카메라를 뒤로 이동시킬 거리 (양수값이 뒤로 이동)")]
    public float cameraBackwardOffset = 5.0f;
    [Tooltip("카메라를 위로 이동시킬 거리 - 더 높은 시점을 위해 증가")]
    public float cameraUpwardOffset = 6.0f;
    [Tooltip("카메라가 아래를 내려다보는 각도")]
    public float cameraLookDownAngle = 25f;
    
    [Header("Camera Movement Filter")]
    [Tooltip("이 거리 이상 움직여야 카메라가 따라옴 (미터)")]
    public float movementThreshold = 0.1f;
    [Tooltip("카메라가 따라올 때의 속도")]
    public float followSpeed = 8f;
    [Tooltip("회전 변화 임계값 (도) - 이 각도 이상 회전해야 카메라가 회전")]
    public float rotationThreshold = 2f;
    [Tooltip("높이 변화 임계값 - 이 값 이상 변해야 카메라 높이 조정")]
    public float heightThreshold = 0.05f;

    [Header("Camera Collision Settings")]
    [Tooltip("충돌 감지에 사용할 레이어 마스크")]
    public LayerMask collisionMask = -1;
    [Tooltip("충돌 감지 구체의 반경")]
    public float collisionRadius = 0.3f;
    [Tooltip("충돌 감지 시 추가 오프셋")]
    public float collisionOffset = 0.1f;
    [Tooltip("타겟으로부터 최소 거리")]
    public float minDistanceFromTarget = 1.5f;
    [Tooltip("충돌 시 카메라 이동 부드러움")]
    public float collisionSmooth = 10f;
    [Tooltip("충돌 디버그 표시")]
    public bool debugCollision = false;

    private float _currentFOV = 60;
    private Transform _viewpoint;
    private bool _shouldLerpCamera = true;
    private bool _lastFrameLookBehind;
    private Vector3 _cameraOffset;
    
    // 카메라 필터링을 위한 변수들
    private bool _isInitialized = false;
    private Vector3 _targetKartPosition;
    private Quaternion _targetKartRotation;
    private Vector3 _currentCameraTarget;
    private Quaternion _currentCameraRotation;
    private float _lastSignificantMoveTime;
    
    // 카메라 충돌 처리를 위한 변수
    private Vector3 _lastValidCameraPosition;
    private float _currentDistance;
    
    // 카메라 독립 Transform
    private GameObject _cameraRigObject;
    private Transform _independentCameraRig;

    private void Start()
    {
        _cameraOffset = new Vector3(0, cameraUpwardOffset, -cameraBackwardOffset);
        _isInitialized = false;
        _currentDistance = cameraBackwardOffset;
        
        // 독립적인 카메라 리그 생성 (자식이 아닌 별도 오브젝트로)
        CreateIndependentCameraRig();
        
        // 초기 타겟 설정
        _targetKartPosition = transform.position;
        _targetKartRotation = transform.rotation;
        _currentCameraTarget = transform.position;
        _currentCameraRotation = transform.rotation;
        
        if (collisionMask == 0)
        {
            collisionMask = ~(1 << LayerMask.NameToLayer("Kart") | 
                             1 << LayerMask.NameToLayer("Trigger") | 
                             1 << LayerMask.NameToLayer("UI"));
        }
    }
    
    private void CreateIndependentCameraRig()
    {
        // 독립적인 카메라 리그 오브젝트 생성
        if (_cameraRigObject == null)
        {
            _cameraRigObject = new GameObject($"{gameObject.name}_CameraRig");
            _independentCameraRig = _cameraRigObject.transform;
            
            // 초기 위치와 회전을 카트와 동일하게 설정
            _independentCameraRig.position = transform.position;
            _independentCameraRig.rotation = transform.rotation;
            
            // rig를 독립 리그의 자식으로 설정
            if (rig != null)
            {
                rig.SetParent(_independentCameraRig, true);
            }
        }
    }

    public override void OnLapCompleted(int lap, bool isFinish)
    {
        base.OnLapCompleted(lap, isFinish);
        if (isFinish)
        {
            useFinishVP = true;
        }
    }

    public override void Render()
    {
        base.Render();
        if (Object.HasInputAuthority && _shouldLerpCamera && !GameManager.IsCameraControlled)
        {
            // 독립 리그의 회전만 카트 회전을 따라감
            if (_independentCameraRig != null)
            {
                _independentCameraRig.rotation = transform.rotation;
            }
            GameManager.GetCameraControl(this);
        }
    }

    public bool ControlCamera(Camera cam)
    {
        if (this.Equals(null))
        {
            Debug.LogWarning("Releasing camera from kart");
            return false;
        }

        _viewpoint = GetViewpoint();

        if (!_isInitialized && cam != null)
        {
            _lastValidCameraPosition = cam.transform.position;
            _currentCameraTarget = transform.position;
            _currentCameraRotation = transform.rotation;
            _targetKartPosition = transform.position;
            _targetKartRotation = transform.rotation;
            _isInitialized = true;
        }

        if (_shouldLerpCamera)
            ControlCameraLerp(cam);
        else
            ControlCameraDriving(cam);

        return true;
    }

    private void ControlCameraDriving(Camera cam)
    {
        if (cam == null || _viewpoint == null || _independentCameraRig == null) return;

        _lastFrameLookBehind = Kart.Input.IsLookBehindPressed;

        // 카트의 현재 위치와 회전 (월드 좌표계)
        Vector3 kartCurrentPosition = transform.position;
        Quaternion kartCurrentRotation = transform.rotation;
        
        // 위치 변화량 체크
        float positionDelta = Vector3.Distance(kartCurrentPosition, _targetKartPosition);
        float rotationDelta = Quaternion.Angle(kartCurrentRotation, _targetKartRotation);
        
        // X, Z 축 움직임만 체크
        Vector3 horizontalCurrent = new Vector3(kartCurrentPosition.x, 0, kartCurrentPosition.z);
        Vector3 horizontalTarget = new Vector3(_targetKartPosition.x, 0, _targetKartPosition.z);
        float horizontalDelta = Vector3.Distance(horizontalCurrent, horizontalTarget);
        
        // 높이 변화량 체크
        float heightDelta = Mathf.Abs(kartCurrentPosition.y - _targetKartPosition.y);
        
        // 임계값을 넘는 움직임이 있을 때만 타겟 업데이트
        bool shouldUpdatePosition = horizontalDelta > movementThreshold;
        bool shouldUpdateHeight = heightDelta > heightThreshold;
        bool shouldUpdateRotation = rotationDelta > rotationThreshold;
        
        // 위치 업데이트
        if (shouldUpdatePosition)
        {
            _targetKartPosition.x = kartCurrentPosition.x;
            _targetKartPosition.z = kartCurrentPosition.z;
            _lastSignificantMoveTime = Time.time;
        }
        
        if (shouldUpdateHeight)
        {
            _targetKartPosition.y = kartCurrentPosition.y;
        }
        
        // 회전 업데이트
        if (shouldUpdateRotation)
        {
            _targetKartRotation = kartCurrentRotation;
        }
        
        // 카메라가 따라갈 목표 위치를 부드럽게 전환
        _currentCameraTarget = Vector3.Lerp(_currentCameraTarget, _targetKartPosition, Time.deltaTime * followSpeed);
        _currentCameraRotation = Quaternion.Slerp(_currentCameraRotation, _targetKartRotation, Time.deltaTime * followSpeed);
        
        // 독립 리그의 위치와 회전 업데이트 (자식의 영향을 받지 않음)
        _independentCameraRig.position = _currentCameraTarget;
        _independentCameraRig.rotation = _currentCameraRotation;
        
        // viewpoint의 로컬 회전만 적용
        if (rig != null)
        {
            rig.localEulerAngles = _viewpoint.localEulerAngles;
        }
        
        // 목표 위치 계산 (독립 리그 기준)
        Vector3 worldOffset = _independentCameraRig.TransformDirection(_cameraOffset);
        Vector3 desiredWorldPos = _currentCameraTarget + worldOffset;
        desiredWorldPos.y += 1.5f;
        
        // camNode 위치 업데이트
        if (camNode != null)
        {
            Vector3 localTargetPosition = _viewpoint.localPosition + _cameraOffset;
            localTargetPosition.y += 2.0f;
            
            camNode.localPosition = Vector3.Lerp(
                camNode.localPosition,
                localTargetPosition,
                Time.deltaTime * lerpFactorVP
            );
            
            // camNode의 월드 위치 재계산
            desiredWorldPos = camNode.position;
            desiredWorldPos.y += 1.5f;
        }
        
        // 충돌 검사
        Vector3 kartTargetPoint = _currentCameraTarget + Vector3.up * 1.0f;
        Vector3 resolvedPosition = ResolveCameraCollision(kartTargetPoint, desiredWorldPos, cam);
        
        // 카메라 위치 적용
        cam.transform.position = Vector3.Lerp(cam.transform.position, resolvedPosition, Time.deltaTime * followSpeed);
        
        // 카메라 회전 (필터링된 타겟을 바라봄)
        Vector3 lookDirection = (_currentCameraTarget - cam.transform.position).normalized;
        lookDirection.y = Mathf.Clamp(lookDirection.y, -0.8f, 0.8f);
        
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            targetRotation = targetRotation * Quaternion.AngleAxis(cameraLookDownAngle, Vector3.right);
            cam.transform.rotation = targetRotation;
        }
        
        _lastValidCameraPosition = cam.transform.position;
        SetFOV(cam);
    }

    private void ControlCameraLerp(Camera cam)
    {
        if (cam == null || camNode == null || _independentCameraRig == null) return;

        // 독립 리그 위치와 회전 업데이트
        _independentCameraRig.position = transform.position;
        _independentCameraRig.rotation = transform.rotation;

        Vector3 targetPosition = camNode.position + camNode.TransformDirection(_cameraOffset);
        targetPosition.y += 3.5f;
        
        Vector3 targetPoint = transform.position + Vector3.up * 1.0f;
        Vector3 resolvedPosition = ResolveCameraCollision(targetPoint, targetPosition, cam);
        
        cam.transform.position = Vector3.Lerp(cam.transform.position, resolvedPosition, Time.deltaTime * 2f);
        
        Vector3 kartPosition = transform.position;
        Vector3 lookDirection = (kartPosition - cam.transform.position).normalized;
        
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            targetRotation = targetRotation * Quaternion.AngleAxis(cameraLookDownAngle, Vector3.right);
            
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRotation, Time.deltaTime * 4f);
            
            if (Vector3.Distance(cam.transform.position, resolvedPosition) < 0.1f &&
                Quaternion.Angle(cam.transform.rotation, targetRotation) < 5f)
            {
                _shouldLerpCamera = false;
                _currentCameraTarget = transform.position;
                _currentCameraRotation = transform.rotation;
                _targetKartPosition = transform.position;
                _targetKartRotation = transform.rotation;
            }
        }
        
        _lastValidCameraPosition = cam.transform.position;
    }

    private Vector3 ResolveCameraCollision(Vector3 targetPoint, Vector3 desiredCameraPosition, Camera cam)
    {
        Vector3 direction = desiredCameraPosition - targetPoint;
        float desiredDistance = direction.magnitude;
        
        if (desiredDistance < minDistanceFromTarget)
        {
            desiredDistance = minDistanceFromTarget;
        }
        
        direction.Normalize();
        
        RaycastHit hit;
        float actualDistance = desiredDistance;
        
        if (Physics.SphereCast(
            targetPoint,
            collisionRadius,
            direction,
            out hit,
            desiredDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            actualDistance = Mathf.Max(hit.distance - collisionOffset, minDistanceFromTarget);
            
            if (debugCollision)
            {
                Debug.DrawLine(targetPoint, hit.point, Color.red, 0.1f);
                Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.yellow, 0.1f);
            }
        }
        else
        {
            if (debugCollision)
            {
                Debug.DrawLine(targetPoint, desiredCameraPosition, Color.green, 0.1f);
            }
        }
        
        _currentDistance = Mathf.Lerp(_currentDistance, actualDistance, Time.deltaTime * collisionSmooth);
        
        Vector3 finalPosition = targetPoint + direction * _currentDistance;
        
        float terrainHeight = GetTerrainHeight(finalPosition);
        if (finalPosition.y < terrainHeight + collisionRadius)
        {
            finalPosition.y = terrainHeight + collisionRadius;
        }
        
        return finalPosition;
    }

    private float GetTerrainHeight(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, 20f, collisionMask))
        {
            return hit.point.y;
        }
        return position.y;
    }

    private void SetFOV(Camera cam)
    {
        if (cam == null) return;

        float adjustedNormalFOV = normalFOV + 20f;
        float adjustedBoostFOV = boostFOV + 15f;
        float adjustedFinishFOV = finishFOV + 12f;
        
        _currentFOV = useFinishVP ? adjustedFinishFOV : 
                      Kart.Controller.BoostTime > 0 ? adjustedBoostFOV : adjustedNormalFOV;
        
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, _currentFOV, Time.deltaTime * lerpFactorFOV);
    }

    private Transform GetViewpoint()
    {
        if (Kart == null || Kart.Controller == null) return null;
        if (useFinishVP) return finishVP;

        if (Kart.Input != null && Kart.Input.IsLookBehindPressed)
        {
            return Kart.Controller.BoostTime > 0 ? backwardBoostVP : backwardVP;
        }

        return Kart.Controller.BoostTime > 0 ? forwardBoostVP : forwardVP;
    }
    
    public void SetCameraOffset(float backward, float upward, float lookDown)
    {
        cameraBackwardOffset = backward;
        cameraUpwardOffset = upward;
        cameraLookDownAngle = lookDown;
        _cameraOffset = new Vector3(0, cameraUpwardOffset, -cameraBackwardOffset);
    }
    
    public void ResetCamera()
    {
        if (Camera.main != null && _independentCameraRig != null)
        {
            _targetKartPosition = transform.position;
            _targetKartRotation = transform.rotation;
            _currentCameraTarget = transform.position;
            _currentCameraRotation = transform.rotation;
            _independentCameraRig.position = transform.position;
            _independentCameraRig.rotation = transform.rotation;
            _isInitialized = false;
            _currentDistance = cameraBackwardOffset;
        }
    }
    
    private void OnValidate()
    {
        _cameraOffset = new Vector3(0, cameraUpwardOffset, -cameraBackwardOffset);
        
        collisionRadius = Mathf.Max(0.1f, collisionRadius);
        collisionOffset = Mathf.Max(0f, collisionOffset);
        minDistanceFromTarget = Mathf.Max(0.5f, minDistanceFromTarget);
        collisionSmooth = Mathf.Max(1f, collisionSmooth);
        movementThreshold = Mathf.Max(0.01f, movementThreshold);
        rotationThreshold = Mathf.Max(0.1f, rotationThreshold);
        heightThreshold = Mathf.Max(0.01f, heightThreshold);
    }
    
    private void OnDisable()
    {
        _isInitialized = false;
        _shouldLerpCamera = true;
    }
    
    private void OnDestroy()
    {
        // 독립 카메라 리그 정리
        if (_cameraRigObject != null)
        {
            Destroy(_cameraRigObject);
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!debugCollision || !Application.isPlaying) return;
        
        if (Camera.main != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Camera.main.transform.position, collisionRadius);
        }
        
        if (transform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.0f, 0.2f);
            
            if (_isInitialized)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_currentCameraTarget + Vector3.up * 1.0f, 0.15f);
            }
        }
    }
}