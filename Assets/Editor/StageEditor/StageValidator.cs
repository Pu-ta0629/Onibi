using System.Collections.Generic;
using UnityEngine;

public static class StageValidator
{
    // Unit(Fire)は、移動領域が共通でFly、大きさは最小のSmall
    private static readonly MovementProfile FireProfile = MovementProfile.Fire;

    public static List<string> Validate(StageData stage)
    {
        var issues = new List<string>();
        var name = stage.name;
        var grid = stage.WalkableGrid;

        CheckPlacement(issues, name, grid, "Start", stage.Start, FireProfile);

        for (var i = 0; i < stage.Goals.Count; i++)
            CheckPlacement(issues, name, grid, $"Goal{i + 1}", stage.Goals[i], FireProfile);


        for (var i = 0; i < stage.Enemies.Count; i++)
        {
            var placement = stage.Enemies[i];
            if (placement.Enemy == null)
                issues.Add($"{name}: Enemy{i + 1} の敵(EnemyData)が、未設定です");

            CheckPlacement(issues, name, grid, $"Enemy{i + 1}", placement, placement.Profile);
        }

        bool[,] reached = null;

        if (TryGetWalkableCell(grid, stage.Start.Position, FireProfile, out var sx, out var sz))
        {
            reached = Flood(grid, FireProfile, sx, sz);

            for (var i = 0; i < stage.Goals.Count; i++)
                CheckReachable(issues, name, grid, reached, $"Goal{i + 1}", stage.Goals[i], FireProfile, "スタートから到達できません");

        }

        CheckSpawnSpots(issues,name,grid,stage.SpawnSpots,reached);

        return issues;
    }

    private static void CheckPlacement(List<string> issues, string stageName, WalkableGrid grid, string label, PlacementData placement, MovementProfile profile)
    {
        if (grid.CanEnter(placement.Position, profile)) return;
        issues.Add($"{stageName}: {label}が、通れないマス、または格子の外にあります{placement.Position}({profile})");
    }

    private static void CheckReachable(List<string> issues, string stageName, WalkableGrid grid, bool[,] reached,
        string label, PlacementData placement, MovementProfile profile, string message)
    {
        if (!TryGetWalkableCell(grid, placement.Position, profile, out var x, out var z)) return;

        if (!reached[x, z]) issues.Add($"{stageName}: {label}が、{message}({profile})");
    }

    private static void CheckSpawnSpots(List<string> issues, string stageName, WalkableGrid grid, IReadOnlyList<SpawnSpotPlacement> spots, bool[,] reached)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int i = 0; i < spots.Count; i++)
        {
            SpawnSpotPlacement spot = spots[i];
            string label = $"湧きスポット{i + 1}({spot.Kind})";

            switch (spot.Kind)
            {
                case SpawnKind.Enemy:
                    if (spot.EnemyList.Count == 0)
                        issues.Add($"{stageName}: {label}に、湧く敵(EnemyData)が1つもありません");
                    foreach (EnemyData enemy in spot.EnemyList)
                    {
                        if (enemy == null) issues.Add($"{stageName}: {label}の敵の候補に、未設定の項目があります");
                    }
                    break;

                case SpawnKind.SpecialKodama:
                    if (spot.SpecialKodamas.Count == 0)
                    {
                        issues.Add($"{stageName}: {label}に、特殊木霊の種類が1つもありません");
                        break;
                    }
                    float total = 0f;
                    foreach (SpecialKodamaChance chance in spot.SpecialKodamas) total += chance.Probability;
                    if (Mathf.Abs(total - 1f) > 0.001f)
                        issues.Add($"{stageName}: {label}の確率の合計が、1になっていません(現在{total:0.###})");
                    break;
            }

            cells.Clear();
            grid.CollectCellsInCircle(spot.Position, spot.Radius, cells);

            bool hasOpenCell = false;
            bool reachable = false;
            foreach (Vector2Int cell in cells)
            {
                if (grid.GetTerrain(cell.x, cell.y) == TerrainType.Wall) continue;

                hasOpenCell = true;
                if (reached != null && reached[cell.x, cell.y]) reachable = true;
            }

            if (!hasOpenCell)
                issues.Add($"{stageName}: {label}の範囲内に、壁以外のマスがありません");
            else if (spot.Kind != SpawnKind.Enemy && !reachable)
                issues.Add($"{stageName}: {label}の範囲に、スタートから到達できるマスがありません");
        }
    }

    private static bool TryGetWalkableCell(WalkableGrid grid, Vector3 position, MovementProfile profile, out int x, out int z)
    {
        x = Mathf.FloorToInt((position.x - grid.Origin.x) / grid.CellSize);
        z = Mathf.FloorToInt((position.z - grid.Origin.z) / grid.CellSize);
        return grid.CanEnter(x, z, profile);
    }

    private static bool[,] Flood(WalkableGrid grid, MovementProfile profile, int startX, int startZ)
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

                if (!grid.CanEnter(nx, nz, profile) || reached[nx, nz]) continue;

                reached[nx, nz] = true;
                queue.Enqueue((nx, nz));
            }
        }
        return reached;
    }
}