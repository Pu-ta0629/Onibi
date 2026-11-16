using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火の第１スキル（近距離攻撃＝突進）をまとめた純粋なC#クラス
/// 体の位置の更新・索敵・突進の開始と進行・木霊の消費・クールタイムを担当する
/// 攻撃対象は、迷子の索敵結果と火自身の索敵結果を合わせた中で、一番重みが大きい敵
/// 移動はMoverに任せる。FireControllerが生成して、毎フレームTickを呼ぶ
/// </summary>
public class FireDashAttack : IDisposable
{
    private readonly List<HitInfo> _hits = new List<HitInfo>();
    private readonly AttackProcessor _attackProcessor = new AttackProcessor();
    private readonly Transform _transform;
    private readonly FireStateData _data;
    private readonly Mover _mover;
    private readonly KodamaStock _kodama;
    private readonly HitDetector _hit;
    private readonly FireTargetSearch _targetSearch;

  //  private readonly AttackHit _burnHit;

    private float _cooldown;      //次の突進までの残り時間（秒）
    private float _facingYaw;     //突進の向き（度）。攻撃のコライダーの向きに使う
    private bool _isDashing;

    /// <summary>突進中か（突進中は、火は周回しない）</summary>
    public bool isDashing => _isDashing;

    /// <param name="owner">当たり判定の持ち主（FireController）</param>
    /// <param name="transform">火のTransform</param>
    /// <param name="data">この火のマスターデータ</param>
    /// <param name="mover">突進の移動に使うMover</param>
    /// <param name="world">全員で共有するHitWorld</param>
    /// <param name="lostChild">索敵結果を借りる迷子</param>
    /// <param name="kodama">突進で消費する木霊</param>
    public FireDashAttack(object owner, Transform transform, FireStateData data, Mover mover, HitWorld world, LostChildController lostChild, KodamaStock kodama/*,FireColor color*/)
    {
        _transform = transform;
        _data = data;
        _mover = mover;
        _kodama = kodama;

        _hit = new HitDetector(world, owner, Faction.Fire, data.Colliders);
        _targetSearch = new FireTargetSearch(new WeightedSearch<EnemyController>(_hit), lostChild);

      //  _burnHit = new AttackHit(owner, new BurnApplyEffect(color, data.BurnCount, data.BurnPotency));
    }

    #region Public
    /// <summary>
    /// 毎フレーム呼ぶ。索敵・突進の開始と進行を行い、突進中ならtrueを返す
    /// </summary>
    public bool Tick(FireAIPermission permission)
    {
        _hit.SetTransform(_transform.position, _facingYaw);
        _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

        if (_isDashing)
        {
            TickDash(permission);
            return _isDashing;
        }

        if (permission.CanSearch)
        {
            _targetSearch.Tick(_transform.position);
        }

        if (permission.CanDash && _cooldown <= 0f)
        {
            TryBeginDash();
        }

        return _isDashing;
    }

    /// <summary>
    /// 当たり判定を外す（火が消えるときに必ず呼ぶ）
    /// </summary>
    public void Dispose()
    {
        _hit.Dispose();
    }
    #endregion

    #region Private
    //攻撃対象がいて、木霊が足りるなら、突進を始める
    private void TryBeginDash()
    {
        if (!_targetSearch.TryGetTarget(out SearchResult target))
        {
            return;
        }

        if (!_kodama.TryConsume(_data.NormalCost))
        {
            return;
        }

        Vector3 direction = target.Position - _transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 1e-6f)
        {
            _facingYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        _mover.BeginStraightTo(target.Position);   //突進の目的地は、始めた瞬間の敵の位置
        _attackProcessor.Begin();
        _isDashing = true;
    }

    //目的地へ直進しながら、攻撃のコライダーで敵に当たったか調べる。着いたら終わる
    private void TickDash(FireAIPermission permission)
    {
        if (!permission.CanDash)
        {
            EndDash();
            return;
        }

        bool isMoving = _mover.MoveStraightToTarget();
        _hit.SetTransform(_transform.position, _facingYaw);

       /* if (_hit.Detect<EnemyController>(_data.NormalAttackKey, _hits))
        {
            _attackProcessor.Apply(_hits, _burnHit);
        }*/

        if (!isMoving)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _cooldown = _data.NormalCooldown;
    }
    #endregion
}
