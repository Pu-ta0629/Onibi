using UnityEngine;

public enum AttackPhase : byte
{
    Ready = 0,
    Charge = 1,
    Active = 2,
    Recovery = 3,
    Cooldown = 4,
}

public readonly struct AttackTimeline
{
    private readonly float _chargeTime;
    private readonly float _activeTime;
    private readonly float _recoveryTime;
    private readonly float _cooldown;

    public AttackTimeline(float chargeTime,float activeTime,float recoveryTime,float cooldown)
    {
        _chargeTime = Mathf.Max(0f,chargeTime);
        _activeTime = Mathf.Max(0f,activeTime);
        _recoveryTime = Mathf.Max(0f,recoveryTime);
        _cooldown = Mathf.Max(0,cooldown);
    }

    public static AttackTimeline From(EnemyAttack attack)
    {
        return new AttackTimeline(attack.ChargeTime, attack.ActiveTime, attack.RecoveryTime, attack.Cooldown);
    }

    public float ChargeEnd => _chargeTime;
    public float ActiveEnd => _chargeTime + _activeTime;
    public float RecoveryEnd => _chargeTime + _activeTime + _recoveryTime;
    public float CooldownEnd => _chargeTime + _activeTime + _recoveryTime + _cooldown;

    // 攻撃を始めてから、次の攻撃を始められるまでの、全体の長さ
    public float Total => CooldownEnd;

    public AttackPhase PhaseAt(float time)
    {
        if (time < 0f) return AttackPhase.Ready;
        if (time < ChargeEnd) return AttackPhase.Charge;
        if (time < ActiveEnd) return AttackPhase.Active;
        if (time < RecoveryEnd) return AttackPhase.Recovery;
        if (time < CooldownEnd) return AttackPhase.Cooldown;
        return AttackPhase.Ready;
    }

    //攻撃のコライダーを、判定する時刻か
    public bool IsColliderActive(float time) => PhaseAt(time) == AttackPhase.Active;

    //ある段階の、開始と終了の時刻(時間の帯の表示に従う)
    public bool TryGetRange(AttackPhase phase,out float start,out float end)
    {
        switch(phase)
        {
            case AttackPhase.Charge:
                start = 0f;
                end = 0f;
                break;
            case AttackPhase.Active:
                start = ChargeEnd;
                end = ActiveEnd;
                break;
            case AttackPhase.Recovery:
                start = ActiveEnd;
                end = RecoveryEnd;
                break;
            case AttackPhase.Cooldown:
                start = RecoveryEnd;
                end = CooldownEnd;
                break;
            default:
                start = 0f;
                end = 0f;
                break;
        }
        return end > start;
    }
}