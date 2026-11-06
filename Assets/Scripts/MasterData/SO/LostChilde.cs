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

    public string DisplayName => _displayName;
    public ColliderSet Colliders => _colliders;
    public MovementProfile MovementProfile => _movementProfile;
}
