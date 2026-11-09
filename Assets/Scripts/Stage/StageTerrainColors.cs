using JetBrains.Annotations;
using UnityEngine;

[System.Serializable]
public class StageTerrainColors
{
    [SerializeField] private Color _ground = new Color(0.10f, 0.35f, 0.15f);
    [SerializeField] private Color _water = new Color(0.10f, 0.35f, 0.15f);
    [SerializeField] private Color _hole = new Color(0.10f, 0.35f, 0.15f);
    [SerializeField] private Color _wall = new Color(0.10f, 0.35f, 0.15f);

    public Color Get(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return _ground;
            case TerrainType.Water: return _water;
            case TerrainType.Hole: return _hole;
            default: return _wall;
        }

    }
}
