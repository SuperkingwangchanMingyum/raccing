using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public class KartInput : KartComponent, INetworkRunnerCallbacks
{
    public struct NetworkInputData : INetworkInput
    {
        public const uint ButtonAccelerate = 1 << 0;
        public const uint ButtonReverse = 1 << 1;
        public const uint ButtonDrift = 1 << 2;
        public const uint ButtonLookbehind = 1 << 3;
        public const uint UseItem = 1 << 4;
        public const uint UseSecondaryItem = 1 << 5;
        public const uint UseBoosterItem = 1 << 6;

        public uint Buttons;
        public uint OneShots;

        private int _steer;
        public float Steer
        {
            get => _steer * .001f;
            set => _steer = (int)(value * 1000);
        }

        public bool IsUp(uint button) => IsDown(button) == false;
        public bool IsDown(uint button) => (Buttons & button) == button;
        public bool IsDownThisFrame(uint button) => (OneShots & button) == button;
        
        public bool IsAccelerate => IsDown(ButtonAccelerate);
        public bool IsReverse => IsDown(ButtonReverse);
        public bool IsDriftPressed => IsDown(ButtonDrift);
        public bool IsDriftPressedThisFrame => IsDownThisFrame(ButtonDrift);
    }

    public Gamepad gamepad;

    [Header("Input Actions")]
    [SerializeField] private InputAction accelerate;
    [SerializeField] private InputAction reverse;
    [SerializeField] private InputAction drift;
    [SerializeField] private InputAction steer;
    [SerializeField] private InputAction lookBehind;
    [SerializeField] private InputAction useItem;
    [SerializeField] private InputAction useSecondaryItem;
    [SerializeField] private InputAction useBoosterItem;
    [SerializeField] private InputAction pause;

    [Header("Mobile Settings")]
    [SerializeField] private bool enableMobileSupport = true;
    [SerializeField] private bool debugMobileInput = false;

    private bool _useItemPressed;
    private bool _useSecondaryItemPressed;
    private bool _useBoosterItemPressed;
    private bool _driftPressed;
    
    // 모바일 드리프트 상태 추적
    private bool _wasMobileDriftingLastFrame = false;

    public override void Spawned()
    {
        base.Spawned();
        Runner.AddCallbacks(this);
        InitializeInputActions();
    }

    private void InitializeInputActions()
    {
        accelerate = accelerate.Clone();
        reverse = reverse.Clone();
        drift = drift.Clone();
        steer = steer.Clone();
        lookBehind = lookBehind.Clone();
        useItem = useItem.Clone();
        useSecondaryItem = useSecondaryItem.Clone();
        useBoosterItem = useBoosterItem.Clone();
        pause = pause.Clone();

        accelerate.Enable();
        reverse.Enable();
        drift.Enable();
        steer.Enable();
        lookBehind.Enable();
        useItem.Enable();
        useSecondaryItem.Enable();
        useBoosterItem.Enable();
        pause.Enable();
        
        useItem.started += UseItemPressed;
        useSecondaryItem.started += UseSecondaryItemPressed;
        useBoosterItem.started += UseBoosterItemPressed;
        drift.started += DriftPressed;
        pause.started += PausePressed;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        DisposeInputs();
        Runner.RemoveCallbacks(this);
    }

    private void OnDestroy()
    {
        DisposeInputs();
    }

    private void DisposeInputs()
    {
        accelerate?.Dispose();
        reverse?.Dispose();
        drift?.Dispose();
        steer?.Dispose();
        lookBehind?.Dispose();
        useItem?.Dispose();
        useSecondaryItem?.Dispose();
        useBoosterItem?.Dispose();
        pause?.Dispose();
    }

    private void UseItemPressed(InputAction.CallbackContext ctx) => _useItemPressed = true;
    private void UseSecondaryItemPressed(InputAction.CallbackContext ctx) => _useSecondaryItemPressed = true;
    private void UseBoosterItemPressed(InputAction.CallbackContext ctx) => _useBoosterItemPressed = true;
    private void DriftPressed(InputAction.CallbackContext ctx) => _driftPressed = true;

    private void PausePressed(InputAction.CallbackContext ctx)
    {
        if (Kart.Controller.CanDrive) InterfaceManager.Instance.OpenPauseMenu();
    }

    public bool IsLookBehindPressed 
    {
        get
        {
            // PC 뒤보기
            if (ReadBool(lookBehind)) return true;
            
            // 모바일 뒤보기
            if (enableMobileSupport && MobileControlsManager.Instance != null && 
                MobileControlsManager.Instance.IsMobileControlActive())
            {
                return MobileControlsManager.Instance.LookBehindInput;
            }
            
            return false;
        }
    }

    private static bool ReadBool(InputAction action) => action?.ReadValue<float>() != 0;
    private static float ReadFloat(InputAction action) => action?.ReadValue<float>() ?? 0f;

    public void OnInput(NetworkRunner runner, NetworkInput input) 
    {
        gamepad = Gamepad.current;
        var userInput = new NetworkInputData();
        
        // ====== PC 입력 처리 ======
        bool pcAccelerate = ReadBool(accelerate);
        bool pcReverse = ReadBool(reverse);
        bool pcDrift = ReadBool(drift);
        bool pcLookBehind = ReadBool(lookBehind);
        float pcSteer = ReadFloat(steer);
        
        if (pcAccelerate) userInput.Buttons |= NetworkInputData.ButtonAccelerate;
        if (pcReverse) userInput.Buttons |= NetworkInputData.ButtonReverse;
        if (pcDrift) userInput.Buttons |= NetworkInputData.ButtonDrift;
        if (pcLookBehind) userInput.Buttons |= NetworkInputData.ButtonLookbehind;
        
        // PC OneShots
        if (_driftPressed) userInput.OneShots |= NetworkInputData.ButtonDrift;
        if (_useItemPressed) userInput.OneShots |= NetworkInputData.UseItem;
        if (_useSecondaryItemPressed) userInput.OneShots |= NetworkInputData.UseSecondaryItem;
        if (_useBoosterItemPressed) userInput.OneShots |= NetworkInputData.UseBoosterItem;
        
        userInput.Steer = pcSteer;
        
        // ====== 모바일 입력 추가 처리 ======
        if (enableMobileSupport)
        {
            var mobile = MobileControlsManager.Instance;
            if (mobile != null && mobile.IsMobileControlActive())
            {
                // 모바일 가속/브레이크
                bool mobileAccel = mobile.AccelerateInput > 0;
                bool mobileBrake = mobile.BrakeInput > 0;
                bool mobileDrift = mobile.DriftInput;
                bool mobileLookBehind = mobile.LookBehindInput;
                float mobileSteer = mobile.SteerInput;
                
                // 버튼 OR 처리 (PC 또는 모바일)
                if (mobileAccel) userInput.Buttons |= NetworkInputData.ButtonAccelerate;
                if (mobileBrake) userInput.Buttons |= NetworkInputData.ButtonReverse;
                if (mobileLookBehind) userInput.Buttons |= NetworkInputData.ButtonLookbehind;
                
                // 모바일 드리프트 처리
                if (mobileDrift) 
                {
                    userInput.Buttons |= NetworkInputData.ButtonDrift;
                    
                    // 이번 프레임에 처음 누른 경우
                    if (!_wasMobileDriftingLastFrame)
                    {
                        userInput.OneShots |= NetworkInputData.ButtonDrift;
                        if (debugMobileInput)
                            Debug.Log($"[Mobile Drift] Started! Steer: {mobileSteer:F2}");
                    }
                }
                else if (_wasMobileDriftingLastFrame && debugMobileInput)
                {
                    Debug.Log("[Mobile Drift] Ended");
                }
                
                _wasMobileDriftingLastFrame = mobileDrift;
                
                // 모바일 아이템
                if (mobile.UseItemInput)
                {
                    userInput.OneShots |= NetworkInputData.UseItem;
                }
                
                // 스티어링 - 더 큰 값 사용
                if (Mathf.Abs(mobileSteer) > Mathf.Abs(userInput.Steer))
                {
                    userInput.Steer = mobileSteer;
                }
                
                // 디버그 출력
                if (debugMobileInput && Time.frameCount % 30 == 0 && (mobileDrift || Mathf.Abs(mobileSteer) > 0.1f))
                {
                    Debug.Log($"[Mobile Input] Drift: {mobileDrift}, Steer: {mobileSteer:F2}, " +
                             $"Accel: {mobileAccel}, Brake: {mobileBrake}");
                    Debug.Log($"[Final Input] Buttons: {userInput.Buttons}, OneShots: {userInput.OneShots}, " +
                             $"Steer: {userInput.Steer:F2}");
                }
            }
            else
            {
                _wasMobileDriftingLastFrame = false;
            }
        }
        
        input.Set(userInput);
        
        // PC OneShot 플래그 리셋
        _driftPressed = false;
        _useItemPressed = false;
        _useSecondaryItemPressed = false;
        _useBoosterItemPressed = false;
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}