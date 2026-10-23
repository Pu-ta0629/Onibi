using UnityEngine;

[System.Serializable]
public class WalkableGrid
{
    [SerializeField,HideInInspector] private int _width = 32;
    [SerializeField,HideInInspector] private int _depth = 32;
    [SerializeField] private float _cellsize = 1f;
    [SerializeField] private Vector3 _origin;
    //地形の種類(TerrainTypeの値。0 地面、1 壁、2 川、3 崖や穴)
    [SerializeField,HideInInspector] private byte[] _cells = new byte[32 * 32];
    // 陣営ごとの、通れないマス(Factionのビットを、マスごとに持つ。0は誰も制限しない)
    [SerializeField,HideInInspector] private byte[] _factionBlocks = new byte[32 * 32];

    public int Width => _width;
    public int Depth => _depth;
    public float CellSize => _cellsize;
    public Vector3 Origin => _origin;

    public const byte Blocked = 1;

    public const string WidthField = nameof(_width);
    public const string DepthField = nameof(_depth);
    public const string CellSizeField = nameof(_cellsize);
    public const string OriginField = nameof(_origin);
    public const string CellsField = nameof(_cells);
    public const string FactionBlocksField = nameof(_factionBlocks);

    public TerrainType GetTerrain(int x,int z)
    {
        if (x < 0 || z < 0 || x >= _width || z >= _depth) return TerrainType.Wall;

        var index = z * Width + x;

        return index < _cells.Length ? (TerrainType)_cells[index] : TerrainType.Wall;
    }

    public Faction GetBlockedFactions(int x,int z)
    {
        if (x < 0 || z < 0 || x >= Width || z >= _depth) return Faction.None;

        var index = z * Width + x;
        return index < _factionBlocks.Length ? (Faction)_factionBlocks[index] : Faction.None;
    }


    //マス単位の判定
    public bool IsBlocked(int x,int z)
    {
        if (x < 0 || z < 0 || x >= _width || z >= _depth) return true;

        var index = z * _width + x;
        return index >= _cells.Length || _cells[index] != 0;
    }

    public bool CanEnter(int x,int z,MovementProfile profile)
    {
        if (!TerrainRules.CanEnter(profile.Move, GetTerrain(x, z))) return false;
        return (GetBlockedFactions(x, z) & profile.Side) == 0;
    }

    public bool CanEnter(Vector3 worldPosition,MovementProfile profile)
    {
        var x = Mathf.FloorToInt((worldPosition.x - Origin.x) / CellSize);
        var z = Mathf.FloorToInt((worldPosition.z - Origin.z) / CellSize);
        return CanEnter(x,z,profile);
    }
    
    //座標での判定
    public bool IsWalkable(Vector3 worldPosition)
    {
        var x = Mathf.FloorToInt((worldPosition.x - _origin.x) / _cellsize);
        var z = Mathf.FloorToInt((worldPosition.z - _origin.z) / _cellsize);
        return !IsBlocked(x, z);
    }

    //マスの中心座標
    public Vector3 CellCenter(int x,int z)
    {
        return new Vector3(
            _origin.x + (x + 0.5f) * _cellsize,
            _origin.y,
            _origin.z + (z + 0.5f) * _cellsize);
    }
    

    
}
