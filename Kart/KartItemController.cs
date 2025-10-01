using Fusion;
using UnityEngine;

public class KartItemController : KartComponent 
{
    public float equipItemTimeout = 3f;
    public float useItemTimeout = 2.5f;

    [Networked] public TickTimer SecondaryEquipCooldown { get; set; }
    [Networked] public TickTimer TertiaryEquipCooldown { get; set; }
    [Networked] public TickTimer QuaternaryEquipCooldown { get; set; }
    
    // 호환성을 위한 프로퍼티
    [Networked] public TickTimer EquipCooldown { get; set; }
    
    public bool CanUseSecondaryItem => Kart.SecondaryItemIndex != -1 && 
                                       SecondaryEquipCooldown.ExpiredOrNotRunning(Runner);
    
    public bool CanUseTertiaryItem => Kart.TertiaryItemIndex != -1 && 
                                      TertiaryEquipCooldown.ExpiredOrNotRunning(Runner);
    
    public bool CanUseQuaternaryItem => Kart.QuaternaryItemIndex != -1 && 
                                        QuaternaryEquipCooldown.ExpiredOrNotRunning(Runner);
    
    // 호환성
    public bool CanUseItem => CanUseSecondaryItem;

    public override void OnEquipItem(Powerup powerup, float timeUntilCanUse, int slotIndex = 0) 
    {
        base.OnEquipItem(powerup, timeUntilCanUse, slotIndex);

        switch (slotIndex)
        {
            case 1: // 슬롯 2
                SecondaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
            case 2: // 슬롯 3
                TertiaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
            case 3: // 슬롯 4
                QuaternaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
            default:
                // 호환성을 위한 기본 처리
                EquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
        }
    }

    // Shift 키 - 순차적으로 사용 가능한 아이템 사용
    public void UseNextAvailableItem() 
    {
        if (CanUseSecondaryItem) 
        {
            UseSecondaryItem();
            return;
        }
        
        if (CanUseTertiaryItem) 
        {
            UseTertiaryItem();
            return;
        }
        
        if (CanUseQuaternaryItem) 
        {
            UseQuaternaryItem();
            return;
        }
        
        // 사용할 아이템이 없으면 경적
        if (Runner.IsForward) 
        {
            Kart.Audio.PlayHorn();
        }
    }

    public void UseSecondaryItem() 
    {
        if (!CanUseSecondaryItem) 
        {
            if (Runner.IsForward) Kart.Audio.PlayHorn();
            return;
        }
        
        var item = Kart.SecondaryItem;
        if (item != null) 
        {
            item.Use(Runner, Kart);
            Kart.SecondaryItemIndex = -1;
        }
    }

    public void UseTertiaryItem() 
    {
        if (!CanUseTertiaryItem) 
        {
            if (Runner.IsForward) Kart.Audio.PlayHorn();
            return;
        }
        
        var item = Kart.TertiaryItem;
        if (item != null) 
        {
            item.Use(Runner, Kart);
            Kart.TertiaryItemIndex = -1;
        }
    }
    
    public void UseQuaternaryItem() 
    {
        if (!CanUseQuaternaryItem) 
        {
            if (Runner.IsForward) Kart.Audio.PlayHorn();
            return;
        }
        
        var item = Kart.QuaternaryItem;
        if (item != null) 
        {
            item.Use(Runner, Kart);
            Kart.QuaternaryItemIndex = -1;
        }
    }

    // 호환성 메서드
    public void UseItem() 
    {
        UseNextAvailableItem();
    }
}