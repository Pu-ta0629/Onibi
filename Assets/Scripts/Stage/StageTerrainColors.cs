using JetBrains.Annotations;
using UnityEngine;

[System.Serializable]
public class StageTerrainColors
{
    [SerializeField] private Color _ground = new Color(0.10f, 0.35f, 0.15f);
    [SerializeField] private Color _water = new Color(0.45f, 0.78f, 1f);
    [SerializeField] private Color _pit = new Color(0.02f, 0.02f, 0.02f);
    [SerializeField] private Color _wall = new Color(0.4f, 0.26f, 0.13f);

    public Color Get(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return _ground;
            case TerrainType.Water: return _water;
            case TerrainType.Pit: return _pit;
            default: return _wall;
        }

    }
}
