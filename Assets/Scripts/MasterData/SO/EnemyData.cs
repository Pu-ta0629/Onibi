using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public class EnemyAttack
{
    [Tooltip("AttackPattern(攻撃範囲の形状を選ぶためのキー)")]
    [SerializeField] private string _pattern;

    [Tooltip("Damage(基礎攻撃力に掛ける倍率)")]
    [SerializeField] private int _damageRate;

    [Tooltip("攻撃の当たり判定(AttackRange。形、範囲、手前の当たらない範囲、位置ずれ)")]
    [SerializeField] private AttackCollider _collider = new();

    [Tooltip("AttackChargeTime(攻撃溜め時間。単位秒)")]
    [SerializeField] private float _chargeTime;

    [Tooltip("AttackCoolTime(攻撃クールタイム。単位秒)")]
    [SerializeField] private float _cooldown;

    [Tooltip("硬直時間(攻撃後硬直時間。単位秒)")]
    [SerializeField] private float _recoveryTime;

    public string Pattern => _pattern;
    public int DamageRate => _damageRate;
    public AttackCollider collider => _collider;

    public float Range => _collider.Shape.Range;
    public float ChargeTime => _chargeTime;
    public float Cooldown => _cooldown;
    public float RecoveryTime => _recoveryTime;

}

[CreateAssetMenu (menuName="Game/MasterData/Enemy",fileName ="Enemy_New")]
public class EnemyData : MasterData
{
    [Tooltip("名前(ステージエディタ表示用)")]
    [SerializeField] string _displayName;

    [Tooltip("種族")]
    [SerializeField] EnemyTypeData _type;

    [Tooltip("HP")]
    [SerializeField] int _maxHP;

    [Tooltip("基礎攻撃力")]
    [SerializeField] int _baseAttack;

    [Tooltip("移動速度")]
    [SerializeField] float _moveSpeed;

    [Header("行動と索敵")]
    [Tooltip("行動Logic(名前。実装を選ぶためのキー)")]
    [SerializeField] string _moveLogic;

    [Tooltip("索敵タイプ（名前。実装を選ぶためのキー）")]
    [SerializeField] string _searchType;

    [Tooltip("索敵範囲")]
    [SerializeField] float _searchRange;

    [Tooltip("戦闘ロジック(名前。実装を選ぶためのキー)")]
    [SerializeField] string _battleLogic;

    [Header("当たり判定")]
    [Tooltip("当てられる側の体")]
    [SerializeField] HitCollider _hitCollider = new();

    [Header("攻撃")]
    [SerializeField] EnemyAttack _attack1 = new();
    [SerializeField] EnemyAttack _attack2 = new();

    public string DisplayName => _displayName;
    public EnemyTypeData Type => _type;
    public int MaxHP => _maxHP;
    public int BaseAttack => _baseAttack;
    public float MoveSpeed => _moveSpeed;
    public string MoveLogic => _moveLogic;
    public string SearchType => _searchType;
    public float SearchRange => _searchRange;
    public string BattleLogic => _battleLogic;
    public EnemyAttack Attack1 => _attack1;
    public EnemyAttack Attack2 => _attack2;

    public HitCollider HitCollider => _hitCollider;

    public MoveType MoveType => _type != null ? _type.moveType : MoveType.Walk;

    public int CalDamage(EnemyAttack attack) => _baseAttack * attack.DamageRate;
}
