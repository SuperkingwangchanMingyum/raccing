using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Managers;

public class GameManager : NetworkBehaviour
{
    public static event Action<GameManager> OnLobbyDetailsUpdated;
    public static event Action<float> OnTimeWarning;
    public static event Action OnTimeUp;
    public static event Action<int> OnLapIncreased;

    [SerializeField, Layer] private int groundLayer;
    public static int GroundLayer => Instance.groundLayer;
    [SerializeField, Layer] private int kartLayer;
    public static int KartLayer => Instance.kartLayer;

    public new Camera camera;
    private ICameraController cameraController;

    public GameType GameType => ResourceManager.Instance.gameTypes[GameTypeId];

    public static Track CurrentTrack { get; private set; }
    public static bool IsPlaying => CurrentTrack != null;

    public static GameManager Instance { get; private set; }

    public string TrackName => ResourceManager.Instance.tracks[TrackId].trackName;
    public string ModeName => ResourceManager.Instance.gameTypes[GameTypeId].modeName;

    [Networked] public NetworkString<_32> LobbyName { get; set; }
    [Networked] public int TrackId { get; set; }
    [Networked] public int GameTypeId { get; set; }
    [Networked] public int MaxUsers { get; set; }
    
    // 시간 제한 관련
    [Networked] public float RaceStartTime { get; set; }
    [Networked] public bool TimeWarningTriggered { get; set; }
    [Networked] public bool IsTimeUp { get; set; }
    [Networked] public TickTimer TimeUpReturnTimer { get; set; }
    
    // 랩 수 관리 추가
    [Networked] public int CurrentMaxLaps { get; set; }
    [Networked] public bool ExtraLapUsed { get; set; }
    
    private ChangeDetector _changeDetector;
    private bool _isReturningToMenu = false;
    private int _originalLapCount = -1;

