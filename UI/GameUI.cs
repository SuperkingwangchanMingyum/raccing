using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using Random = UnityEngine.Random;

public class GameUI : MonoBehaviour
{
    public interface IGameUIComponent
    {
        void Init(KartEntity entity);
    }

    public CanvasGroup fader;
    public Animator introAnimator;
    public Animator countdownAnimator;
    
    [Header("아이템 슬롯 2 (E키/터치) - 첫 번째 활성 슬롯")]
    public GameObject secondaryPickupContainer;
    public Image secondaryPickupDisplay;
    public Button secondaryPickupButton;
    public Animator secondaryItemAnimator;
    
    [Header("아이템 슬롯 3 (R키/터치)")]
    public GameObject tertiaryPickupContainer;
    public Image tertiaryPickupDisplay;
    public Button tertiaryPickupButton;
    public Animator tertiaryItemAnimator;
    
    [Header("아이템 슬롯 4 (T키/터치)")]
    public GameObject quaternaryPickupContainer;
    public Image quaternaryPickupDisplay;
    public Button quaternaryPickupButton;
    public Animator quaternaryItemAnimator;
    
    [Header("빈 슬롯 아이콘")]
    public Sprite emptySlotIcon;
    
    [Header("Time Limit UI - Auto Generated")]
    private GameObject timeLimitContainer;
    private Text timeLimitText;
    private Image timeLimitBar;
    private Image timeLimitBarBg;
    private GameObject timeWarningPanel;
    private Text timeWarningText;
    private Animator timeWarningAnimator;
    private CanvasGroup timeLimitCanvasGroup;
    
    // TIME UP 메시지 UI
    private GameObject timeUpMessagePanel;
    private Text timeUpMessageText;
    private GameObject returnToMenuText;
    
    // UI 색상 설정
    private Color normalTimeColor = new Color(0.2f, 1f, 0.2f, 1f);
    private Color warningTimeColor = new Color(1f, 0.92f, 0.016f, 1f);
    private Color criticalTimeColor = new Color(1f, 0.2f, 0.2f, 1f);
    
    [Header("기타 UI")]
    public GameObject timesContainer;
    public GameObject coinCountContainer;
    public GameObject lapCountContainer;        // 랩/점수 공용 컨테이너
    public EndRaceUI endRaceScreen;
    public Image boostBar;
    public Text coinCount;
    public Text lapCount;                       // 랩/점수 공용 텍스트
    public Text raceTimeText;
    public Text[] lapTimeTexts;
    public Text introGameModeText;
    public Text introTrackNameText;
    public Button continueEndButton;
    
    private bool _startedCountdown;
    private bool _isTimeWarningShown = false;
    private Coroutine _timeWarningCoroutine;
    private Coroutine _timeUpCoroutine;
    
    // 이전 아이템 인덱스 추적용
    private int previousSecondaryIndex = -1;
    private int previousTertiaryIndex = -1;
    private int previousQuaternaryIndex = -1;
    
    // 아이템 스핀 상태 추적
    private bool isSecondarySpinning = false;
    private bool isTertiarySpinning = false;
    private bool isQuaternarySpinning = false;
    
    // 점수 추적용
    private int currentScore = 0;

    public KartEntity Kart { get; private set; }
    private KartController KartController => Kart.Controller;

    // 슬롯 1 호환성 메서드들 (더미)
    public void SetPickupDisplay(Powerup item) { }
    public void ClearPickupDisplay() { }
    public void StartSpinItem() { }

    public void Init(KartEntity kart)
    {
        Kart = kart;

        var uis = GetComponentsInChildren<IGameUIComponent>(true);
        foreach (var ui in uis) ui.Init(kart);

        kart.LapController.OnLapChanged += SetLapCount;

        var track = Track.Current;

        if (track == null)
            Debug.LogWarning($"You need to initialize the GameUI on a track for track-specific values to be updated!");
        else
        {
            introGameModeText.text = GameManager.Instance.GameType.modeName;
            introTrackNameText.text = track.definition.trackName;
        }

        GameType gameType = GameManager.Instance.GameType;

        // 게임 모드에 따른 UI 설정
        SetupGameModeUI(gameType);

        // 시간 제한 UI 자동 생성
        if (!gameType.IsPracticeMode())
        {
            Debug.Log("[GameUI] Creating Time Limit UI - Time Limit Enabled");
            CreateTimeLimitUI();
            CreateTimeUpUI();
            
            if (timeLimitContainer != null)
            {
                timeLimitContainer.SetActive(true);
                GameManager.OnTimeWarning += ShowTimeWarning;
                GameManager.OnTimeUp += OnTimeUp;
                Debug.Log($"[GameUI] Time Limit UI Created - {gameType.GetEffectiveTimeLimit()} minutes");
            }
        }

        // 아이템 및 코인 설정
        if (gameType.hasPickups == false)
        {
            if (secondaryPickupContainer) secondaryPickupContainer.SetActive(false);
            if (tertiaryPickupContainer) tertiaryPickupContainer.SetActive(false);
            if (quaternaryPickupContainer) quaternaryPickupContainer.SetActive(false);
        }
        else
        {
            ClearSecondaryDisplay();
            ClearTertiaryDisplay();
            ClearQuaternaryDisplay();
            SetupItemButtons();
        }

        if (gameType.hasCoins == false)
        {
            coinCountContainer.SetActive(false);
        }

        continueEndButton.gameObject.SetActive(kart.Object.HasStateAuthority);

        SetupItemEvents();

        kart.OnCoinCountChanged += count =>
        {
            AudioManager.Play("coinSFX", AudioManager.MixerTarget.SFX);
            coinCount.text = $"{count:00}";
        };
    }
    
