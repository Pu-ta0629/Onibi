using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlacementData
{
    [SerializeField] private Vector3 _position;
    [SerializeField] private float _rotationY;

    public Vector3 Position => _position;
    public float RotationY => _rotationY;

    public const string PositionField = nameof(_position);
    public const string RotationYField = nameof(_rotationY);
}

[System.Serializable]
public class GoalPlacement : PlacementData
{
    [SerializeField] private float _radius = 1f;

    public float Radius => _radius;

    public const string RadiusField = nameof(_radius);
}

[System.Serializable]
public class EnemyPlacement : PlacementData
{
    [SerializeField] private int _enemyId;

    public int EnemyId => _enemyId;

    public const string EnemyIdField = nameof(_enemyId);
}

[System.Serializable]
public class ItemPlacement : PlacementData
{
    [SerializeField] int _itemId;

    public int ItemId => _itemId;

    public const string ItemIdField = nameof(_itemId);
}


[CreateAssetMenu(menuName = "Scriptable Objects/StageData")]
public class StageData : ScriptableObject
{
    [SerializeField] private int _stageId;
    [SerializeField] private string _stageName;

    [SerializeField] PlacementData _start;
    [SerializeField] List<GoalPlacement> _goals = new();
    [SerializeField] List<EnemyPlacement> _enemies = new();
    [SerializeField] List<ItemPlacement> _items = new();
    [SerializeField] WalkableGrid _walkableGrid = new();

    public int StageId => _stageId;
    public string StageName => _stageName;

    public PlacementData Start => _start;
    public IReadOnlyList<GoalPlacement> Goals => _goals;
    public IReadOnlyList<EnemyPlacement> Enemies => _enemies;
    public IReadOnlyList<ItemPlacement> Items => _items;

    public WalkableGrid WalkableGrid => _walkableGrid;

    public const string StageIdField = nameof(_stageId);
    public const string StageNameField = nameof(_stageName);
    public const string StartField = nameof(_start);
    public const string GoalsField = nameof(_goals);
    public const string EnemiesField = nameof(_enemies);
    public const string ItemsField = nameof(_items);
    public const string WalkableGridField = nameof(_walkableGrid);


}
