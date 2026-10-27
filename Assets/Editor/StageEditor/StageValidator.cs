using System.Collections.Generic;
using UnityEngine;

public class StageValidator
{
    static readonly MovementProfile FireProfile = new(MoveType.Walk, SizeClass.Medium, Faction.Fire);


    public static List<string> Validate(StageData stage)
    {
        var issues = new List<string>();
        var name = stage.name;
        var grid = stage.WalkableGrid;

        CheckPlacement(issues, name, grid, "Start", stage.Start,FireProfile);
        for (var i = 0; i < stage.Goals.Count; i++)
            CheckPlacement(issues, name, grid, $"Goal {i + 1}", stage.Goals[i],FireProfile);
        for (var i = 0; i < stage.Enemies.Count; i++)
            { 
                var placement = stage.Enemies[i];
                if (placement.Enemy == null)
                    issues.Add($"{name}: Enemy{i + 1} の敵(EnemyData)が、未設定です");

                CheckPlacement(issues, name, grid, $"Enemy{i + 1}", placement,placement.Profile);
            }
        for(var i = 0;i<stage.Items.Count; i++)
            CheckPlacement(issues,name,grid,$"Item{i + i}",stage.Items[i],FireProfile);

        if(TryGetWalkableCell(grid,stage.Start.Position,FireProfile,out var sx,out var sz))
        {
            var reached = Flood(grid,FireProfile, sx, sz);

            for (var i = 0; i < stage.Goals.Count; i++)
                CheckReachable(issues, name, grid, reached, $"Goal{i + 1}", stage.Goals[i],FireProfile, "スタートから到達できません");

            for (var i = 0; i < stage.Items.Count; i++)
                CheckReachable(issues, name, grid, reached, $"Item{i + 1}", stage.Items[i],FireProfile, "スタートから到達できません(取れないアイテム)");
        }

        return issues;
    }

    private static void CheckPlacement(List<string> issues, string stageName, WalkableGrid grid, string label, PlacementData placement, MovementProfile profile)
    {
        if (grid.CanEnter(placement.Position,profile)) return;
        issues.Add($"{stageName}: {label}が、通れないマス、または格子の外にあります{placement.Position}({profile})");
    }

    private static void CheckReachable(List<string> issues,string stageName,WalkableGrid grid, bool[,] reached,
        string label,PlacementData placement,MovementProfile profile,string message)
    {
        if (!TryGetWalkableCell(grid, placement.Position,profile, out var x, out var z)) return;

        if (!reached[x, z]) issues.Add($"{stageName}:{label}が、{message}({profile})");
    }

    private static bool TryGetWalkableCell(WalkableGrid grid,Vector3 position,MovementProfile profile,out int x,out int z)
    {
        x = Mathf.FloorToInt((position.x - grid.Origin.x) / grid.CellSize);
        z = Mathf.FloorToInt((position.z - grid.Origin.z) / grid.CellSize);
        return grid.CanEnter(x,z,profile);
    }

    private static bool[,] Flood(WalkableGrid grid,MovementProfile profile, int startX, int startZ)
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

                if (!grid.CanEnter(nx, nz,profile) || reached[nx, nz]) continue;

                reached[nx, nz] = true;
                queue.Enqueue((nx, nz));
            }
        }
        return reached;
    }
}