    // 게임 모드에 따른 UI 설정
    private void SetupGameModeUI(GameType gameType)
    {
        if (gameType.IsScoreCollectionMode())
        {
            // 점수 수집 모드
            SetupScoreMode();
        }
        else if (gameType.IsPracticeMode())
        {
            // 연습 모드 - 랩 UI 숨김
            timesContainer?.SetActive(false);
            lapCountContainer?.SetActive(false);
        }
        else
        {
            // 일반 레이싱 모드 - 기본 설정
            SetupRacingMode();
        }
    }
    
    // 점수 수집 모드 설정
    private void SetupScoreMode()
    {
        // 랩 타임 관련 UI는 숨김
        timesContainer?.SetActive(false);
        
        // 랩 카운터 컨테이너는 점수 표시용으로 계속 사용
        lapCountContainer?.SetActive(true);
        
        // 초기 점수 표시
        UpdateScoreDisplay(-1, 0);
    }
    
    // 일반 레이싱 모드 설정
    private void SetupRacingMode()
    {
        // 기본 레이싱 UI 활성화
        timesContainer?.SetActive(true);
        lapCountContainer?.SetActive(true);
    }
    
    // 점수 표시 업데이트 (기존 랩 카운터 Text 사용)
    private void UpdateScoreDisplay(int playerIndex, int score)
    {
        if (!GameManager.Instance.GameType.IsScoreCollectionMode())
            return;
            
        // 내 점수만 업데이트하거나 초기화(-1)인 경우
        if (playerIndex == -1 || playerIndex == GetMyPlayerIndex())
        {
            currentScore = (playerIndex == -1) ? 0 : score;
            int targetScore = GameManager.Instance.GameType.targetScore;
            
            // 기존 lapCount Text를 점수 표시용으로 사용
            if (lapCount != null)
            {
                lapCount.text = $"{currentScore}/{targetScore}";
                
                // 목표 달성 시 색상 변경
                if (currentScore >= targetScore)
                {
                    lapCount.color = Color.green;
                }
                else if (currentScore >= targetScore * 0.8f) // 80% 달성
                {
                    lapCount.color = Color.yellow;
                }
                else
                {
                    lapCount.color = Color.white;
                }
            }
        }
    }
    
    // 기존 랩 카운트 설정 (레이싱 모드에서만 작동)
    private void SetLapCount(int lap, int maxLaps) 
    { 
        // 점수 수집 모드가 아닐 때만 랩 카운터 업데이트
        if (!GameManager.Instance.GameType.IsScoreCollectionMode())
        {
            if (lapCount != null)
            {
                lapCount.text = $"{(lap > maxLaps ? maxLaps : lap)}/{maxLaps}";
                lapCount.color = Color.white;
            }
        }
    }
    
    private int GetMyPlayerIndex()
    {
        if (RoomPlayer.Local == null) return -1;
        return RoomPlayer.Players.IndexOf(RoomPlayer.Local);
    }
    
    private void OnScoreGameWon(int winnerIndex)
    {
        ShowEndRaceScreen();
        
        if (winnerIndex == GetMyPlayerIndex())
        {
            AudioManager.Play("raceFinishedSFX", AudioManager.MixerTarget.SFX);
        }
    }
    
    private void OnScoreGameTimeUp()
    {
        AudioManager.Play("timeUpSFX", AudioManager.MixerTarget.SFX);
    }
    
