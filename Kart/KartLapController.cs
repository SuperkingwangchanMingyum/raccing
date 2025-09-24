using System;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;

public class KartLapController : KartComponent
{
    public static event Action<KartLapController> OnRaceCompleted;

    [Networked] public int Lap { get; set; } = 1;

    [Networked, Capacity(5)]
    public NetworkArray<int> LapTicks { get; }

    [Networked] public int StartRaceTick { get; set; }

    [Networked] public int EndRaceTick { get; set; }

    [Networked] private int CheckpointIndex { get; set; } = -1;

    public event Action<int, int> OnLapChanged;
    public bool HasFinished => EndRaceTick != 0;

    private KartController Controller => Kart.Controller;
    private GameUI Hud => Kart.Hud;

    private NetworkRigidbody3D _nrb;
    private ChangeDetector _changeDetector;

    private void Awake()
    {
        _nrb = GetComponent<NetworkRigidbody3D>();
    }

    public override void Spawned()
    {
        base.Spawned();

        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // lap control is not needed if the gametype does not use laps
        if (GameManager.Instance != null && GameManager.Instance.GameType.IsPracticeMode())
        {
            enabled = false;
        }
        else
        {
            Lap = 1;
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Lap):
                    OnLapChangedCallback(this);
                    break;
                case nameof(CheckpointIndex):
                    CheckpointIndexChanged(this);
                    break;
            }
        }
    }

    public override void OnRaceStart()
    {
        base.OnRaceStart();
        StartRaceTick = Runner.Tick;
    }

    public override void OnLapCompleted(int lap, bool isFinish)
    {
        base.OnLapCompleted(lap, isFinish);

        if (isFinish)
        {
            if (Object.HasInputAuthority)
            {
                // finished race
                AudioManager.Play("raceFinishedSFX", AudioManager.MixerTarget.SFX);
                if (Hud != null)
                {
                    Hud.ShowEndRaceScreen();
                }
            }

            if (Kart.Controller != null && Kart.Controller.RoomUser != null)
            {
                Kart.Controller.RoomUser.HasFinished = true;
            }
            EndRaceTick = Runner.Tick;
        }
        else
        {
            if (Object.HasInputAuthority)
            {
                AudioManager.Play("newLapSFX", AudioManager.MixerTarget.SFX);
            }
        }

        OnRaceCompleted?.Invoke(this);
    }

    public void ResetToCheckpoint()
    {
        if (GameManager.CurrentTrack == null)
        {
            Debug.LogWarning("[KartLapController] CurrentTrack is null in ResetToCheckpoint");
            return;
        }

        var tgt = CheckpointIndex == -1
            ? GameManager.CurrentTrack.finishLine.transform
            : GameManager.CurrentTrack.checkpoints[CheckpointIndex].transform;

        if (_nrb != null && tgt != null)
        {
            _nrb.Teleport(tgt.position, tgt.rotation);
        }

        // Reset Kart, stop moving/drifting/boosting and clear item! / play SFX  
        if (Controller != null)
        {
            Controller.ResetControllerState();
        }
    }

    // Public 메서드 추가 - UI 업데이트를 위한 메서드
    public void UpdateLapUI(int newMaxLaps)
    {
        // 현재 랩과 새로운 최대 랩으로 UI 업데이트
        try
        {
            OnLapChanged?.Invoke(Lap, newMaxLaps);
            
            // Hud가 있으면 직접 업데이트
            if (Hud != null && Hud.lapCount != null)
            {
                Hud.lapCount.text = $"{Lap}/{newMaxLaps}";
            }
            
            Debug.Log($"[KartLapController] UI Updated - Lap: {Lap}/{newMaxLaps}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[KartLapController] Error updating UI: {e.Message}");
        }
    }

    // Public 메서드 추가 - 외부에서 랩 변경 이벤트 트리거
    public void TriggerLapChangedEvent()
    {
        if (GameManager.Instance != null)
        {
            int maxLaps = GameManager.Instance.GetCurrentMaxLaps();
            OnLapChanged?.Invoke(Lap, maxLaps);
        }
    }

    private static void OnLapChangedCallback(KartLapController changed)
    {
        // Null 체크
        if (changed == null)
        {
            Debug.LogWarning("[KartLapController] OnLapChangedCallback: changed is null");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[KartLapController] OnLapChangedCallback: GameManager.Instance is null");
            return;
        }

        if (GameManager.Instance.GameType == null)
        {
            Debug.LogWarning("[KartLapController] OnLapChangedCallback: GameType is null");
            return;
        }

        // GameManager의 CurrentMaxLaps를 안전하게 가져오기
        int maxLaps = 0;
        try
        {
            maxLaps = GameManager.Instance.GetCurrentMaxLaps();
            Debug.Log($"[KartLapController] Current max laps: {maxLaps}, Current lap: {changed.Lap}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[KartLapController] Failed to get max laps: {e.Message}");
            // 폴백: GameType의 기본값 사용
            maxLaps = GameManager.Instance.GameType.lapCount;
        }

        var isPracticeMode = GameManager.Instance.GameType.IsPracticeMode();
        var isFinish = !isPracticeMode && changed.Lap - 1 == maxLaps;

        // KartComponent들에게 이벤트 전달
        var behaviours = changed.GetComponentsInChildren<KartComponent>();
        if (behaviours != null)
        {
            foreach (var b in behaviours)
            {
                if (b != null)
                {
                    try
                    {
                        b.OnLapCompleted(changed.Lap, isFinish);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[KartLapController] Error in OnLapCompleted for {b.GetType().Name}: {e.Message}");
                    }
                }
            }
        }

        // OnLapChanged 이벤트 호출
        try
        {
            changed.OnLapChanged?.Invoke(changed.Lap, maxLaps);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[KartLapController] Error invoking OnLapChanged: {e.Message}");
        }
    }

    private static void CheckpointIndexChanged(KartLapController changed)
    {
        if (changed == null || changed.Object == null)
        {
            Debug.LogWarning("[KartLapController] CheckpointIndexChanged: changed or Object is null");
            return;
        }

        var nObject = changed.Object;

        if (!nObject.HasInputAuthority) return;

        // -1 means checkpoint is the finish line itself
        if (changed.CheckpointIndex != -1)
        {
            AudioManager.Play("errorSFX", AudioManager.MixerTarget.SFX);
        }
    }

    public void ProcessCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("[KartLapController] ProcessCheckpoint: checkpoint is null");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[KartLapController] ProcessCheckpoint: GameManager.Instance is null");
            return;
        }

        // if Game type is practice
        if (GameManager.Instance.GameType.IsPracticeMode())
        {
            CheckpointIndex = checkpoint.index;
            return;
        }

        // if current checkpoint is the one directly after the previous checkpoints
        if (CheckpointIndex == checkpoint.index - 1)
        {
            CheckpointIndex++;
        }
    }

    public void ProcessFinishLine(FinishLine finishLine)
    {
        if (finishLine == null)
        {
            Debug.LogWarning("[KartLapController] ProcessFinishLine: finishLine is null");
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.GameType == null)
        {
            Debug.LogWarning("[KartLapController] ProcessFinishLine: GameManager or GameType is null");
            return;
        }

        if (GameManager.CurrentTrack == null)
        {
            Debug.LogWarning("[KartLapController] ProcessFinishLine: CurrentTrack is null");
            return;
        }

        var gameType = GameManager.Instance.GameType;
        var checkpoints = GameManager.CurrentTrack.checkpoints;

        if (gameType.IsPracticeMode())
        {
            CheckpointIndex = -1;
            return;
        }

        // GameManager의 CurrentMaxLaps를 안전하게 가져오기
        int maxLaps = 0;
        try
        {
            maxLaps = GameManager.Instance.GetCurrentMaxLaps();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[KartLapController] ProcessFinishLine - Failed to get max laps: {e.Message}");
            maxLaps = gameType.lapCount;
        }

        // If we are on the last checkpoint, proceed to 'complete' a lap. (Or if we are in debug)
        if (CheckpointIndex == checkpoints.Length - 1 || finishLine.debug)
        {
            // If we have just started the race we dont want to complete a lap. This is a small workaround.
            if (Lap == 0) return;

            // 아직 최대 랩에 도달하지 않았으면 계속 진행
            if (Lap <= maxLaps)
            {
                // Add our current tick to the LapTicks networked property so we can keep track of race times.
                if (Lap - 1 < LapTicks.Length && Lap - 1 >= 0)
                {
                    LapTicks.Set(Lap - 1, Runner.Tick);
                }

                // Increment the lap and reset the checkpoint index to -1
                Lap++;
                CheckpointIndex = -1;

                Debug.Log($"[KartLapController] Lap completed: {Lap - 1}/{maxLaps}");
            }
        }
    }

    /// <summary>
    /// 시간 초과 시 강제 종료
    /// </summary>
    public void ForceFinish()
    {
        if (!HasFinished && Object != null && Object.HasStateAuthority)
        {
            EndRaceTick = Runner.Tick;

            // 현재까지의 랩 기록
            if (Lap > 0 && Lap <= LapTicks.Length)
            {
                for (int i = Lap - 1; i < LapTicks.Length; i++)
                {
                    if (i >= 0 && i < LapTicks.Length && LapTicks.Get(i) == 0)
                    {
                        LapTicks.Set(i, Runner.Tick);
                    }
                }
            }

            // DNF(Did Not Finish) 상태로 설정
            if (Kart != null && Kart.Controller != null && Kart.Controller.RoomUser != null)
            {
                Kart.Controller.RoomUser.HasFinished = true;
                Debug.Log($"[KartLapController] Force finished - Player: {Kart.Controller.RoomUser.Username}, Lap: {Lap}, Time: {GetTotalRaceTime()}s");
            }

            // 종료 이벤트 발생
            OnRaceCompleted?.Invoke(this);
        }
    }

    /// <summary>
    /// Returns the total time we have been racing for, in seconds.
    /// </summary>
    public float GetTotalRaceTime()
    {
        if (Runner == null || !Runner.IsRunning || StartRaceTick == 0)
            return 0f;

        // 시간 초과 시 최대 시간 반환
        if (GameManager.Instance != null && GameManager.Instance.IsTimeUp && GameManager.Instance.GameType != null)
        {
            return GameManager.Instance.GameType.timeLimitMinutes * 60f;
        }

        var endTick = EndRaceTick == 0 ? Runner.Tick.Raw : EndRaceTick;
        return TickHelper.TickToSeconds(Runner, endTick - StartRaceTick);
    }

    /// <summary>
    /// Get lap time for specific lap
    /// </summary>
    public float GetLapTime(int lapIndex)
    {
        if (lapIndex < 0 || lapIndex >= LapTicks.Length || Runner == null)
            return 0f;

        var lapTick = LapTicks.Get(lapIndex);
        if (lapTick == 0)
            return 0f;

        var previousTick = lapIndex == 0 ? StartRaceTick : LapTicks.Get(lapIndex - 1);
        return TickHelper.TickToSeconds(Runner, lapTick - previousTick);
    }

    /// <summary>
    /// Get current lap progress as percentage
    /// </summary>
    public float GetLapProgress()
    {
        if (!GameManager.CurrentTrack || GameManager.CurrentTrack.checkpoints == null)
            return 0f;

        int totalCheckpoints = GameManager.CurrentTrack.checkpoints.Length;
        if (totalCheckpoints == 0)
            return 0f;

        float currentProgress = CheckpointIndex + 1;
        return currentProgress / (totalCheckpoints + 1); // +1 for finish line
    }

    /// <summary>
    /// Get race completion percentage (GameManager의 CurrentMaxLaps 사용)
    /// </summary>
    public float GetRaceCompletionPercentage()
    {
        if (GameManager.Instance == null || GameManager.Instance.GameType == null)
            return 0f;

        var gameType = GameManager.Instance.GameType;
        if (gameType.IsPracticeMode())
            return 0f;

        int totalLaps = 0;
        try
        {
            totalLaps = GameManager.Instance.GetCurrentMaxLaps();
        }
        catch
        {
            totalLaps = gameType.lapCount;
        }

        if (totalLaps == 0)
            return 0f;

        float completedLaps = Mathf.Max(0, Lap - 1);
        float currentLapProgress = GetLapProgress();

        return ((completedLaps + currentLapProgress) / totalLaps) * 100f;
    }

    /// <summary>
    /// Check if player is in last lap (GameManager의 CurrentMaxLaps 사용)
    /// </summary>
    public bool IsLastLap()
    {
        if (GameManager.Instance == null || GameManager.Instance.GameType == null)
            return false;

        var gameType = GameManager.Instance.GameType;
        if (gameType.IsPracticeMode())
            return false;

        int maxLaps = 0;
        try
        {
            maxLaps = GameManager.Instance.GetCurrentMaxLaps();
        }
        catch
        {
            maxLaps = gameType.lapCount;
        }

        return Lap == maxLaps;
    }

    /// <summary>
    /// Reset lap controller for new race
    /// </summary>
    public void ResetForNewRace()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            Lap = 1;
            StartRaceTick = 0;
            EndRaceTick = 0;
            CheckpointIndex = -1;

            // Clear lap times
            for (int i = 0; i < LapTicks.Length; i++)
            {
                LapTicks.Set(i, 0);
            }
        }
    }

    private void OnDestroy()
    {
        // 이벤트 정리
        OnLapChanged = null;
        OnRaceCompleted = null;
    }
}