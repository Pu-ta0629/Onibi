using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WalkableGrid
{
    [SerializeField, HideInInspector] private int _width = 32;
    [SerializeField, HideInInspector] private int _depth = 32;
    [SerializeField] private float _cellsize = 1f;
    [SerializeField] private Vector3 _origin;
    // 地形の種類(TerrainTypeの値。0 地面、1 壁、2 川、3 崖や穴)
    [SerializeField, HideInInspector] private byte[] _cells = CreateFilled(32 * 32, (byte)TerrainType.Wall);
    // 陣営ごとの、通れないマス(Factionのビットを、マスごとに持つ。0は誰も制限しない)
    [SerializeField, HideInInspector] private byte[] _factionBlocks = CreateFilled(32 * 32, (byte)FactionRules.All);

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

    private static byte[] CreateFilled(int count,byte value)
    {
        byte[] array = new byte[count];
        for (int i = 0; i < count; i++) array[i] = value;
        return array;
    }

    public TerrainType GetTerrain(int x, int z)
    {
        if (x < 0 || z < 0 || x >= _width || z >= _depth) return TerrainType.Wall;

        var index = z * Width + x;

        return index < _cells.Length ? (TerrainType)_cells[index] : TerrainType.Wall;
    }

    public Faction GetBlockedFactions(int x, int z)
    {
        if (x < 0 || z < 0 || x >= Width || z >= _depth) return Faction.None;

        int index = z * Width + x;
        // 変更前: return index < _factionBlocks.Length ? (Faction)_factionBlocks[index] : Faction.None;
        // 変更後: 配列が足りない時は、下地(全陣営が通れない)として扱う
        return index < _factionBlocks.Length ? (Faction)_factionBlocks[index] : FactionRules.All;
    }

    public Faction GetPassableFactions(int x,int z)
    {
        return FactionRules.All & ~GetBlockedFactions(x, z);
    }

    // マス単位の判定(古い判定。移動手段と陣営を見ないので、CanEnterを使うこと)
    // 他の班員のコードが使っている可能性があるので、削除せずObsoleteにしている
    [System.Obsolete("移動手段と陣営を見ません。CanEnter(x, z, profile)を使ってください")]
    public bool IsBlocked(int x, int z)
    {
        if (x < 0 || z < 0 || x >= _width || z >= _depth) return true;

        var index = z * _width + x;
        return index >= _cells.Length || _cells[index] != 0;
    }

    public bool CanEnter(int x, int z, MovementProfile profile)
    {
        if (!TerrainRules.CanEnter(profile.Move, GetTerrain(x, z))) return false;
        return (GetBlockedFactions(x, z) & profile.Side) == 0;
    }

    public bool CanEnter(Vector3 worldPosition, MovementProfile profile)
    {
        var x = Mathf.FloorToInt((worldPosition.x - Origin.x) / CellSize);
        var z = Mathf.FloorToInt((worldPosition.z - Origin.z) / CellSize);
        return CanEnter(x, z, profile);
    }

    // 座標での判定(古い判定。CanEnter(worldPosition, profile)を使うこと)
    [System.Obsolete("移動手段と陣営を見ません。CanEnter(worldPosition, profile)を使ってください")]
    public bool IsWalkable(Vector3 worldPosition)
    {
        var x = Mathf.FloorToInt((worldPosition.x - _origin.x) / _cellsize);
        var z = Mathf.FloorToInt((worldPosition.z - _origin.z) / _cellsize);
#pragma warning disable CS0618
        return !IsBlocked(x, z);
#pragma warning restore CS0618
    }

    // マスの中心座標
    public Vector3 CellCenter(int x, int z)
    {
        return new Vector3(
            _origin.x + (x + 0.5f) * _cellsize,
            _origin.y,
            _origin.z + (z + 0.5f) * _cellsize);
    }

    public void CollectCellsInCircle(Vector3 center,float radius,List<Vector2Int> result)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - radius - _origin.x) / _cellsize));
        int maxX = Mathf.Min(_width - 1, Mathf.FloorToInt((center.x + radius - _origin.x) / _cellsize));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((center.z - radius - _origin.z) / _cellsize));
        int maxZ = Mathf.Min(_depth - 1, Mathf.FloorToInt((center.z + radius - _origin.z) / _cellsize));

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellCenter = CellCenter(x, z);
                float dx = cellCenter.x - center.x;
                float dz = cellCenter.z - center.z;
                if (dx * dx + dz * dz <= radius * radius) result.Add(new Vector2Int(x, z));
            }
        }
    }
}