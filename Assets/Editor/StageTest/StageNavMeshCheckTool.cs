using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// 生成したNavMeshが、StageDataの格子どおりにできているかを調べる確認ツール
// 使い方: 再生して、ProjectウィンドウでStageDataのアセットを選び、メニューから実行する
public static class StageNavMeshCheckTool
{
    private const string MenuPath = "Tools/Stage Tools/NavMesh生成チェック";

    // マスの中心でNavMeshを探す距離。隣のマスまでの距離(0.5)より小さくする
    private const float SampleDistance = 0.25f;
    private const int MaxLog = 10;

    [MenuItem(MenuPath)]
    private static void Run()
    {
        StageData stage = Selection.activeObject as StageData;
        if (stage == null) return;

        // 確認用に、専用のStageNavMeshSetを作る(ゲーム本編のものには触らない)
        using (StageNavMeshSet set = StageNavMeshSet.CreateFor(stage))
        {
            foreach (MovementProfile profile in StageNavMeshSet.CollectProfiles(stage))
                CheckGrid(stage, set, profile);

            CheckReach(stage, set);
        }
    }

    // 再生中で、StageDataを選んでいる時だけ、メニューを有効にする
    [MenuItem(MenuPath, true)]
    private static bool CanRun()
    {
        return Application.isPlaying && Selection.activeObject is StageData;
    }

    // マスごとに、格子とNavMeshを突き合わせる
    private static void CheckGrid(StageData stage, StageNavMeshSet set, MovementProfile profile)
    {
        WalkableGrid grid = stage.WalkableGrid;

        if (set.TryGetAgentTypeId(profile,out int _))
        {
            Debug.Log($"[NavMeshCheck] {profile}: NavMeshなし(通れるマスが0)");
            return;
        }

        // 半径の分だけ縁が削られるので、縁から離れたマスだけを「欠け」の対象にする(おおよその目安)
        float radius = SizeRules.AgentRadius(profile.Size);
        int margin = Mathf.CeilToInt(radius / grid.CellSize + 0.5f) + 1;

        int walkable = 0;
        int onMeshCount = 0;
        int leakCount = 0;
        int interiorMissingCount = 0;
        int logged = 0;

        for (int z = 0; z < grid.Depth; z++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                bool canEnter = grid.CanEnter(x, z, profile);
                bool onMesh = set.TrySamplePosition(profile, grid.CellCenter(x, z), SampleDistance, out _);

                if (canEnter) walkable++;
                if (onMesh) onMeshCount++;

                if (!canEnter && onMesh)
                {
                    leakCount++;
                    if (logged++ < MaxLog)
                        Debug.LogWarning($"[NavMeshCheck] {profile}: 通れないマス({x},{z} / {grid.GetTerrain(x, z)})にNavMeshがあります");
                }
                else if (canEnter && !onMesh && IsInterior(grid, profile, x, z, margin))
                {
                    interiorMissingCount++;
                    if (logged++ < MaxLog)
                        Debug.LogWarning($"[NavMeshCheck] {profile}: 通れるマス({x},{z})の内側なのにNavMeshがありません");
                }
            }
        }

        string verdict = leakCount == 0 && interiorMissingCount == 0 ? "OK" : "NG";
        Debug.Log($"[NavMeshCheck] {profile}: {verdict} 通れるマス{walkable}、NavMesh上のマス{onMeshCount}、" +
                  $"はみ出し{leakCount}、内側の欠け{interiorMissingCount}(半径{radius}、縁の除外幅{margin}マス)");
    }

    // 周囲margin マスが、すべて通れるか
    private static bool IsInterior(WalkableGrid grid, MovementProfile profile, int x, int z, int margin)
    {
        for (int dz = -margin; dz <= margin; dz++)
        {
            for (int dx = -margin; dx <= margin; dx++)
            {
                if (!grid.CanEnter(x + dx, z + dz, profile)) return false;
            }
        }
        return true;
    }

    // Fireのスタートから、ゴールとアイテムへ、NavMeshで経路が引けるかを調べる
    private static void CheckReach(StageData stage, StageNavMeshSet set)
    {
        MovementProfile profile = MovementProfile.Fire;
        if (!set.TryGetAgentTypeId(profile,out int agentTypeId))
        {
            Debug.LogWarning("[NavMeshCheck] Fireの NavMesh がありません");
            return;
        }
        if (!set.TrySamplePosition(profile, stage.Start.Position, 1f, out Vector3 start))
        {
            Debug.LogWarning("[NavMeshCheck] Start が Fire の NavMesh 上にありません");
            return;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas,
        };

        int navUnreachable = 0;
        for (int i = 0; i < stage.Goals.Count; i++)
        {
            if (!CanReach(set, filter, start, stage.Goals[i].Position))
            {
                navUnreachable++;
                Debug.LogWarning($"[NavMeshCheck] Goal{i + 1} に、経路が引けません");
            }
        }

        // StageValidator(格子の探索)の結果と並べて、食い違いを見る
        int validatorUnreachable = 0;
        foreach (string issue in StageValidator.Validate(stage))
        {
            if (issue.Contains("到達")) validatorUnreachable++;
        }

        string verdict = navUnreachable == validatorUnreachable ? "一致" : "不一致";
        Debug.Log($"[NavMeshCheck] 到達: NavMeshで不可{navUnreachable}件、Validatorで不可{validatorUnreachable}件({verdict})");
    }

    private static bool CanReach(StageNavMeshSet set, NavMeshQueryFilter filter, Vector3 start, Vector3 target)
    {
        if (!set.TrySamplePosition(MovementProfile.Fire, target, 1f, out Vector3 goal)) return false;

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(start, goal, filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }
}