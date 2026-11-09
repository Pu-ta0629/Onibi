using UnityEngine;

// 迷い子(警護対象)。いまは、名前とコライダーだけを持つ。HP、移動速度などは、項目が決まったら足す
[CreateAssetMenu(menuName = "Game/MasterData/Lostchild", fileName = "child_new")]
public class LostChild : MasterData
{
    [Tooltip("名前")]
    [SerializeField] private string _displayName;

    [Header("コライダー")]
    [Tooltip("体(Hit)、攻撃(Attack)、索敵(Search)など。役割(Role)で、使い道が決まる")]
    [SerializeField] private ColliderSet _colliders = new();

    [SerializeField] private MovementProfile _movementProfile;

    [Header("MoveConfig")]
    [Tooltip("移動速度")]
    [SerializeField, Range(0.1f, 20f)] private float _moveSpeed = 5f;
    [Tooltip("目的地の近くのNavMeshを探す距離")]
    [SerializeField, Range(0.1f, 10f)] private float _navMeshSampleDistance = 2f;

    [Header("Ground")]
    [Tooltip("地面のLayer。NavMeshを使わない移動で、地形の高さに合わせるのに使う")]
    [SerializeField] private LayerMask _groundMask;

    [Header("FollowConfig")]
    [Tooltip("追従先がこの距離以上動いたら目的地を更新する")]
    [SerializeField, Range(0.1f, 5f)] private float _repathDistance = 0.5f;
    [Tooltip("追従先にこの距離（水平）まで近づいたら止まる。離れたら再び動く")]
    [SerializeField, Range(0.1f, 20f)] private float _stopDistance = 2f;

    [Header("Combat")]
    [Tooltip("ライフの数。敵の攻撃が1回当たるたびに、1つ減る")]
    [SerializeField, Range(1, 20)] private int _maxLife = 3;

    public string DisplayName => _displayName;
    public ColliderSet Colliders => _colliders;
    public MovementProfile MovementProfile => _movementProfile;
    public float MoveSpeed => _moveSpeed;
    public float NavMsehSampleDistance => _navMeshSampleDistance;
    public LayerMask GroundMask => _groundMask;
    public float RepathDistance => _repathDistance;
    public float StopDistance => _stopDistance;
    public int MaxLife => _maxLife;
}
