using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
public class GoalPlacement : RadiusPlacement
{
}

[System.Serializable]
public class FirePlacement : PlacementData
{
    [SerializeField] private FireKind _kind;
    [SerializeField] private FireColor _color;

    public FireKind Kind => _kind;
    public FireColor Color => _color;
}

[System.Serializable]
public class EnemyPlacement : PlacementData
{
    [SerializeField] private EnemyData _enemy;

    [SerializeField] private SizeClass _size = SizeClass.Medium;

    public EnemyData Enemy => _enemy;
    public SizeClass Size => _size;

    public const string EnemyField = nameof(_enemy);
    public const string SizeField = nameof(_size);

    public MovementProfile Profile => new(_enemy != null ? _enemy.MoveType : MoveType.Walk, _size, Faction.Enemy);
}

[System.Serializable]
public class RadiusPlacement : PlacementData
{
    [SerializeField] private float _radius = 1f;

    public float Radius => _radius;

    public const string RadiusField = nameof(_radius);
}



[System.Serializable]
public class SpecialKodamaChance
{
    [SerializeField] private SpecialKodamaKind _kind;
    [SerializeField, Range(0f, 1f)] private float _probability = 0.5f;

    public SpecialKodamaKind Kind => _kind;
    public float Probability => _probability;
}


[System.Serializable]
public class SpawnSpotPlacement : RadiusPlacement
{
    [SerializeField] private SpawnKind _kind = SpawnKind.Kodama;

    [SerializeField] private List<EnemyData> _enemyList = new();
    [SerializeField] private SizeClass _enemySize = SizeClass.Medium;

    [SerializeField] private List<SpecialKodamaChance> _specialKodamas = new();

    [SerializeField, Min(0.1f)] private float _spawnInterval = 5f;

    public SpawnKind Kind => _kind;
    public IReadOnlyList<EnemyData> EnemyList => _enemyList;
    public SizeClass EnemySize => _enemySize;
    public IReadOnlyList<SpecialKodamaChance> SpecialKodamas => _specialKodamas;
    public float SpawnInterval => _spawnInterval;

    public const string KindField = nameof(_kind);
    public const string EnemyListField = nameof(_enemyList);
    public const string EnemySizeField = nameof(_enemySize);
    public const string SpecialKodamasField = nameof(_specialKodamas);
    public const string SpawnIntervalField = nameof(_spawnInterval);

    public bool Contains(Vector3 point)
    {
        float dx = point.x - Position.x;
        float dz = point.z - Position.z;
        return dx * dx + dz * dz <= Radius * Radius; 
    }

    public IEnumerable<MovementProfile> EnemyProfiles
    {
        get
        {
            foreach(EnemyData enemy in _enemyList)
            {
                if (enemy != null) yield return new MovementProfile(enemy.MoveType, _enemySize, Faction.Enemy);
            }
        }
    }

    // 確率に従って、特殊木霊の種類を選ぶ。rollは、0以上1未満の乱数
    // 確率の合計が1でなくても、合計に対する割合で選ぶ
    public bool TryPickSpecialKodama(float roll,out SpecialKodamaKind result)
    {
        result = default;

        float total = 0f;
        foreach (SpecialKodamaChance chance in _specialKodamas) total += chance.Probability;
        if (total <= 0f) return false;

        float target = roll * total;
        float sum = 0f;
        foreach(SpecialKodamaChance chance in _specialKodamas)
        {
            sum += chance.Probability;
            if(target < sum)
            {
                result = chance.Kind;
                return true;
            }
        }

        result = _specialKodamas[_specialKodamas.Count - 1].Kind;
        return true;
    }
}




[CreateAssetMenu(menuName = "Scriptable Objects/StageData")]
public class StageData : ScriptableObject
{
    [SerializeField] private int _stageId;
    [SerializeField] private string _stageName;

    [SerializeField] PlacementData _start;
    [SerializeField] List<GoalPlacement> _goals = new();
    [SerializeField] List<FirePlacement> _fires = new();
    [SerializeField] List<SpawnSpotPlacement> _spawnSpots = new();
    [SerializeField] List<EnemyPlacement> _enemies = new();
    [SerializeField] WalkableGrid _walkableGrid = new();

    public int StageId => _stageId;
    public string StageName => _stageName;

    public PlacementData Start => _start;
    public IReadOnlyList<GoalPlacement> Goals => _goals;
    public IReadOnlyList<SpawnSpotPlacement> SpawnSpots => _spawnSpots;
    public IReadOnlyList<EnemyPlacement> Enemies => _enemies;

    public WalkableGrid WalkableGrid => _walkableGrid;

    public const string StageIdField = nameof(_stageId);
    public const string StageNameField = nameof(_stageName);
    public const string StartField = nameof(_start);
    public const string GoalsField = nameof(_goals);
    public const string SpawnSpotsField = nameof(_spawnSpots);
    public const string EnemiesField = nameof(_enemies);
    public const string WalkableGridField = nameof(_walkableGrid);


}
