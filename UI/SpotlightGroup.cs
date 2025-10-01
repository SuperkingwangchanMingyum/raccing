using System.Collections.Generic;
using UnityEngine;

public class SpotlightGroup : MonoBehaviour
{
    private static readonly Dictionary<string, SpotlightGroup> spotlights = new Dictionary<string, SpotlightGroup>();

    public string searchName = "";
    public int defaultIndex = -1;
    public List<GameObject> objects;

    private GameObject focused = null;

    public static bool Search(string spotlightName, out SpotlightGroup spotlight)
    {
        return spotlights.TryGetValue(spotlightName, out spotlight);
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(searchName) == false)
        {
            if (spotlights.ContainsKey(searchName))
            {
                spotlights[searchName] = this;
            }
            else
            {
                spotlights.Add(searchName, this);
            }
        }
        
        // Kart Display인 경우 자동으로 카트 찾기
        if (searchName == "Kart Display")
        {
            AutoFindKarts();
        }
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(searchName) == false)
        {
            if (spotlights.ContainsKey(searchName) && spotlights[searchName] == this)
            {
                spotlights.Remove(searchName);
            }
        }
    }

    private void Awake()
    {
        // Kart Display인 경우 자동으로 카트 찾기
        if (searchName == "Kart Display")
        {
            AutoFindKarts();
        }
        
        // 모든 오브젝트 초기화 (비활성화)
        if (objects != null)
        {
            foreach (var obj in objects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
        
        // 기본 인덱스가 설정되어 있으면 해당 오브젝트 활성화
        if (defaultIndex >= 0 && defaultIndex < objects.Count && objects[defaultIndex] != null)
        {
            FocusIndex(defaultIndex);
        }
    }

    /// <summary>
    /// 자동으로 모든 카트 찾기 (ResourceManager의 kartDefinitions 수만큼)
    /// </summary>
    private void AutoFindKarts()
    {
        // ResourceManager가 있는지 확인
        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            resourceManager = FindObjectOfType<ResourceManager>();
        }
        
        if (resourceManager != null && resourceManager.kartDefinitions != null)
        {
            int kartCount = resourceManager.kartDefinitions.Length;
            
            // objects 리스트 초기화
            if (objects == null)
                objects = new List<GameObject>();
            
            // 리스트 크기 맞추기
            while (objects.Count < kartCount)
            {
                objects.Add(null);
            }
            
            // 각 인덱스별로 카트 찾기
            for (int i = 0; i < kartCount; i++)
            {
                if (objects.Count > i && objects[i] == null)
                {
                    // 여러 방법으로 카트 찾기
                    GameObject kart = FindKartByIndex(i);
                    if (kart != null)
                    {
                        objects[i] = kart;
                        Debug.Log($"[SpotlightGroup] Auto-assigned Kart at index {i}: {kart.name}");
                    }
                }
            }
        }
        
        Debug.Log($"[SpotlightGroup] Total karts found: {objects?.Count ?? 0}");
    }

    /// <summary>
    /// 인덱스에 해당하는 카트 찾기
    /// </summary>
    private GameObject FindKartByIndex(int index)
    {
        // 일반적인 카트 이름 패턴들
        string[] possibleNames = {
            "Red Kart",      // 0
            "Green Kart",    // 1
            "Blue Kart",     // 2
            "Kart 4",        // 3
            "Rainbow Kart",  // 3 (alternative)
            "Gray Kart",     // 3 (alternative)
            "Kart4",         // 3 (no space)
            "KartPrefab4",   // 3 (prefab name)
            $"Kart ({index})", // generic pattern
            $"Kart{index}"     // generic pattern without space
        };

        GameObject foundKart = null;
        
        // 1. 부모의 자식들에서 찾기
        if (transform.parent != null)
        {
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                Transform child = transform.parent.GetChild(i);
                if (IsKartAtIndex(child.name, index))
                {
                    foundKart = child.gameObject;
                    break;
                }
            }
        }
        
        // 2. 같은 레벨의 형제들에서 찾기
        if (foundKart == null)
        {
            Transform[] siblings = GetComponentsInParent<Transform>();
            foreach (var sibling in siblings)
            {
                if (IsKartAtIndex(sibling.name, index))
                {
                    foundKart = sibling.gameObject;
                    break;
                }
            }
        }
        
        // 3. Scene 전체에서 찾기
        if (foundKart == null)
        {
            foreach (string kartName in possibleNames)
            {
                foundKart = GameObject.Find(kartName);
                if (foundKart != null)
                    break;
            }
        }
        
        // 4. 태그로 찾기 (만약 Kart 태그가 있다면)
        if (foundKart == null)
        {
            GameObject[] karts = GameObject.FindGameObjectsWithTag("Kart");
            if (karts.Length > index)
            {
                foundKart = karts[index];
            }
        }
        
        return foundKart;
    }

    /// <summary>
    /// 이름이 특정 인덱스의 카트인지 확인
    /// </summary>
    private bool IsKartAtIndex(string name, int index)
    {
        name = name.ToLower();
        
        // 인덱스별 특별 케이스
        switch (index)
        {
            case 0: return name.Contains("red");
            case 1: return name.Contains("green");
            case 2: return name.Contains("blue");
            case 3: return name.Contains("4") || name.Contains("rainbow") || name.Contains("gray");
            default: return name.Contains(index.ToString());
        }
    }

    public void FocusIndex(int index)
    {
        // 오브젝트 리스트 유효성 체크
        if (objects == null || objects.Count == 0)
        {
            Debug.LogWarning($"SpotlightGroup '{searchName}': No objects in list. Trying to find karts...");
            AutoFindKarts();
            
            if (objects == null || objects.Count == 0)
            {
                Debug.LogError($"SpotlightGroup '{searchName}': Still no objects after auto-find!");
                return;
            }
        }
        
        // 인덱스 범위 체크
        if (index < 0 || index >= objects.Count)
        {
            Debug.LogWarning($"SpotlightGroup '{searchName}': Invalid index {index}. Valid range: 0-{objects.Count - 1}");
            
            // 리스트 확장 시도
            if (index >= 0 && searchName == "Kart Display")
            {
                while (objects.Count <= index)
                {
                    objects.Add(null);
                }
                AutoFindKarts();
            }
            else
            {
                return;
            }
        }
        
        // NULL 체크
        if (objects[index] == null)
        {
            Debug.LogWarning($"SpotlightGroup '{searchName}': Object at index {index} is null. Trying to find it...");
            
            GameObject kart = FindKartByIndex(index);
            if (kart != null)
            {
                objects[index] = kart;
                Debug.Log($"Found and assigned kart for index {index}: {kart.name}");
            }
            else
            {
                Debug.LogError($"Could not find kart for index {index}!");
                return;
            }
        }
        
        // 이전에 포커스된 오브젝트 비활성화
        if (focused != null && focused != objects[index])
        {
            focused.SetActive(false);
        }
        
        // 새 오브젝트 포커스 및 활성화
        focused = objects[index];
        focused.SetActive(true);
        
        Debug.Log($"SpotlightGroup '{searchName}': Focused on index {index} - {focused.name}");
    }

    public void Defocus()
    {
        if (focused != null)
        {
            focused.SetActive(false);
        }
        focused = null;
    }
    
    /// <summary>
    /// 디버그: 현재 상태 출력
    /// </summary>
    [ContextMenu("Debug - Print All Karts")]
    public void DebugPrintAllKarts()
    {
        Debug.Log($"===== SpotlightGroup '{searchName}' Debug =====");
        
        if (objects != null)
        {
            Debug.Log($"Total objects: {objects.Count}");
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] == null)
                    Debug.LogWarning($"  [{i}] NULL - Need to assign!");
                else
                    Debug.Log($"  [{i}] {objects[i].name} (Active: {objects[i].activeSelf})");
            }
        }
        else
        {
            Debug.LogError("Objects list is NULL!");
        }
        
        // ResourceManager 확인
        var rm = ResourceManager.Instance;
        if (rm != null && rm.kartDefinitions != null)
        {
            Debug.Log($"ResourceManager has {rm.kartDefinitions.Length} kart definitions");
        }
    }
    
    [ContextMenu("Debug - Force Find All Karts")]
    public void DebugForceFindKarts()
    {
        AutoFindKarts();
        DebugPrintAllKarts();
    }
}