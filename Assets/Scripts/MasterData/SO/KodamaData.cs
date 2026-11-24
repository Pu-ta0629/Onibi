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

    [Header("入手量")]
    [Tooltip("取得したときに、迷子が受け取る量の範囲(最小～最大。両方を含む)")]
    [SerializeField, Min(1)] private int _minAmount = 1;
    [SerializeField, Min(1)] private int _maxAmount = 1;

    [Header("見た目")]
    [Tooltip("木霊のSprite")]
    [SerializeField] private GameObject _prefab;

    

    [Header("コライダー")]
    [Tooltip("接触の判定を1つ以上作る。対象はLostChildだけにする。Hitは不要")]
    [SerializeField] private ColliderSet _colliders = new();

    public string DisplayName => _displayName;
    public bool IsSpecial => _isSpecial;
    public SpecialKodamaKind SpecialKind => _specialKind;

    public int minAmount => _minAmount;
    public int maxAmount => Mathf.Max(_minAmount, _maxAmount);
    public GameObject Prefab => _prefab;
    public ColliderSet Colliders => _colliders;

    ///<summary>取得した時の入手量を、範囲の中から決める</summary>
    public int RollAmount()
    {
        return Random.Range(_minAmount, maxAmount +1);
    }

    ///<summary>接触の判定の、中心から端までの最大距離。木霊同士の感覚に使う</summary>
    public float TouchRadius
    {
        get
        {
            float max = 0f;
            foreach(var def in _colliders.All)
            {
                if(def == null || def.Role != ColliderRole.Search)
                {
                    continue;
                }

                max = Mathf.Max(max, def.Offset.magnitude + def.Shape.BoundingRadius);
            }

            return max;
        }
    }
}