    private void SetupItemEvents()
    {
        // 슬롯 2 (E키)
        Kart.OnSecondaryItemChanged += index =>
        {
            if (isSecondarySpinning) return;
            
            if (index == -1)
            {
                ClearSecondaryDisplay();
                previousSecondaryIndex = -1;
            }
            else
            {
                if (previousSecondaryIndex == -1)
                {
                    StartSecondarySpinItem();
                }
                else if (previousSecondaryIndex != index)
                {
                    SetSecondaryDisplay(ResourceManager.Instance.powerups[index]);
                }
                previousSecondaryIndex = index;
            }
            UpdateButtonState(secondaryPickupButton, index != -1);
        };
        
        // 슬롯 3 (R키)
        Kart.OnTertiaryItemChanged += index =>
        {
            if (isTertiarySpinning) return;
            
            if (index == -1)
            {
                ClearTertiaryDisplay();
                previousTertiaryIndex = -1;
            }
            else
            {
                if (previousTertiaryIndex == -1)
                {
                    StartTertiarySpinItem();
                }
                else if (previousTertiaryIndex != index)
                {
                    SetTertiaryDisplay(ResourceManager.Instance.powerups[index]);
                }
                previousTertiaryIndex = index;
            }
            UpdateButtonState(tertiaryPickupButton, index != -1);
        };
        
        // 슬롯 4 (T키)
        Kart.OnQuaternaryItemChanged += index =>
        {
            if (isQuaternarySpinning) return;
            
            if (index == -1)
            {
                ClearQuaternaryDisplay();
                previousQuaternaryIndex = -1;
            }
            else
            {
                if (previousQuaternaryIndex == -1)
                {
                    StartQuaternarySpinItem();
                }
                else if (previousQuaternaryIndex != index)
                {
                    SetQuaternaryDisplay(ResourceManager.Instance.powerups[index]);
                }
                previousQuaternaryIndex = index;
            }
            UpdateButtonState(quaternaryPickupButton, index != -1);
        };
    }
    
    private void SetupItemButtons()
    {
        // 슬롯 2 버튼 - E키 (독립 사용)
        if (secondaryPickupButton != null)
        {
            secondaryPickupButton.onClick.RemoveAllListeners();
            secondaryPickupButton.onClick.AddListener(() =>
            {
                if (Kart && Kart.Items && Kart.SecondaryItem != null)
                {
                    Kart.Items.UseSecondaryItem();
                    AnimateButtonPress(secondaryPickupButton.transform);
                    AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
                }
            });
        }
        
        // 슬롯 3 버튼 - R키
        if (tertiaryPickupButton != null)
        {
            tertiaryPickupButton.onClick.RemoveAllListeners();
            tertiaryPickupButton.onClick.AddListener(() =>
            {
                if (Kart && Kart.Items && Kart.TertiaryItem != null)
                {
                    Kart.Items.UseTertiaryItem();
                    AnimateButtonPress(tertiaryPickupButton.transform);
                    AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
                }
            });
        }
        
        // 슬롯 4 버튼 - T키
        if (quaternaryPickupButton != null)
        {
            quaternaryPickupButton.onClick.RemoveAllListeners();
            quaternaryPickupButton.onClick.AddListener(() =>
            {
                if (Kart && Kart.Items && Kart.QuaternaryItem != null)
                {
                    Kart.Items.UseQuaternaryItem();
                    AnimateButtonPress(quaternaryPickupButton.transform);
                    AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
                }
            });
        }
    }
    
    // 시간 제한 UI 업데이트 (점수 모드 대응)
    private void UpdateTimeLimitUI()
    {
        if (!GameManager.Instance)
            return;
            
        if (GameManager.Instance.GameType.IsPracticeMode())
            return;
            
        if (timeLimitText == null || timeLimitBar == null)
            return;
            
        float remainingTime = GameManager.Instance.GetRemainingTime();
        float totalTime;
        
        // 점수 수집 모드와 레이싱 모드에 따른 시간 계산
        if (GameManager.Instance.GameType.IsScoreCollectionMode())
        {
            totalTime = GameManager.Instance.GameType.scoreTimeLimit;
        }
        else
        {
            totalTime = GameManager.Instance.GameType.timeLimitMinutes * 60f;
        }
        
        float progress = remainingTime / totalTime;
        
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timeLimitText.text = $"{minutes:00}:{seconds:00}";
        
        if (timeLimitBar != null)
        {
            timeLimitBar.fillAmount = progress;
            
            if (remainingTime <= 10f)
            {
                timeLimitBar.color = criticalTimeColor;
                float alpha = Mathf.PingPong(Time.time * 4f, 1f);
                timeLimitBar.color = new Color(criticalTimeColor.r, criticalTimeColor.g, criticalTimeColor.b, alpha);
                timeLimitText.color = new Color(1, 1, 1, alpha);
            }
            else if (remainingTime <= 30f)
            {
                timeLimitBar.color = criticalTimeColor;
                timeLimitText.color = Color.white;
            }
            else if (remainingTime <= 60f)
            {
                timeLimitBar.color = warningTimeColor;
                timeLimitText.color = Color.white;
            }
            else
            {
                timeLimitBar.color = normalTimeColor;
                timeLimitText.color = Color.white;
            }
        }
    }
    
