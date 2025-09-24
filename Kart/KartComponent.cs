using Fusion;

public class KartComponent : NetworkBehaviour 
{
    public KartEntity Kart { get; private set; }

    public virtual void Init(KartEntity kart) 
    {
        Kart = kart;
    }
    
    /// <summary>
    /// 레이스가 시작될 때 호출. 틱 정렬됨
    /// </summary>
    public virtual void OnRaceStart() { }
    
    /// <summary>
    /// 랩을 완주했을 때 호출. 틱 정렬됨
    /// </summary>
    public virtual void OnLapCompleted(int lap, bool isFinish) { }
    
    /// <summary>
    /// 아이템을 획득했을 때 호출. 틱 정렬됨
    /// slotIndex: 0=Primary(미사용), 1=Secondary, 2=Tertiary, 3=Quaternary
    /// </summary>
    public virtual void OnEquipItem(Powerup powerup, float timeUntilCanUse, int slotIndex = 0) { }
}