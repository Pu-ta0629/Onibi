using System.Collections.Generic;
using UnityEngine;

public class StageValidator
{
    public static List<string> Validate(StageData stage)
    {
        var issues = new List<string>();
        var name = stage.name;
        var grid = stage.WalkableGrid;

        CheckPlacement(issues, name, grid, "Start", stage.Start);
        for (var i = 0; i < stage.Goals.Count; i++)
            CheckPlacement(issues, name, grid, $"Goal {i + 1}", stage.Goals[i]);
        for (var i = 0; i < stage.Enemies.Count; i++)
            CheckPlacement(issues, name, grid, $"Enemy{i + 1}", stage.Enemies[i]);
        for(var i = 0;i<stage.Items.Count; i++)
            CheckPlacement(issues,name,grid,$"Item{i + i}",stage.Items[i]);

        if(TryGetWalkableCell(grid,stage.Start.Position,out var sx,out var sz))
        {
            var reached = Flood(grid, sx, sz);

            for (var i = 0; i < stage.Goals.Count; i++)
                CheckReachable(issues, name, grid, reached, $"Goal{i + 1}", stage.Goals[i], "スタートから到達できません");

            for (var i = 0; i < stage.Items.Count; i++)
                CheckReachable(issues, name, grid, reached, $"Item{i + 1}", stage.Items[i], "スタートから到達できません(取れないアイテム)");
        }

        return issues;
    }

    private static void CheckPlacement(List<string> issues,string stageName,WalkableGrid grid,string label,PlacementData placement)
    {
        if (grid.IsWalkable(placement.Position)) return;
        issues.Add($"{stageName}: {label}が、通れないマス、または格子の外にあります{placement.Position}");
    }

    private static void CheckReachable(List<string> issues,string stageName,WalkableGrid grid, bool[,] reached,
        string label,PlacementData placement,string message)
    {
        if (!TryGetWalkableCell(grid, placement.Position, out var x, out var z)) return;

        if (!reached[x, z]) issues.Add($"{stageName}:{label}が、{message}");
    }

    private static bool TryGetWalkableCell(WalkableGrid grid,Vector3 position,out int x,out int z)
    {
        x = Mathf.FloorToInt((position.x - grid.Origin.x) / grid.CellSize);
        z = Mathf.FloorToInt((position.z - grid.Origin.z) / grid.CellSize);
        return !grid.IsBlocked(x,z);
    }

    private static bool[,] Flood(WalkableGrid grid, int startX, int startZ)
    {
        var reached = new bool[grid.Width, grid.Depth];
        var queue = new Queue<(int x, int z)>();
        var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        reached[startX, startZ] = true;
        queue.Enqueue((startX, startZ));

        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var (dx, dz) in directions)
            {
                var nx = x + dx;
                var nz = z + dz;

                if (grid.IsBlocked(nx, nz) || reached[nx, nz]) continue;

                reached[nx, nz] = true;
                queue.Enqueue((nx, nz));
            }
        }
        return reached;
    }
}
