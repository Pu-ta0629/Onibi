using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[CreateAssetMenu(menuName ="Game/MasterData/FireState",fileName = "FireState_New")]
public class FireStateData :MasterData
{
    public static readonly MovementProfile UnitProfile = new(MoveType.Fly, SizeClass.Small, Faction.Fire);

    [Tooltip("種類(火の状態の名前)")]
    [SerializeField] private string _fireState;

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
    [SerializeField] string _kodamaType;

    [Header("攻撃")]
    [Tooltip("消費量(通常攻撃時に消費する木霊の量。突進攻撃)")]
    [SerializeField] int _normalCost;

    [Tooltip("特殊消費量(固有行動時に消費する木霊の量。)")]
    [SerializeField] int _specialCost;

    [Tooltip("クールタイム1(通常攻撃をするまでの時間。秒)")]
    [SerializeField] float _normalCooldown;

    [Tooltip("クールタイム2(固有行動をするまでの時間。秒)")]
    [SerializeField] float _specialCooldown;

    [Header("火傷")]
    [Tooltip("火傷威力(攻撃時に与える火傷威力)")]
    [SerializeField] int _burnPotency;

    [Tooltip("火傷回数(攻撃時に与える火傷回数)")]
    [SerializeField] int _burnCount;

    public string FireState => _fireState;
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
}
