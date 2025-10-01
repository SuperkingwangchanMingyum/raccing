using UnityEngine;
using Fusion;
using Random = UnityEngine.Random;

public class ItemBox : NetworkBehaviour, ICollidable 
{
    public GameObject model;
    public ParticleSystem breakParticle;
    public float cooldown = 5f;
    public Transform visuals;

    [Networked] public KartEntity Kart { get; set; }
    [Networked] public TickTimer DisabledTimer { get; set; }
    
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Kart):
                    OnKartChanged(this);
                    break;
            }
        }
    }

    public bool Collide(KartEntity kart) 
    {
        if (kart != null && DisabledTimer.ExpiredOrNotRunning(Runner) && Kart == null) 
        {
            Kart = kart;
            DisabledTimer = TickTimer.CreateFromSeconds(Runner, cooldown);
            
            if (Object.HasStateAuthority) 
            {
                GiveItemToSlot(kart);
            }
        }
        return true;
    }
    
    private void GiveItemToSlot(KartEntity kart)
    {
        var powerUp = GetRandomPowerup();
        
        // 빈 슬롯 순서대로 채우기 (2 -> 3 -> 4)
        if (kart.SecondaryItemIndex == -1)
        {
            kart.SecondaryItemIndex = powerUp;
            return;
        }
        
        if (kart.TertiaryItemIndex == -1)
        {
            kart.TertiaryItemIndex = powerUp;
            return;
        }
        
        if (kart.QuaternaryItemIndex == -1)
        {
            kart.QuaternaryItemIndex = powerUp;
            return;
        }
        
        // 모든 슬롯이 차있으면 현재 선택된 슬롯 교체
        int currentSlot = kart.GetCurrentSlot();
        
        switch (currentSlot)
        {
            case 1: // 슬롯 2
                kart.SecondaryItemIndex = powerUp;
                break;
            case 2: // 슬롯 3
                kart.TertiaryItemIndex = powerUp;
                break;
            case 3: // 슬롯 4
                kart.QuaternaryItemIndex = powerUp;
                break;
            default:
                kart.SecondaryItemIndex = powerUp;
                break;
        }
    }

    private static void OnKartChanged(ItemBox changed) 
    { 
        changed.OnKartChanged(); 
    }
    
    private void OnKartChanged() 
    {
        visuals.gameObject.SetActive(Kart == null);

        if (Kart == null) return;

        AudioManager.PlayAndFollow("itemCollectSFX", transform, AudioManager.MixerTarget.SFX);
        breakParticle.Play();
    }

    public override void FixedUpdateNetwork() 
    {
        base.FixedUpdateNetwork();
        
        if (DisabledTimer.ExpiredOrNotRunning(Runner) && Kart != null) 
        {
            Kart = null;
        }
    }

    private int GetRandomPowerup() 
    {
        var powerUps = ResourceManager.Instance.powerups;
        var seed = Runner.Tick;
        Random.InitState(seed);
        return Random.Range(0, powerUps.Length);
    }
}