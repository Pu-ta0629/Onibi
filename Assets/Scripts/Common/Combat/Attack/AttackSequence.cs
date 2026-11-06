using UnityEngine;

/// <summary>
/// 攻撃1回分の進行（溜め→有効→硬直→クールタイム）を、時間で進める純粋なC#クラス
/// 時間の区切りはAttackTimelineが持つ。ここは、経過時間を進めて、いまの段階を答える
/// </summary>
public class AttackSequence
{
    private AttackTimeline _timeline;
    private float _elapsed;
    private bool _isRunning;

    /// <summary>攻撃が進行中か（クールタイムの間も、trueのまま）</summary>
    public bool isRunning => _isRunning;

    /// <summary>いまの段階。進行中でなければReady（次の攻撃を始められる）</summary>
    public AttackPhase phase => _isRunning ? _timeline.PhaseAt(_elapsed) : AttackPhase.Ready;

    /// <summary>
    /// 攻撃を始める
    /// </summary>
    public void Begin(AttackTimeline timeline)
    {
        _timeline = timeline;
        _elapsed = 0f;
        _isRunning = true;
    }

    /// <summary>
    /// 毎フレーム呼ぶ。全体の長さを超えたら、攻撃の進行を終える
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!_isRunning)
        {
            return;
        }

        _elapsed += deltaTime;
        if (_elapsed >= _timeline.Total)
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// 進行を捨てて、Readyに戻す
    /// </summary>
    public void Reset()
    {
        _isRunning = false;
        _elapsed = 0f;
    }
}
