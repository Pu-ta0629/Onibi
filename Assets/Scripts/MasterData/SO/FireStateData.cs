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

    [Tooltip("明るさ(自身を中心に照らせる範囲。(半径))")]
    [SerializeField] private float _lightRadius;

    [Header("この状態になる条件")]
    [Tooltip("木霊数(この状態になるために必要な木霊数)")]
    [SerializeField] private int _kodamaCount;

    [Tooltip("木霊の種類(この状態になるために必要な木霊の種類)")]
    [SerializeField] private string _kodamaType;

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
    public float LightRadius => _lightRadius;
    public int KodamaCount => _kodamaCount;
    public string KodamaType => _kodamaType;
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
