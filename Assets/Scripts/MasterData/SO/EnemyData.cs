using JetBrains.Annotations;
using UnityEngine;

[System.Serializable]
public class EnemyMoveConfig
{
    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使用")]
    public LayerMask GroundMask;

    [Tooltip("目的地の近くにあるNavMeshを探す距離")]
    [Range(0.1f, 10f)] public float NavMeshSampleDistance = 2f;

    [Tooltip("追従対象がこの距離以上動いたら経路を更新する")]
    [Range(0.05f, 5f)] public float RepathDistance = 0.5f;

    [Tooltip("対象にこの距離まで近づいたら停止して、攻撃を始める（攻撃のコライダーは別途調整する）")]
    [Range(0.1f, 20f)] public float StopDistance = 1.5f;
}

// 攻撃パターン1つ分(敵が、Attack1 と Attack2 の2つを持つ)
[System.Serializable]
public class EnemyAttack
{
    [Tooltip("AttackPattern(攻撃の当たり判定を選ぶためのキー。コライダーの一覧にある、Key と同じ名前にする)")]
    [SerializeField] private string _pattern;

    [Tooltip("Damage(基礎攻撃力に掛ける倍率)")]
    [SerializeField] private int _damageRate;

    [Tooltip("AttackChargeTime(攻撃溜め時間。単位秒)")]
    [SerializeField] private float _chargeTime;

    [Tooltip("有効時間(溜め終了から、攻撃のコライダーを判定し続ける時間。単位秒)")]
    [SerializeField] private float _activeTime = 0.2f;

    [Tooltip("AttackCoolTime(攻撃クールタイム。単位秒)")]
    [SerializeField] private float _cooldown;

    [Tooltip("硬直時間(攻撃後硬直時間。単位秒)")]
    [SerializeField] private float _recoveryTime;

    public string Pattern => _pattern;
    public int DamageRate => _damageRate;
    public float ChargeTime => _chargeTime;
    public float ActiveTime => _activeTime;
    public float Cooldown => _cooldown;
    public float RecoveryTime => _recoveryTime;

    // 攻撃の当たり判定の名前(Pattern と同じ。コードで読むときに、意味が分かりやすいように別名にしてある)
    public string ColliderKey => _pattern;
}

[CreateAssetMenu(menuName = "Game/MasterData/Enemy", fileName = "Enemy_New")]
public class EnemyData : MasterData
{
    [Tooltip("名前(ステージエディタ表示用)")]
    [SerializeField] private string _displayName;

    [Tooltip("種族")]
    [SerializeField] private EnemyTypeData _type;

    [Tooltip("画像")]
    [SerializeField] private Sprite _sprite;

    [SerializeField] private string _behaviourTypeName;

    [Tooltip("HP")]
    [SerializeField] private int _maxHP;

    [Tooltip("基礎攻撃力")]
    [SerializeField] private int _baseAttack;

    [Tooltip("移動速度")]
    [SerializeField] private float _moveSpeed;

    [Header("行動と索敵")]
    [Tooltip("行動Logic(名前。実装を選ぶためのキー)")]
    [SerializeField] private string _moveLogic;

    [Tooltip("索敵タイプ(名前。実装を選ぶためのキー)")]
    [SerializeField] private string _searchType;

    [Tooltip("索敵範囲")]
    [SerializeField] private float _searchRange;

    [Tooltip("戦闘ロジック(名前。実装を選ぶためのキー)")]
    [SerializeField] private string _battleLogic;

    [Header("移動の設定値")]
    [Tooltip("地面のLayer、NavMeshの検索距離、経路の更新距離、攻撃を始める距離")]
    [SerializeField] private EnemyMoveConfig _moveConfig = new();

    [Header("コライダー")]
    [Tooltip("体(Hit)、攻撃(Attack)、索敵(Search)など。役割(Role)で、使い道が決まる")]
    [SerializeField] private ColliderSet _colliders = new();

    [Header("攻撃")]
    [SerializeField] private EnemyAttack _attack1 = new();
    [SerializeField] private EnemyAttack _attack2 = new();

    public string DisplayName => _displayName;
    public EnemyTypeData Type => _type;
    public Sprite Sprite => _sprite;
    public int MaxHP => _maxHP;
    public int BaseAttack => _baseAttack;
    public float MoveSpeed => _moveSpeed;
    public string MoveLogic => _moveLogic;
    public string SearchType => _searchType;
    public float SearchRange => _searchRange;
    public string BattleLogic => _battleLogic;
    public EnemyMoveConfig MoveConfig => _moveConfig;
    public ColliderSet Colliders => _colliders;
    public EnemyAttack Attack1 => _attack1;
    public EnemyAttack Attack2 => _attack2;

    public MoveType MoveType => _type != null ? _type.moveType : MoveType.Walk;

    public int CalDamage(EnemyAttack attack) => _baseAttack * attack.DamageRate;
}