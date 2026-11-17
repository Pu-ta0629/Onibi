using UnityEngine;

[CreateAssetMenu(menuName = "Game/MasterData/FireState", fileName = "FireState_New")]
public class FireStateData : MasterData
{
    // Unit(Fire)の移動プロファイルは、全員共通の固定値(飛べる、最小の大きさ)
    public static readonly MovementProfile Fire = new(MoveType.Fly, SizeClass.Small, Faction.Fire);

    [Tooltip("種類(火の状態の名前)")]
    [SerializeField] private FireKind _fireKind = FireKind.Tanebi;

    [Tooltip("ランク(火のレベル)")]
    [SerializeField] private int _rank;

    [Tooltip("移動速度")]
    [SerializeField] private float _moveSpeed;

    [Tooltip("マウス追従速度(ラジアルで選択中の火が、マウスを追う速さ)。0以下なら、移動速度と同じになる")]
    [SerializeField] private float _followSpeed;

    [Tooltip("明るさ(自身を中心に照らせる範囲。(半径))")]
    [SerializeField] private float _lightRadius;

    [Header("移動")]
    [Tooltip("目的地の近くのNavMeshを探す距離。地面の高さがずれていても届くよう、大きめにする")]
    [SerializeField, Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Tooltip("目的地がこの距離以上動いたら、経路を更新する(経路計算の回数を減らす)")]
    [SerializeField, Range(0.05f, 2f)] private float _destinationUpdateDistance = 0.3f;

    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使う")]
    [SerializeField] private LayerMask _groundMask;

    [Header("周回")]
    [Tooltip("迷子を中心に周回する円の半径")]
    [SerializeField, Range(0.5f, 20f)] private float _orbitRadius = 3f;

    [Tooltip("周回の速さ(度/秒)。移動速度より速く回ることはできない")]
    [SerializeField, Range(10f, 180f)] private float _orbitSpeed = 60f;

    [Header("見た目")]
    [Tooltip("この状態になったときの最初の明るさレベル。各レベルの効果はLightManagerで共通に設定する")]
    [SerializeField] private FireBrightness _initialBrightness = FireBrightness.Level2;

    [Tooltip("この状態のEffect。色はFireEffectCatalogのGradientで、火の色に合わせて着色される")]
    [SerializeField] private ParticleSystem _effectPrefab;

    [Header("この状態になる条件(進化・退化の判定に使う)")]
    [Tooltip("木霊数(この状態になるために必要な現在の木霊量。これ未満になると、下の状態へ退化する)")]
    [SerializeField] private int _kodamaCount;

    [Tooltip("必要な特殊木霊(None = 不要)。狐火・隠火・鬼火だけ設定する")]
    [SerializeField] private SpecialKodamaKind _requiredSpecial = SpecialKodamaKind.None;

    [Header("攻撃")]
    [Tooltip("消費量(通常攻撃時に消費する木霊の量。突進攻撃)")]
    [SerializeField] private int _normalCost;

    [Tooltip("特殊消費量(固有行動時に消費する木霊の量。)")]
    [SerializeField] private int _specialCost;

    [Tooltip("クールタイム1(通常攻撃をするまでの時間。秒)")]
    [SerializeField] private float _normalCooldown;

    [Tooltip("クールタイム2(固有行動をするまでの時間。秒)")]
    [SerializeField] private float _specialCooldown;

    [Header("火傷")]
    [Tooltip("火傷威力(攻撃時に与える火傷威力)")]
    [SerializeField] private int _burnPotency;

    [Tooltip("火傷回数(攻撃時に与える火傷回数)")]
    [SerializeField] private int _burnCount;

    [Header("コライダー")]
    [Tooltip("体(Hit)、攻撃(Attack)、索敵(Search)など。役割(Role)で、使い道が決まる")]
    [SerializeField] private ColliderSet _colliders = new();

    [Tooltip("通常攻撃の当たり判定の名前(コライダーの一覧にある、Key)")]
    [SerializeField] private string _normalAttackKey = "normal";

    [Tooltip("固有行動の当たり判定の名前(コライダーの一覧にある、Key)")]
    [SerializeField] private string _specialAttackKey = "special";

    public FireKind FireKind => _fireKind;
    public int Rank => _rank;
    public float MoveSpeed => _moveSpeed;
    public float FollowSpeed => _followSpeed > 0f ? _followSpeed : _moveSpeed;
    public float LightRadius => _lightRadius;
    public float NavMeshSampleDistance => _navMeshSampleDistance;
    public float DestinationUpdateDistance => _destinationUpdateDistance;
    public LayerMask GroundMask => _groundMask;
    public float OrbitRadius => _orbitRadius;
    public float OrbitSpeed => _orbitSpeed;
    public FireBrightness InitialBrightness => _initialBrightness;
    public ParticleSystem EffectPrefab => _effectPrefab;
    public int KodamaCount => _kodamaCount;
    public SpecialKodamaKind RequiredSpecial => _requiredSpecial;
    public int NormalCost => _normalCost;
    public int SpecialCost => _specialCost;
    public float NormalCooldown => _normalCooldown;
    public float SpecialCooldown => _specialCooldown;
    public int BurnPotency => _burnPotency;
    public int BurnCount => _burnCount;
    public ColliderSet Colliders => _colliders;
    public string NormalAttackKey => _normalAttackKey;
    public string SpecialAttackKey => _specialAttackKey;
}