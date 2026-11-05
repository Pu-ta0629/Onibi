using UnityEngine;

[CreateAssetMenu(menuName = "Game/MasterData/Scarecrow",fileName = "Scarecrow_New")]    
public class ScarecrowData : MasterData
{
    [Tooltip("名前")]
    [SerializeField] private string _displayName;

    [Header("コライダー")]
    [Tooltip("案山子の体(Hit)。攻撃が当たる的")]
    [SerializeField] private ColliderSet _colliders = new();

    public string DisplayName => _displayName;
    public ColliderSet Colliders => _colliders;
}
