using UnityEngine;

[CreateAssetMenu(fileName = "New Game Type", menuName = "Scriptable Object/Game Type")]
public class GameType : ScriptableObject
{
    [Header("Basic Settings")]
    public string modeName;
    public int lapCount;
    public bool hasCoins;
    public bool hasPickups;
    
    [Header("Score Collection Mode")]
    public bool isScoreCollectionMode = false;  // 점수 수집 모드 플래그
    public int targetScore = 100;               // 목표 점수
    public float scoreTimeLimit = 300f;         // 점수 모드 시간 제한
    
    [Header("Time Limit Settings")]
    public bool hasTimeLimit = true;
    public float timeLimitMinutes = 6.0f;
    public float warningTimeSeconds = 30.0f;
    
    [Header("Special Rules")]
    public bool allowRespawn = true;
    public bool showLapTimes = true;
    public bool showPosition = true;
    
    // 연습 모드 체크
    public bool IsPracticeMode() 
    { 
        return lapCount == 0 && !isScoreCollectionMode;
    }
    
    // 점수 수집 모드 체크
    public bool IsScoreCollectionMode() 
    { 
        return isScoreCollectionMode;
    }
    
    // 시간 제한 적용 여부
    public bool ShouldApplyTimeLimit()
    {
        if (isScoreCollectionMode)
            return scoreTimeLimit > 0;
        return hasTimeLimit && !IsPracticeMode();
    }
    
    // 효과적인 시간 제한 값 반환
    public float GetEffectiveTimeLimit()
    {
        if (isScoreCollectionMode)
            return scoreTimeLimit / 60f; // 분 단위로 변환
        return timeLimitMinutes;
    }
    
    // UI에서 표시할 목표값 반환
    public int GetTargetValue()
    {
        return isScoreCollectionMode ? targetScore : lapCount;
    }
    
    // UI 진행도 텍스트 생성
    public string GetProgressText(int currentValue)
    {
        int target = GetTargetValue();
        return $"{currentValue}/{target}";
    }
    
    // 시간 초과 시 처리
    public bool IsTimeUp(float remainingTime)
    {
        return ShouldApplyTimeLimit() && remainingTime <= 0;
    }
    
    // 경고 시간 체크
    public bool ShouldShowWarning(float remainingTime)
    {
        return ShouldApplyTimeLimit() && remainingTime <= warningTimeSeconds && remainingTime > 0;
    }
}