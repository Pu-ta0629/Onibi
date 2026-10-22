using UnityEngine;

[System.Serializable]
public class WalkableGrid
{
    [SerializeField,HideInInspector] private int _width = 32;
    [SerializeField,HideInInspector] private int _depth = 32;
    [SerializeField] private float _cellsize = 1f;
    [SerializeField] private Vector3 _origin;
    [SerializeField,HideInInspector] private byte[] _cells = new byte[32 * 32];

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

    //マス単位の判定
    public bool IsBlocked(int x,int z)
    {
        if (x < 0 || z < 0 || x >= _width || z >= _depth) return true;

        var index = z * _width + x;
        return index >= _cells.Length || _cells[index] != 0;
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
