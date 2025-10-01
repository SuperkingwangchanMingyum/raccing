using UnityEngine;
using UnityEngine.UI;

public class KartSelectUI : MonoBehaviour
{
    public Image speedStatBar;
    public Image accelStatBar;
    public Image turnStatBar;

    private void OnEnable() 
    {
        // 저장된 카트 ID가 범위를 벗어나면 0으로 리셋
        if (ClientInfo.KartId >= ResourceManager.Instance.kartDefinitions.Length)
        {
            ClientInfo.KartId = 0;
        }
        SelectKart(ClientInfo.KartId);
    }

    public void SelectKart(int kartIndex)
    {
        // 🔴 중요: 범위 체크 추가
        if (!IsValidKartIndex(kartIndex))
        {
            Debug.LogError($"[KartSelectUI] Invalid kart index: {kartIndex}. Max available: {ResourceManager.Instance.kartDefinitions.Length - 1}");
            return;
        }

        Debug.Log($"[KartSelectUI] Selecting kart {kartIndex} of {ResourceManager.Instance.kartDefinitions.Length} available karts");
        
        ClientInfo.KartId = kartIndex;
        
        // 3D 모델 표시 (안전하게 처리)
        if (SpotlightGroup.Search("Kart Display", out SpotlightGroup spotlight)) 
        {
            // SpotlightGroup도 범위 체크
            if (kartIndex < spotlight.objects.Count)
            {
                spotlight.FocusIndex(kartIndex);
            }
            else
            {
                Debug.LogWarning($"[KartSelectUI] SpotlightGroup doesn't have enough objects. Has {spotlight.objects.Count}, needs {kartIndex + 1}");
            }
        }
        else
        {
            Debug.LogWarning("[KartSelectUI] 'Kart Display' SpotlightGroup not found!");
        }
        
        ApplyStats();

        // 네트워크 동기화
        if (RoomPlayer.Local != null) 
        {
            RoomPlayer.Local.RPC_SetKartId(kartIndex);
        }
    }

    private bool IsValidKartIndex(int index)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[KartSelectUI] ResourceManager.Instance is null!");
            return false;
        }

        if (ResourceManager.Instance.kartDefinitions == null)
        {
            Debug.LogError("[KartSelectUI] kartDefinitions array is null!");
            return false;
        }

        if (index < 0 || index >= ResourceManager.Instance.kartDefinitions.Length)
        {
            return false;
        }

        if (ResourceManager.Instance.kartDefinitions[index] == null)
        {
            Debug.LogError($"[KartSelectUI] Kart definition at index {index} is null!");
            return false;
        }

        return true;
    }

    private void ApplyStats()
    {
        if (!IsValidKartIndex(ClientInfo.KartId))
        {
            Debug.LogError("[KartSelectUI] Cannot apply stats - invalid kart ID");
            return;
        }

        KartDefinition def = ResourceManager.Instance.kartDefinitions[ClientInfo.KartId];
        
        // UI 요소 null 체크
        if (speedStatBar != null) speedStatBar.fillAmount = def.SpeedStat;
        if (accelStatBar != null) accelStatBar.fillAmount = def.AccelStat;
        if (turnStatBar != null) turnStatBar.fillAmount = def.TurnStat;
    }

    // 디버그용 메서드 추가
    [ContextMenu("Debug Kart Info")]
    public void DebugKartInfo()
    {
        Debug.Log("=== Kart Selection Debug Info ===");
        Debug.Log($"Total Karts Available: {ResourceManager.Instance.kartDefinitions.Length}");
        
        for (int i = 0; i < ResourceManager.Instance.kartDefinitions.Length; i++)
        {
            var kart = ResourceManager.Instance.kartDefinitions[i];
            Debug.Log($"Slot {i}: {(kart != null ? kart.name : "EMPTY")}");
        }
        
        if (SpotlightGroup.Search("Kart Display", out SpotlightGroup spotlight))
        {
            Debug.Log($"SpotlightGroup 'Kart Display' has {spotlight.objects.Count} display objects");
        }
        else
        {
            Debug.LogError("SpotlightGroup 'Kart Display' NOT FOUND!");
        }
    }
}