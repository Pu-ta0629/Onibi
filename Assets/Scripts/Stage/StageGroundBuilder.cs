using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.TerrainTools;
using UnityEngine;

public sealed class StageGroundBuilder
{
    private const string RootName = "StageGround";
    private const float PlaneUnit = 10f;
    private const float YOffset = -0.01f;

    private struct Block
    {
        public int X;
        public int Z;
        public int W;
        public int D;
        public TerrainType Terrain;
    }

    private readonly List<Material> _materials = new ();
    
    public GameObject Root {  get; private set; }

    public void Build(WalkableGrid grid,StageTerrainColors colors,HideFlags flags)
    {
        Clear();

        Root = new GameObject(RootName);
        Root.hideFlags = flags;

        Dictionary<TerrainType, Material> materials = new();
        float cellSize = grid.CellSize;
        Vector3 origin = grid.Origin;

        foreach(Block block in CollectBlocks(grid))
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = $"{block.Terrain}_{block.X}_{block.Z}";
            plane.hideFlags = flags;

            //見た目だけ
            DestoryObject(plane.GetComponent<MeshCollider>());

            Transform t = plane.transform;
            t.SetParent(Root.transform, false);
            t.localPosition = new Vector3(
                origin.x = (block.X + block.W * 0.5f) *  cellSize,
                origin.y,
                origin.z = (block.Z + block.D * 0.5f ) * cellSize);
            t.localScale = new Vector3(block.W * cellSize / PlaneUnit, 1f, block.D * cellSize / PlaneUnit);

            Renderer renderer = plane.GetComponent<Renderer>();
            if(!materials.TryGetValue(block.Terrain,out Material material))
            {
                material = new Material(renderer.sharedMaterial);
                material.name = $"StageGround_{block.Terrain}";
                material.hideFlags = flags;
                SetColor(material, colors.Get(block.Terrain));

                materials.Add(block.Terrain, material);
                _materials.Add(material);
            }
            renderer.sharedMaterial = material;
        }
    }

    public void Clear()
    {
        DestoryObject(Root);
        Root = null;

        foreach (Material material in _materials)
            DestoryObject(material);
        _materials.Clear();
    }

    private static List<Block> CollectBlocks(WalkableGrid grid)
    {
        List<Block> blocks = new List<Block>();
        List<Block> open = new List<Block>();

        for (int z = 0; z < grid.Depth; z++)
        {
            List<Block> next = new List<Block>();
            int x = 0;
            while (x < grid.Width)
            {
                TerrainType terrain = grid.GetTerrain(x, z);
                int start = x;
                while (x < grid.Width && grid.GetTerrain(x, z) == terrain) x++;
                int width = x - start;

                int index = open.FindIndex(b => b.X == start && b.W == width && b.Terrain == terrain);
                if (index >= 0)
                {
                    Block extended = open[index];
                    extended.D++;
                    open.RemoveAt(index);
                    next.Add(extended);
                }
                else
                {
                    next.Add(new Block { X = start, Z = z, W = width, D = 1, Terrain = terrain });
                }
            }
            blocks.AddRange(open);
            open = next;

        }
        blocks.AddRange(open);
        return blocks;

    }

    private static void SetColor(Material material,Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void DestoryObject(Object target)
    {
        if (target == null) return;

        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

}
