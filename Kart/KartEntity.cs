using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class KartEntity : KartComponent
{
    public static event Action<KartEntity> OnKartSpawned;
    public static event Action<KartEntity> OnKartDespawned;

    // 아이템 변경 이벤트 - 슬롯 2,3,4만 사용
    public event Action<int> OnSecondaryItemChanged;
    public event Action<int> OnTertiaryItemChanged;
    public event Action<int> OnQuaternaryItemChanged;
    public event Action<int> OnCoinCountChanged;
    
    // 호환성을 위한 이벤트 (Secondary와 동일)
    public event Action<int> OnHeldItemChanged
    {
        add { OnSecondaryItemChanged += value; }
        remove { OnSecondaryItemChanged -= value; }
    }
    
    public event Action<int> OnPrimaryItemChanged
    {
        add { OnSecondaryItemChanged += value; }
        remove { OnSecondaryItemChanged -= value; }
    }
    
    // 컴포넌트 참조
    public KartAnimator Animator { get; private set; }
    public KartCamera Camera { get; private set; }
    public KartController Controller { get; private set; }
    public KartInput Input { get; private set; }
    public KartLapController LapController { get; private set; }
    public KartAudio Audio { get; private set; }
    public GameUI Hud { get; private set; }
    public KartItemController Items { get; private set; }
    public NetworkRigidbody3D Rigidbody { get; private set; }

    // 아이템 슬롯들
    public Powerup SecondaryItem =>
        SecondaryItemIndex == -1 ? null : ResourceManager.Instance.powerups[SecondaryItemIndex];

    public Powerup TertiaryItem =>
        TertiaryItemIndex == -1 ? null : ResourceManager.Instance.powerups[TertiaryItemIndex];
    
    public Powerup QuaternaryItem =>
        QuaternaryItemIndex == -1 ? null : ResourceManager.Instance.powerups[QuaternaryItemIndex];

    // 호환성 프로퍼티
    public Powerup HeldItem => SecondaryItem;
    public Powerup PrimaryItem => SecondaryItem;

    [Networked] public int SecondaryItemIndex { get; set; } = -1;
    [Networked] public int TertiaryItemIndex { get; set; } = -1;
    [Networked] public int QuaternaryItemIndex { get; set; } = -1;
    [Networked] public int CoinCount { get; set; }
    
    // 호환성을 위한 네트워크 변수
    [Networked] public int HeldItemIndex { get; set; } = -1;
    [Networked] public int PrimaryItemIndex { get; set; } = -1;
    [Networked] public int CurrentSlotIndex { get; set; } = 1;

    public Transform itemDropNode;

    private bool _despawned;
    private ChangeDetector _changeDetector;
    
    // 이전 값 추적
    private int previousSecondaryIndex = -1;
    private int previousTertiaryIndex = -1;
    private int previousQuaternaryIndex = -1;

    public static readonly List<KartEntity> Karts = new List<KartEntity>();

    private void Awake()
    {
        Animator = GetComponentInChildren<KartAnimator>();
        Camera = GetComponent<KartCamera>();
        Controller = GetComponent<KartController>();
        Input = GetComponent<KartInput>();
        LapController = GetComponent<KartLapController>();
        Audio = GetComponentInChildren<KartAudio>();
        Items = GetComponent<KartItemController>();
        Rigidbody = GetComponent<NetworkRigidbody3D>();

        var components = GetComponentsInChildren<KartComponent>();
        foreach (var component in components) component.Init(this);
    }

    public override void Spawned()
    {
        base.Spawned();
        
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        if (Object.HasInputAuthority)
        {
            Hud = Instantiate(ResourceManager.Instance.hudPrefab);
            Hud.Init(this);
            Instantiate(ResourceManager.Instance.nicknameCanvasPrefab);
        }

        Karts.Add(this);
        OnKartSpawned?.Invoke(this);
    }
    
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(SecondaryItemIndex):
                    if (SecondaryItemIndex != previousSecondaryIndex)
                    {
                        OnSecondaryItemChangedCallback(this);
                        previousSecondaryIndex = SecondaryItemIndex;
                    }
                    break;
                    
                case nameof(TertiaryItemIndex):
                    if (TertiaryItemIndex != previousTertiaryIndex)
                    {
                        OnTertiaryItemChangedCallback(this);
                        previousTertiaryIndex = TertiaryItemIndex;
                    }
                    break;
                    
                case nameof(QuaternaryItemIndex):
                    if (QuaternaryItemIndex != previousQuaternaryIndex)
                    {
                        OnQuaternaryItemChangedCallback(this);
                        previousQuaternaryIndex = QuaternaryItemIndex;
                    }
                    break;
                    
                case nameof(CoinCount):
                    OnCoinCountChangedCallback(this);
                    break;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        Karts.Remove(this);
        _despawned = true;
        OnKartDespawned?.Invoke(this);
    }

    private void OnDestroy()
    {
        Karts.Remove(this);
        if (!_despawned)
        {
            OnKartDespawned?.Invoke(this);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out ICollidable collidable))
        {
            collidable.Collide(this);
        }
    }

    // 정적 콜백 메서드들
    private static void OnSecondaryItemChangedCallback(KartEntity changed)
    {
        changed.OnSecondaryItemChanged?.Invoke(changed.SecondaryItemIndex);

        if (changed.SecondaryItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.SecondaryItem, 3f, 1);
        }
    }

    private static void OnTertiaryItemChangedCallback(KartEntity changed)
    {
        changed.OnTertiaryItemChanged?.Invoke(changed.TertiaryItemIndex);

        if (changed.TertiaryItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.TertiaryItem, 2f, 2);
        }
    }
    
    private static void OnQuaternaryItemChangedCallback(KartEntity changed)
    {
        changed.OnQuaternaryItemChanged?.Invoke(changed.QuaternaryItemIndex);

        if (changed.QuaternaryItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.QuaternaryItem, 3f, 3);
        }
    }

    private static void OnCoinCountChangedCallback(KartEntity changed)
    {
        changed.OnCoinCountChanged?.Invoke(changed.CoinCount);
    }

    // 아이템 설정 메서드들
    public bool SetSecondaryItem(int index)
    {
        SecondaryItemIndex = index;
        return true;
    }
    
    public bool SetTertiaryItem(int index)
    {
        TertiaryItemIndex = index;
        return true;
    }
    
    public bool SetQuaternaryItem(int index)
    {
        QuaternaryItemIndex = index;
        return true;
    }
    
    // 호환성 메서드들
    public bool SetHeldItem(int index)
    {
        return SetSecondaryItem(index);
    }
    
    public bool SetPrimaryItem(int index)
    {
        return SetSecondaryItem(index);
    }
    
    public int GetCurrentSlot() => CurrentSlotIndex;
    
    public void SetCurrentSlot(int slotIndex)
    {
        CurrentSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
    }
    
    public bool HasEmptySlot()
    {
        return SecondaryItemIndex == -1 || TertiaryItemIndex == -1 || QuaternaryItemIndex == -1;
    }
    
    public bool AreAllSlotsFull()
    {
        return SecondaryItemIndex != -1 && TertiaryItemIndex != -1 && QuaternaryItemIndex != -1;
    }

    public void SpinOut()
    {
        Controller.IsSpinout = true;
        StartCoroutine(OnSpinOut());
    }

    private IEnumerator OnSpinOut()
    {
        yield return new WaitForSeconds(2f);
        Controller.IsSpinout = false;
    }
}