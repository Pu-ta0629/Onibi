using UnityEngine;

/// <summary>
/// 木霊のマスターデータ。動かない。迷子が触れると取得されて、消える
/// 通常の木霊、isSpecialがOFFのデータを1つだけ作る。特殊木霊は、種類ごとに1つずつ作る
/// </summary>
[CreateAssetMenu(menuName ="Game/MasterData/Kodama",fileName ="Kodama_New")]
public class KodamaData : MasterData
{
    [Tooltip("名前")]
    [SerializeField] private string _displayName;

    [Header("種類")]
    [Tooltip("特殊木霊か。ONのとき、StageDataの特殊木霊の種類から、このデータが選ばれる。OFFが通常の木霊")]
    [SerializeField] private bool _isSpecial;

    [Tooltip("特殊木霊の種類(StageDataの特殊木霊と同じ種類)。")]
    [SerializeField] private SpecialKodamaKind _specialKind = 0;

    [Header("見た目")]
    [Tooltip("木霊のSprite")]
    [SerializeField] private GameObject _prefab;

    [Header("コライダー")]
    [Tooltip("接触の判定を1つ以上作る。対象はLostChildだけにする。Hitは不要")]
    [SerializeField] private ColliderSet _colliders = new();

    public string DisplayName => _displayName;
    public bool IsSpecial => _isSpecial;
    public SpecialKodamaKind SpecialKind => _specialKind;
    public GameObject Prefab => _prefab;
    public ColliderSet Colliders => _colliders;
}
