using UnityEngine;

// 迷い子(警護対象)。いまは、名前とコライダーだけを持つ。HP、移動速度などは、項目が決まったら足す
[CreateAssetMenu(menuName = "Game/MasterData/LostChilde", fileName = "Childe_new")]
public class LostChilde : MasterData
{
    [Tooltip("名前")]
    [SerializeField] private string _displayName;

    [Header("コライダー")]
    [Tooltip("体(Hit)、攻撃(Attack)、索敵(Search)など。役割(Role)で、使い道が決まる")]
    [SerializeField] private ColliderSet _colliders = new();

    public string DisplayName => _displayName;
    public ColliderSet Colliders => _colliders;
}
