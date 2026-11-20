using System;
using UnityEngine;

/// <summary>
/// ステージの進行を管理する純粋なC＃クラス
/// 迷子の中心点が、選ばれたGoalの円に入ったらクリアする
/// クリアしたら StageCleard を一度だけ発火。止める処理は、購読側
/// </summary>
public class StageController
{
    private readonly LostChildController _lostChild;
    private readonly GoalPlacement _goal;
    private StageState _state = StageState.Ready;

    ///<summary>クリアが確定したときに、一度だけ発火する</summary>
    public event Action StageCleared;

    public StageState State => _state;
    public bool IsPlaying => _state == StageState.Playing;

    public StageController(LostChildController lostCild,GoalPlacement goal)
    {
        _lostChild = lostCild;
        _goal = goal;
    }

    #region Public
    ///<summary>ステージの生成が終わった後に、一度だけ呼ぶ。ここから判定を始める</summary>
    public void Begin()
    {
        if(_lostChild == null || _goal == null)
        {
            Debug.LogError("[StageController] Begin : Reference is Null!!!!");
            return;
        }

        _state = StageState.Playing; 
    }

    /// <summary>毎フレーム呼ぶ。Playingの間だけ、Goalに入ったかを調べる</summary>
    public void Tick()
    {
        if(_state != StageState.Playing)
        {
            return;
        }

        if(_lostChild.life != null && _lostChild.life.isDead)
        {
            return;
        }

        //中心点だけで判定
        if(_goal.Contains(_lostChild.transform.position))
        {
            Clear();
        }
    }
    #endregion

    #region Private

    // 状態を変えてから通知する。Playingのときしか入らないので、二十発火しない
    private void Clear()
    {
        _state = StageState.Cleared;

    }
    #endregion
}

public enum StageState
{
    Ready,
    Playing,
    Cleared,
}