    // 시간 제한 UI 생성
    private void CreateTimeLimitUI()
    {
        Debug.Log("[GameUI] Creating Time Limit UI...");
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found for Time Limit UI!");
            return;
        }
        
        // 메인 컨테이너 생성
        timeLimitContainer = new GameObject("TimeLimitContainer");
        timeLimitContainer.transform.SetParent(canvas.transform, false);
        
        RectTransform containerRect = timeLimitContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 1f);
        containerRect.anchorMax = new Vector2(0.5f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = new Vector2(0, -20);
        containerRect.sizeDelta = new Vector2(400, 60);
        
        timeLimitCanvasGroup = timeLimitContainer.AddComponent<CanvasGroup>();
        
        // 배경 패널
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(timeLimitContainer.transform, false);
        Image bgImage = bgPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        
        RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // 타이머 텍스트
        GameObject timerTextObj = new GameObject("TimerText");
        timerTextObj.transform.SetParent(timeLimitContainer.transform, false);
        timeLimitText = timerTextObj.AddComponent<Text>();
        timeLimitText.text = "5:00";
        timeLimitText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeLimitText.fontSize = 32;
        timeLimitText.fontStyle = FontStyle.Bold;
        timeLimitText.alignment = TextAnchor.MiddleCenter;
        timeLimitText.color = Color.white;
        
        RectTransform textRect = timerTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-20, -10);
        textRect.anchoredPosition = Vector2.zero;
        
        // 프로그레스 바 배경
        GameObject barBgObj = new GameObject("ProgressBarBg");
        barBgObj.transform.SetParent(timeLimitContainer.transform, false);
        timeLimitBarBg = barBgObj.AddComponent<Image>();
        timeLimitBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0, 0);
        barBgRect.anchorMax = new Vector2(1, 0);
        barBgRect.pivot = new Vector2(0.5f, 0);
        barBgRect.sizeDelta = new Vector2(-20, 8);
        barBgRect.anchoredPosition = new Vector2(0, 5);
        
        // 프로그레스 바
        GameObject barObj = new GameObject("ProgressBar");
        barObj.transform.SetParent(barBgObj.transform, false);
        timeLimitBar = barObj.AddComponent<Image>();
        timeLimitBar.color = normalTimeColor;
        timeLimitBar.type = Image.Type.Filled;
        timeLimitBar.fillMethod = Image.FillMethod.Horizontal;
        timeLimitBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(0, 0.5f);
        barRect.sizeDelta = Vector2.zero;
        barRect.anchoredPosition = Vector2.zero;
        
        // 경고 패널 생성
        CreateTimeWarningPanel(canvas);
        
        Debug.Log("[GameUI] Time Limit UI created successfully!");
    }
    
    private void CreateTimeWarningPanel(Canvas canvas)
    {
        timeWarningPanel = new GameObject("TimeWarningPanel");
        timeWarningPanel.transform.SetParent(canvas.transform, false);
        timeWarningPanel.SetActive(false);
        
        RectTransform warningRect = timeWarningPanel.AddComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 0.5f);
        warningRect.anchorMax = new Vector2(0.5f, 0.5f);
        warningRect.pivot = new Vector2(0.5f, 0.5f);
        warningRect.anchoredPosition = new Vector2(0, 100);
        warningRect.sizeDelta = new Vector2(600, 200);
        
        Image warningBg = timeWarningPanel.AddComponent<Image>();
        warningBg.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        
        GameObject warningTextObj = new GameObject("WarningText");
        warningTextObj.transform.SetParent(timeWarningPanel.transform, false);
        timeWarningText = warningTextObj.AddComponent<Text>();
        timeWarningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeWarningText.fontSize = 48;
        timeWarningText.fontStyle = FontStyle.Bold;
        timeWarningText.alignment = TextAnchor.MiddleCenter;
        timeWarningText.color = Color.white;
        timeWarningText.text = "⚠️ WARNING!\n30 SECONDS LEFT!";
        
        RectTransform warningTextRect = warningTextObj.GetComponent<RectTransform>();
        warningTextRect.anchorMin = Vector2.zero;
        warningTextRect.anchorMax = Vector2.one;
        warningTextRect.sizeDelta = Vector2.zero;
        warningTextRect.anchoredPosition = Vector2.zero;
    }
    
    // TIME UP UI 생성
    private void CreateTimeUpUI()
    {
        Debug.Log("[GameUI] Creating Time Up UI...");
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found for Time Up UI!");
            return;
        }
        
        // TIME UP 패널
        timeUpMessagePanel = new GameObject("TimeUpPanel");
        timeUpMessagePanel.transform.SetParent(canvas.transform, false);
        timeUpMessagePanel.SetActive(false);
        
        RectTransform panelRect = timeUpMessagePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        
        // 전체 화면 어두운 배경
        Image bgImage = timeUpMessagePanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.85f);
        
        // TIME UP 텍스트
        GameObject textObj = new GameObject("TimeUpText");
        textObj.transform.SetParent(timeUpMessagePanel.transform, false);
        timeUpMessageText = textObj.AddComponent<Text>();
        timeUpMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeUpMessageText.fontSize = 120;
        timeUpMessageText.fontStyle = FontStyle.Bold;
        timeUpMessageText.alignment = TextAnchor.MiddleCenter;
        timeUpMessageText.color = new Color(1f, 0f, 0f, 1f);
        timeUpMessageText.text = "TIME UP!";
        
        // 그림자 효과
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(5, -5);
        
        // 아웃라인 효과
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3, 3);
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.6f);
        textRect.anchorMax = new Vector2(0.5f, 0.6f);
        textRect.sizeDelta = new Vector2(1000, 200);
        textRect.anchoredPosition = Vector2.zero;
        
        // GAME OVER 텍스트
        GameObject gameOverObj = new GameObject("GameOverText");
        gameOverObj.transform.SetParent(timeUpMessagePanel.transform, false);
        Text gameOverText = gameOverObj.AddComponent<Text>();
        gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gameOverText.fontSize = 80;
        gameOverText.fontStyle = FontStyle.Bold;
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.color = Color.white;
        gameOverText.text = "GAME OVER";
        
        Shadow gameOverShadow = gameOverObj.AddComponent<Shadow>();
        gameOverShadow.effectColor = Color.black;
        gameOverShadow.effectDistance = new Vector2(3, -3);
        
        RectTransform gameOverRect = gameOverObj.GetComponent<RectTransform>();
        gameOverRect.anchorMin = new Vector2(0.5f, 0.4f);
        gameOverRect.anchorMax = new Vector2(0.5f, 0.4f);
        gameOverRect.sizeDelta = new Vector2(800, 150);
        gameOverRect.anchoredPosition = Vector2.zero;
        
        // 메인 메뉴로 돌아가는 메시지
        returnToMenuText = new GameObject("ReturnText");
        returnToMenuText.transform.SetParent(timeUpMessagePanel.transform, false);
        Text returnText = returnToMenuText.AddComponent<Text>();
        returnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        returnText.fontSize = 40;
        returnText.alignment = TextAnchor.MiddleCenter;
        returnText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        returnText.text = "Returning to Main Menu...";
        
        RectTransform returnRect = returnToMenuText.GetComponent<RectTransform>();
        returnRect.anchorMin = new Vector2(0.5f, 0.25f);
        returnRect.anchorMax = new Vector2(0.5f, 0.25f);
        returnRect.sizeDelta = new Vector2(600, 100);
        returnRect.anchoredPosition = Vector2.zero;
        
        Debug.Log("[GameUI] Time Up UI created successfully!");
    }
    
    // TIME UP 이벤트 처리
    private void OnTimeUp()
    {
        Debug.Log("[GameUI] TIME UP! Showing game over screen...");
        
        if (timeUpMessagePanel != null)
        {
            timeUpMessagePanel.SetActive(true);
            
            // 기존 코루틴 중지
            if (_timeUpCoroutine != null)
            {
                StopCoroutine(_timeUpCoroutine);
            }
            
            _timeUpCoroutine = StartCoroutine(TimeUpSequence());
        }
    }
    
    private IEnumerator TimeUpSequence()
    {
        // 카메라 흔들림 효과 (옵션)
        float shakeTime = 0.5f;
        float elapsed = 0;
        
        while (elapsed < shakeTime)
        {
            elapsed += Time.deltaTime;
            Camera.main.transform.position += Random.insideUnitSphere * 0.1f;
            yield return null;
        }
        
        // 3초 대기
        yield return new WaitForSeconds(3f);
        
        // 메인 메뉴로 이동은 GameManager에서 처리
        Debug.Log("[GameUI] Waiting for return to menu...");
    }
    
    // TIME UP 메시지 표시 (공개 메서드)
    public void ShowTimeUpMessage()
    {
        OnTimeUp();
    }
    
    // 각 슬롯별 스핀 메서드들
    public void StartSecondarySpinItem()
    {
        if (isSecondarySpinning) return;
        
        isSecondarySpinning = true;
        
        if (secondaryItemAnimator != null)
            StartCoroutine(SecondarySpinItemRoutine());
        else
            StartCoroutine(FakeSpinRoutine(secondaryPickupDisplay, Kart.SecondaryItemIndex, 2));
    }
    
    private IEnumerator SecondarySpinItemRoutine()
    {
        secondaryItemAnimator.SetBool("Ticking", true);
        float dur = 3;
        float spd = Random.Range(9f, 11f);
        float x = 0;
        
        while (x < dur - 0.5f)
        {
            x += Time.deltaTime;
            secondaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            
            if (secondaryPickupDisplay != null)
            {
                int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
                secondaryPickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
            }
            
            yield return null;
        }
        
        while (x < dur)
        {
            x += Time.deltaTime;
            secondaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            yield return null;
        }

        secondaryItemAnimator.SetBool("Ticking", false);
        
        if (Kart.SecondaryItemIndex != -1)
        {
            SetSecondaryDisplay(ResourceManager.Instance.powerups[Kart.SecondaryItemIndex]);
            previousSecondaryIndex = Kart.SecondaryItemIndex;
        }
        else
        {
            ClearSecondaryDisplay();
        }
        
        isSecondarySpinning = false;
    }
    
    public void StartTertiarySpinItem()
    {
        if (isTertiarySpinning) return;
        
        isTertiarySpinning = true;
        
        if (tertiaryItemAnimator != null)
            StartCoroutine(TertiarySpinItemRoutine());
        else
            StartCoroutine(FakeSpinRoutine(tertiaryPickupDisplay, Kart.TertiaryItemIndex, 3));
    }
    
    private IEnumerator TertiarySpinItemRoutine()
    {
        tertiaryItemAnimator.SetBool("Ticking", true);
        float dur = 3;
        float spd = Random.Range(9f, 11f);
        float x = 0;
        
        while (x < dur - 0.5f)
        {
            x += Time.deltaTime;
            tertiaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            
            if (tertiaryPickupDisplay != null)
            {
                int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
                tertiaryPickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
            }
            
            yield return null;
        }
        
        while (x < dur)
        {
            x += Time.deltaTime;
            tertiaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            yield return null;
        }

        tertiaryItemAnimator.SetBool("Ticking", false);
        
        if (Kart.TertiaryItemIndex != -1)
        {
            SetTertiaryDisplay(ResourceManager.Instance.powerups[Kart.TertiaryItemIndex]);
            previousTertiaryIndex = Kart.TertiaryItemIndex;
        }
        else
        {
            ClearTertiaryDisplay();
        }
        
        isTertiarySpinning = false;
    }
    
    public void StartQuaternarySpinItem()
    {
        if (isQuaternarySpinning) return;
        
        isQuaternarySpinning = true;
        
        if (quaternaryItemAnimator != null)
            StartCoroutine(QuaternarySpinItemRoutine());
        else
            StartCoroutine(FakeSpinRoutine(quaternaryPickupDisplay, Kart.QuaternaryItemIndex, 4));
    }
    
    private IEnumerator QuaternarySpinItemRoutine()
    {
        quaternaryItemAnimator.SetBool("Ticking", true);
        float dur = 3;
        float spd = Random.Range(9f, 11f);
        float x = 0;
        
        while (x < dur - 0.5f)
        {
            x += Time.deltaTime;
            quaternaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            
            if (quaternaryPickupDisplay != null)
            {
                int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
                quaternaryPickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
            }
            
            yield return null;
        }
        
        while (x < dur)
        {
            x += Time.deltaTime;
            quaternaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
            yield return null;
        }

        quaternaryItemAnimator.SetBool("Ticking", false);
        
        if (Kart.QuaternaryItemIndex != -1)
        {
            SetQuaternaryDisplay(ResourceManager.Instance.powerups[Kart.QuaternaryItemIndex]);
            previousQuaternaryIndex = Kart.QuaternaryItemIndex;
        }
        else
        {
            ClearQuaternaryDisplay();
        }
        
        isQuaternarySpinning = false;
    }
    
    // 공통 FakeSpinRoutine
    private IEnumerator FakeSpinRoutine(Image display, int finalItemIndex, int slotNumber)
    {
        if (display == null || finalItemIndex < 0) yield break;
        
        float dur = 3;
        float x = 0;
        
        while (x < dur - 0.5f)
        {
            x += Time.deltaTime;
            
            if ((int)(x * 10) % 1 == 0)
            {
                int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
                display.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
                
                if (x < 2.5f && (int)(x * 5) % 1 == 0)
                {
                    AudioManager.Play("tickItemUI", AudioManager.MixerTarget.UI);
                }
            }
            
            yield return null;
        }
        
        display.sprite = ResourceManager.Instance.powerups[finalItemIndex].itemIcon;
        AudioManager.Play("itemCollectSFX", AudioManager.MixerTarget.SFX);
        
        switch (slotNumber)
        {
            case 2:
                isSecondarySpinning = false;
                break;
            case 3:
                isTertiarySpinning = false;
                break;
            case 4:
                isQuaternarySpinning = false;
                break;
        }
    }
    
    // Display 메서드들
    public void SetSecondaryDisplay(Powerup item)
    {
        if (secondaryPickupDisplay == null) return;
        
        if (item)
            secondaryPickupDisplay.sprite = item.itemIcon;
        else
            secondaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
    }
    
    public void ClearSecondaryDisplay()
    {
        if (secondaryPickupDisplay != null)
        {
            secondaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : 
                (ResourceManager.Instance.noPowerup != null ? ResourceManager.Instance.noPowerup.itemIcon : null);
        }
        UpdateButtonState(secondaryPickupButton, false);
    }
    
    public void SetTertiaryDisplay(Powerup item)
    {
        if (tertiaryPickupDisplay == null) return;
        
        if (item)
            tertiaryPickupDisplay.sprite = item.itemIcon;
        else
            tertiaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
    }
    
    public void ClearTertiaryDisplay()
    {
        if (tertiaryPickupDisplay != null)
        {
            tertiaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : 
                (ResourceManager.Instance.noPowerup != null ? ResourceManager.Instance.noPowerup.itemIcon : null);
        }
        UpdateButtonState(tertiaryPickupButton, false);
    }
    
    public void SetQuaternaryDisplay(Powerup item)
    {
        if (quaternaryPickupDisplay == null) return;
        
        if (item)
            quaternaryPickupDisplay.sprite = item.itemIcon;
        else
            quaternaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
    }
    
    public void ClearQuaternaryDisplay()
    {
        if (quaternaryPickupDisplay != null)
        {
            quaternaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : 
                (ResourceManager.Instance.noPowerup != null ? ResourceManager.Instance.noPowerup.itemIcon : null);
        }
        UpdateButtonState(quaternaryPickupButton, false);
    }
    
    private void ShowTimeWarning(float remainingTime)
    {
        if (_isTimeWarningShown) return;
        _isTimeWarningShown = true;
        
        if (_timeWarningCoroutine != null)
            StopCoroutine(_timeWarningCoroutine);
        
        _timeWarningCoroutine = StartCoroutine(TimeWarningRoutine(remainingTime));
    }
    
    private IEnumerator TimeWarningRoutine(float remainingTime)
    {
        if (timeWarningPanel == null) yield break;
        
        timeWarningPanel.SetActive(true);
        
        if (timeWarningText != null)
        {
            timeWarningText.text = $"⚠️ WARNING!\n{(int)remainingTime} SECONDS LEFT!";
        }
        
        AudioManager.Play("warningTimeSFX", AudioManager.MixerTarget.SFX);
        
        yield return new WaitForSeconds(3f);
        
        timeWarningPanel.SetActive(false);
        _timeWarningCoroutine = null;
    }
    
    private void UpdateButtonState(Button button, bool hasItem)
    {
        if (button != null)
        {
            button.interactable = hasItem;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = hasItem ? new Color(1, 1, 1, 1f) : new Color(1, 1, 1, 0.3f);
            }
        }
    }
    
    private void AnimateButtonPress(Transform buttonTransform)
    {
        StartCoroutine(ButtonPressAnimation(buttonTransform));
    }
    
    private IEnumerator ButtonPressAnimation(Transform target)
    {
        if (target == null) yield break;
        
        Vector3 originalScale = target.localScale;
        float duration = 0.15f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = Vector3.Lerp(originalScale, originalScale * 0.85f, t);
            yield return null;
        }
        
        elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = Vector3.Lerp(originalScale * 0.85f, originalScale, t);
            yield return null;
        }
        
        target.localScale = originalScale;
    }
    
    // Update 메서드
    private void Update()
    {
        if (!Kart || !Kart.LapController.Object || !Kart.LapController.Object.IsValid)
            return;

        // 레이싱 모드에서만 카운트다운 처리
        if (!GameManager.Instance.GameType.IsScoreCollectionMode())
        {
            if (!_startedCountdown && Track.Current != null && Track.Current.StartRaceTimer.IsRunning)
            {
                var remainingTime = Track.Current.StartRaceTimer.RemainingTime(Kart.Runner);
                if (remainingTime != null && remainingTime <= 3.0f)
                {
                    _startedCountdown = true;
                    HideIntro();
                    FadeIn();
                    countdownAnimator.SetTrigger("StartCountdown");
                }
            }

            if (Kart.LapController.enabled) UpdateLapTimes();
        }

        UpdateBoostBar();
        UpdateTimeLimitUI();

        var controller = Kart.Controller;
        if (controller.BoostTime > 0f)
        {
            if (controller.BoostTierIndex == -1) return;
            Color color = controller.driftTiers[controller.BoostTierIndex].color;
            SetBoostBarColor(color);
        }
        else
        {
            if (!controller.IsDrifting) return;
            SetBoostBarColor(controller.DriftTierIndex < controller.driftTiers.Length - 1
                ? controller.driftTiers[controller.DriftTierIndex + 1].color
                : controller.driftTiers[controller.DriftTierIndex].color);
        }
    }
    
    private void OnDestroy()
    {
        if (Kart != null && Kart.LapController != null)
            Kart.LapController.OnLapChanged -= SetLapCount;
        
        GameManager.OnTimeWarning -= ShowTimeWarning;
        GameManager.OnTimeUp -= OnTimeUp;
        
        if (_timeWarningCoroutine != null)
            StopCoroutine(_timeWarningCoroutine);
        if (_timeUpCoroutine != null)
            StopCoroutine(_timeUpCoroutine);
    }
    
    // 나머지 기존 메서드들
    public void FinishCountdown() { }
    public void HideIntro() { introAnimator.SetTrigger("Exit"); }
    public void HideEndRaceScreen() 
    { 
        if (endRaceScreen != null && endRaceScreen.gameObject != null)
            endRaceScreen.gameObject.SetActive(false); 
    }
    
    private void FadeIn() { StartCoroutine(FadeInRoutine()); }
    
    private IEnumerator FadeInRoutine()
    {
        float t = 1;
        while (t > 0)
        {
            fader.alpha = 1 - t;
            t -= Time.deltaTime;
            yield return null;
        }
    }
    
    private void UpdateBoostBar()
    {
        if (!KartController.Object || !KartController.Object.IsValid)
            return;
        
        var driftIndex = KartController.DriftTierIndex;
        var boostIndex = KartController.BoostTierIndex;

        if (KartController.IsDrifting)
        {
            if (driftIndex < KartController.driftTiers.Length - 1)
                SetBoostBar((KartController.DriftTime - KartController.driftTiers[driftIndex].startTime) /
                            (KartController.driftTiers[driftIndex + 1].startTime - KartController.driftTiers[driftIndex].startTime));
            else
                SetBoostBar(1);
        }
        else
        {
            SetBoostBar(boostIndex == -1
                ? 0f
                : KartController.BoostTime / KartController.driftTiers[boostIndex].boostDuration);
        }
    }
    
    private void UpdateLapTimes()
    {
        if (!Kart.LapController.Object || !Kart.LapController.Object.IsValid)
            return;
        var lapTimes = Kart.LapController.LapTicks;
        for (var i = 0; i < Mathf.Min(lapTimes.Length, lapTimeTexts.Length); i++)
        {
            var lapTicks = lapTimes.Get(i);

            if (lapTicks == 0)
            {
                lapTimeTexts[i].text = "";
            }
            else
            {
                var previousTicks = i == 0
                    ? Kart.LapController.StartRaceTick
                    : lapTimes.Get(i - 1);

                var deltaTicks = lapTicks - previousTicks;
                var time = TickHelper.TickToSeconds(Kart.Runner, deltaTicks);

                SetLapTimeText(time, i);
            }
        }

        SetRaceTimeText(Kart.LapController.GetTotalRaceTime());
    }
    
    public void SetBoostBar(float amount) { boostBar.fillAmount = amount; }
    public void SetBoostBarColor(Color color) { boostBar.color = color; }
    public void SetCoinCount(int count) { coinCount.text = $"{count:00}"; }
    public void SetRaceTimeText(float time) { raceTimeText.text = $"{(int)(time / 60):00}:{time % 60:00.000}"; }
    public void SetLapTimeText(float time, int index) { lapTimeTexts[index].text = $"<color=#FFC600>L{index + 1}</color> {(int)(time / 60):00}:{time % 60:00.000}"; }
    public void ShowEndRaceScreen() { endRaceScreen.gameObject.SetActive(true); }
    public void OpenPauseMenu() { InterfaceManager.Instance.OpenPauseMenu(); }
}