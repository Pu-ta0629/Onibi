using System;
using UnityEngine;

/// <summary>
/// 焚き火。火の生成場所に置かれ、火に攻撃されて倒されると、火が付く（OnBurned）
/// 判定の陣営は敵（Faction.Enemy）。IFireTargetを実装するので、火の索敵・攻撃の対象になる
/// 倒れたら、躰の判定を外して非表示にする（以降、索敵・攻撃の対象にならない）
/// 火の獲得は、通知を受けたFireWoodMawnagerが行う。
/// </summary>
public class FireWood : MonoBehaviour, IHittable, IHitTarget, IFireTarget
{
    private const int MaxHp = 1;

    /// <summary>火が付いた（倒れた）時に、一度だけ通知する</summary>
    public event Action<FireWood> OnBurned;

    private HitDetector _hit;
    private Health _health;
    private FireColor _color;
    private FireKind _kind;
    private Vector3 _position;
    private bool _isInitialized;

    /// <summary>この焚き木から獲得する火の色（StageDataのFiresで指定）</summary>
    public FireColor Color => _color;
    /// <summary>この焚き木から獲得する火の形（StageDataのFiresで指定）</summary>
    public FireKind Kind => _kind;

    /// <summary>焚き木の位置（火が生成される位置）</summary>
    public Vector3 Position => _position;

    public bool isBurned => _health == null || _health.isDead;

    /// <summary>IHitTarget:　火傷は効かない</summary>
    public BurnState Burn => null;

    #region Initialize
    public void Initialize(HitWorld world, ColliderSet colliders, FireColor color, FireKind kind, Vector3 position)
    {
        if(world == null | colliders == null || color == FireColor.None)
        {
            Debug.Log($"[FireWood] Initialize : Reference is Null!!! ");
            return;
        }

        _color = color;
        _kind = kind;
        _position = position;

        _hit = new HitDetector(world, this, Faction.Enemy, colliders);
        _hit.SetTransform(position, 0f);

        _health = new Health(MaxHp);
        _health.OnDead += HandleDead;
        _isInitialized = true;
    }
    #endregion

    #region UnityLifeCycle
    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDead -= HandleDead;
        }
        _hit.Dispose();
    }
    #endregion

    #region Public
    /// <summary>
    /// Ihittable: 命中情報を受け取り、効果御w順に適応する
    /// </summary>
    public void OnHit(AttackHit hit)
    {
        if (!_isInitialized || isBurned) return;
        hit.ApplyTo(this);
    }
    public void TakeDamage(int damage)
    {
        if (_isInitialized || isBurned) return;
        _health.TakeDamage(damage);
    }
    #endregion
    #region Private
    private void HandleDead()
    {
        _hit.SetActive(false); //以降、索敵・攻撃対象にならない
        gameObject.SetActive(false);
        OnBurned?.Invoke(this);
    }
    #endregion
}
