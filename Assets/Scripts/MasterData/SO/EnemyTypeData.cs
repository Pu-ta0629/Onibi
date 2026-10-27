using UnityEngine;

[CreateAssetMenu(menuName ="Game/MasterData/EnemyType",fileName ="EnemyType_New")]
public class EnemyTypeData : MasterData
{
    [Tooltip("種類名")]
    [SerializeField] string _displayName;

    [Tooltip("移動領域(Walk=陸のみ/Swim=泳げる/Fly=飛べる)")]
    [SerializeField] MoveType _moveType;

    public string DisplayName => _displayName;
    public MoveType moveType => _moveType;
}
