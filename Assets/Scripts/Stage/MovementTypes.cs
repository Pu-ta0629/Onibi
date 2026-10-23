using System;
using UnityEngine;

public enum TerrainType : byte
{
    Ground = 0,
    Wall = 1,
    Water = 2,
    Pit = 3,
}

public enum MoveType : byte
{
    Walk =0,
    Swim = 1,
    Fly = 2,
}

public enum SizeClass : byte
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

[Flags]
public enum Faction : byte
{
    None = 0,
    Fire = 1,
    LostChild = 2,
    Enemy = 4,
}

[Serializable]
public struct MovementProfile : IEquatable<MovementProfile>
{
    [SerializeField] private MoveType _moveType;
    [SerializeField] private SizeClass _size;
    [SerializeField] private Faction _faction;

    public MovementProfile(MoveType moveType, SizeClass size, Faction faction)
    {
        _moveType = moveType;
        _size = size;
        _faction = faction;
    }

    public MoveType Move => _moveType;
    public SizeClass Size => _size;
    public Faction Side => _faction;

    public bool Equals(MovementProfile other) => 
        _moveType == other._moveType && _size == other._size && _faction == other._faction;

    public override bool Equals(object obj) => obj is MovementProfile other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_moveType, _size, _faction);

    public override string ToString() => $"{_moveType}/{_size}/{_faction}";
}

public static class TerrainRules
{
    public static bool CanEnter(MoveType move,TerrainType terrain)
    {
        switch(terrain)
        {
            case TerrainType.Ground:
                return true;
            case TerrainType.Water:
                return move == MoveType.Swim || move == MoveType.Fly;
            case TerrainType.Pit:
                return move == MoveType.Fly;
            default: 
                return false;
        }
    }
}

public static class SizeRules
{
    public static float AgentRadius(SizeClass size)
    {
        switch(size)
        {
            case SizeClass.Small: return 0.3f;
            case SizeClass.Large: return 1.0f;
            default             : return 0.5f; 
        }
    }
}