    private static void OnLobbyDetailsChangedCallback(GameManager changed)
    {
        OnLobbyDetailsUpdated?.Invoke(changed);
    }

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned()
    {
        base.Spawned();
        
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            LobbyName = ServerInfo.LobbyName;
            TrackId = ServerInfo.TrackId;
            GameTypeId = ServerInfo.GameMode;
            MaxUsers = ServerInfo.MaxUsers;
            
            // 랩 수 초기화
            _originalLapCount = GameType.lapCount;
            CurrentMaxLaps = GameType.lapCount;
            ExtraLapUsed = false;
            
            // 점수 수집 모드라면 ScoreManager 스폰
            if (GameType.IsScoreCollectionMode())
            {
                SpawnScoreManager();
            }
        }
    }
    
    private void SpawnScoreManager()
    {
        // ScoreManager 프리팹을 찾아서 스폰
        var scoreManagerPrefab = Resources.Load<GameObject>("ScoreManager");
        if (scoreManagerPrefab != null)
        {
            Runner.Spawn(scoreManagerPrefab);
            Debug.Log("[GameManager] ScoreManager spawned for score collection mode");
        }
        else
        {
            Debug.LogWarning("[GameManager] ScoreManager prefab not found in Resources folder");
        }
    }
    
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(LobbyName):
                case nameof(TrackId):
                case nameof(GameTypeId):
                case nameof(MaxUsers):
                    OnLobbyDetailsChangedCallback(this);
                    break;
                case nameof(IsTimeUp):
                    if (IsTimeUp)
                        OnTimeUpCallback();
                    break;
                case nameof(CurrentMaxLaps):
                    OnLapIncreased?.Invoke(CurrentMaxLaps);
                    break;
            }
        }
    }
    
    // 현재 최대 랩 수 반환
    public int GetCurrentMaxLaps()
    {
        return CurrentMaxLaps > 0 ? CurrentMaxLaps : GameType.lapCount;
    }
    
    // 랩 수 증가 메서드
    public bool TryIncreaseLapCount(int additionalLaps)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only host can increase lap count!");
            return false;
        }
        
        if (ExtraLapUsed)
        {
            Debug.Log("[GameManager] Extra lap already used this race!");
            RPC_NotifyLapIncreaseFailed();
            return false;
        }
        
        if (GameType.IsPracticeMode() || GameType.IsScoreCollectionMode())
        {
            Debug.Log("[GameManager] Cannot use in Practice Mode or Score Collection Mode!");
            RPC_NotifyLapIncreaseFailed();
            return false;
        }
        
        // 랩 수 증가
        CurrentMaxLaps += additionalLaps;
        ExtraLapUsed = true;
        
        Debug.Log($"[GameManager] Lap count increased by {additionalLaps}! New max: {CurrentMaxLaps}");
        
        // 모든 클라이언트에게 알림
        RPC_NotifyLapIncrease(additionalLaps, CurrentMaxLaps);
        
        return true;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyLapIncrease(int addedLaps, int newMaxLaps)
    {
        Debug.Log($"[GameManager RPC] Lap increased by {addedLaps}, new max: {newMaxLaps}");
        
        // 모든 카트의 UI 업데이트
        UpdateAllKartsUI();
        
        // 이벤트 발생
        OnLapIncreased?.Invoke(newMaxLaps);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyLapIncreaseFailed()
    {
        Debug.Log("[GameManager RPC] Lap increase failed - already used or not applicable mode");
    }
    
    private void UpdateAllKartsUI()
    {
        foreach (var kart in KartEntity.Karts)
        {
            if (kart != null && kart.LapController != null)
            {
                kart.LapController.UpdateLapUI(CurrentMaxLaps);
            }
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        
        // 시간 제한 체크
        if (Object.HasStateAuthority && GameType.ShouldApplyTimeLimit() && IsPlaying)
        {
            float remainingTime = GetRemainingTime();
            
            // 경고 시간 체크
            if (!TimeWarningTriggered && remainingTime <= GameType.warningTimeSeconds && remainingTime > 0)
            {
                TimeWarningTriggered = true;
                RPC_TriggerTimeWarning(remainingTime);
            }
            
            // 시간 초과 체크
            if (!IsTimeUp && remainingTime <= 0)
            {
                IsTimeUp = true;
                TimeUpReturnTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);
                RPC_TriggerTimeUp();
            }
            
            // 메인 씬 복귀 체크
            if (IsTimeUp && TimeUpReturnTimer.Expired(Runner) && !_isReturningToMenu)
            {
                _isReturningToMenu = true;
                RPC_ReturnToMenu();
            }
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerTimeWarning(float remainingTime)
    {
        OnTimeWarning?.Invoke(remainingTime);
        
        if (Object.HasInputAuthority)
        {
            AudioManager.Play("warningTimeSFX", AudioManager.MixerTarget.SFX);
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerTimeUp()
    {
        OnTimeUp?.Invoke();
        
        if (Object.HasInputAuthority)
        {
            AudioManager.Play("timeUpSFX", AudioManager.MixerTarget.SFX);
        }
        
        // 모든 카트를 즉시 죽이기
        foreach (var kart in KartEntity.Karts)
        {
            if (kart && kart.Controller)
            {
                kart.Controller.IsTimeUp = true;
                kart.Controller.AppliedSpeed = 0;
                kart.Controller.Rigidbody.linearVelocity = Vector3.zero;
                kart.SpinOut();
                if (kart.Input)
                {
                    kart.Input.enabled = false;
                }
                
                if (Object.HasStateAuthority && !kart.LapController.HasFinished)
                {
                    kart.LapController.ForceFinish();
                }
            }
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReturnToMenu()
    {
        Debug.Log("Time's up! Returning to main menu...");
        
        // 모든 카트 정리
        foreach (var kart in KartEntity.Karts.ToList())
        {
            if (kart != null)
            {
                // HUD 정리
                if (kart.Object.HasInputAuthority && kart.Hud != null)
                {
                    kart.Hud.HideEndRaceScreen();
                    Destroy(kart.Hud.gameObject);
                }
                
                // 카트 디스폰
                if (Runner != null && Runner.IsRunning && kart.Object != null && kart.Object.IsValid)
                {
                    Runner.Despawn(kart.Object);
                }
            }
        }
        
        // 게임 상태 리셋
        CleanupGameState();
        
        // 지연 후 메뉴로 이동
        StartCoroutine(DelayedReturnToMenu());
    }
    
    private IEnumerator DelayedReturnToMenu()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (LevelManager.Instance != null)
        {
            LevelManager.LoadMenu();
        }
    }
    
    // 게임 상태 정리 메서드
    private void CleanupGameState()
    {
        // 트랙 정보 초기화
        CurrentTrack = null;
        cameraController = null;
        
        // 플래그 초기화
        _isReturningToMenu = false;
        
        // 카메라 리셋
        if (camera != null)
        {
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;
        }
    }
    
    // 남은 시간 계산 (점수 수집 모드 대응)
    public float GetRemainingTime()
    {
        if (!IsPlaying) 
        {
            return GameType.IsScoreCollectionMode() ? 
                GameType.scoreTimeLimit : 
                GameType.timeLimitMinutes * 60f;
        }
        
        if (GameType.IsPracticeMode())
            return float.MaxValue;
        
        if (RaceStartTime == 0) 
        {
            return GameType.IsScoreCollectionMode() ? 
                GameType.scoreTimeLimit : 
                GameType.timeLimitMinutes * 60f;
        }
        
        float elapsedTime = Runner.SimulationTime - RaceStartTime;
        float totalTime = GameType.IsScoreCollectionMode() ? 
            GameType.scoreTimeLimit : 
            GameType.timeLimitMinutes * 60f;
        float remaining = totalTime - elapsedTime;
        
        return Mathf.Max(0, remaining);
    }
    
    // 레이스 시작
    public void StartRace()
    {
        if (Object.HasStateAuthority)
        {
            RaceStartTime = Runner.SimulationTime;
            TimeWarningTriggered = false;
            IsTimeUp = false;
            _isReturningToMenu = false;
            
            // 랩 수 초기화 (새 레이스 시작)
            CurrentMaxLaps = _originalLapCount > 0 ? _originalLapCount : GameType.lapCount;
            ExtraLapUsed = false;
        }
    }
    
    // 시간 초과 콜백
    private void OnTimeUpCallback()
    {
        Debug.Log("Time's up! All karts will be eliminated!");
        
        foreach (var kart in KartEntity.Karts)
        {
            if (kart && kart.Controller)
            {
                if (kart.Object.HasInputAuthority)
                {
                    AudioManager.Play("crashSFX", AudioManager.MixerTarget.SFX, kart.transform.position);
                }
                
                kart.gameObject.SetActive(false);
            }
        }
        
        if (Object.HasInputAuthority)
        {
            var localKart = RoomPlayer.Local?.Kart?.GetComponent<KartEntity>();
            if (localKart && localKart.Hud)
            {
                localKart.Hud.ShowTimeUpMessage();
            }
        }
    }
    
    // 게임 리셋 - 개선된 버전
    public void ResetGame()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RaceStartTime = 0;
            TimeWarningTriggered = false;
            IsTimeUp = false;
            _isReturningToMenu = false;
            
            // 랩 수 리셋
            CurrentMaxLaps = _originalLapCount > 0 ? _originalLapCount : GameType.lapCount;
            ExtraLapUsed = false;
        }
        
        // 게임 상태 정리
        CleanupGameState();
    }
    
    // OnDestroy에서 정리
    private void OnDestroy()
    {
        if (Instance == this)
        {
            CleanupGameState();
            Instance = null;
        }
    }
    
    private void LateUpdate()
    {
        if (cameraController == null) return;
        if (cameraController.Equals(null))
        {
            Debug.LogWarning("Phantom object detected");
            cameraController = null;
            return;
        }

        if (cameraController.ControlCamera(camera) == false)
            cameraController = null;
    }
    
    public static void GetCameraControl(ICameraController controller)
    {
        Instance.cameraController = controller;
    }

    public static bool IsCameraControlled => Instance.cameraController != null;

    public static void SetTrack(Track track)
    {
        CurrentTrack = track;
        
        if (Instance && Instance.Object.HasStateAuthority && track != null)
        {
            Instance.StartRace();
        }
    }
}