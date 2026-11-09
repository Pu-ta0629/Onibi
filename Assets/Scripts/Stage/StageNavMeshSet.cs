using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
// 修正: UnityEditor.SettingsManagementのusingを削除(ビルド時にエラーになるため)

public sealed class StageNavMeshSet : IDisposable
{
    private const float Thickness = 0.2f;

    private sealed class Entry
    {
        public int AgentTypeId;
        public NavMeshData Data;
        public NavMeshDataInstance Instance;
    }

    private readonly WalkableGrid _grid;
    private readonly Dictionary<MovementProfile, Entry> _entries = new();
    private readonly HashSet<MovementProfile> _failed = new();

    public StageNavMeshSet(WalkableGrid grid) => _grid = grid;


#if UNITY_EDITOR || DEVELOPMENT_BUILD

    public WalkableGrid Grid => _grid;

    public int BuiltCount => _entries.Count;

    public IEnumerable<KeyValuePair<MovementProfile,int>> GetBuiltAgentTypes()
    {
        foreach (KeyValuePair<MovementProfile, Entry> pair in _entries)
            yield return new KeyValuePair<MovementProfile, int>(pair.Key, pair.Value.AgentTypeId);
    }

#endif


    public int GetAgentTypeId(MovementProfile profile)
    {
        if (_entries.TryGetValue(profile, out var cached)) return cached.AgentTypeId;
        if(_failed.Contains(profile)) return -1;

        var entry = Build(profile);
        if (entry == null) 
        {
            _failed.Add(profile);
            return -1; 
        }

        _entries.Add(profile, entry);
        return entry.AgentTypeId;
    }

    public void Prewarm(IEnumerable<MovementProfile> profiles)
    {
        foreach (MovementProfile profile in profiles)
            GetAgentTypeId(profile);    //作って、キャッシュに入れる
    }

    public static HashSet<MovementProfile> CollectProfiles(StageData stage, params MovementProfile[] extraProfiles)
    {
        var profiles = new HashSet<MovementProfile>(extraProfiles);
        profiles.Add(MovementProfile.Fire); // Fireは、全ステージで必要
        foreach (EnemyPlacement placement in stage.Enemies)
            profiles.Add(placement.Profile);

        foreach(SpawnSpotPlacement spot in stage.SpawnSpots)
        {
            foreach (MovementProfile profile in spot.EnemyProfiles)
                profiles.Add(profile);
        }

        return profiles;
    }

    // 警護対象のように、StageDataに入っていないプロファイルは、extraProfilesで渡す
    public static StageNavMeshSet CreateFor(StageData stage,params MovementProfile[] extraProfiles)
    {
        var set = new StageNavMeshSet(stage.WalkableGrid);
        set.Prewarm(CollectProfiles(stage, extraProfiles));
        return set;
    }

    public bool TrySamplePosition(MovementProfile profile, Vector3 position, float maxDistance, out Vector3 result)
    {
        result = position;

        var agentTypeId = GetAgentTypeId(profile);
        if (agentTypeId < 0) return false;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas,
        };
        if (!NavMesh.SamplePosition(position, out var hit, maxDistance, filter)) return false;

        result = hit.position;
        return true;
    }

    private Entry Build(MovementProfile profile)
    {
        var sources = CollectSources(profile);
        if (sources.Count == 0)
        {
            Debug.LogWarning($"[StageNavMeshSet] {profile}が通れるマスがないので、NavMeshを作りませんでした");
            return null;
        }

        var settings = NavMesh.CreateSettings();
        settings.agentRadius = SizeRules.AgentRadius(profile.Size);
        settings.agentHeight = 2f;
        settings.agentClimb = 0.4f;
        settings.agentSlope = 45f;

        var o = _grid.Origin;
        var width = _grid.Width * _grid.CellSize;
        var depth = _grid.Depth * _grid.CellSize;
        var center = new Vector3(o.x + width * 0.5f, o.y, o.z + depth * 0.5f);

        var bounds = new Bounds(Vector3.zero, new Vector3(width + 2f, 5f, depth + 2f));

        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, center, Quaternion.identity);

        return new Entry
        {
            AgentTypeId = settings.agentTypeID,
            Data = data,
            Instance = NavMesh.AddNavMeshData(data),
        };
    }

    private List<NavMeshBuildSource> CollectSources(MovementProfile profile)
    {
        var sources = new List<NavMeshBuildSource>();
        var o = _grid.Origin;
        var s = _grid.CellSize;

        for (var z = 0; z < _grid.Depth; z++)
        {
            var x = 0;
            while (x < _grid.Width)
            {
                if (!_grid.CanEnter(x, z, profile))
                {
                    x++;
                    continue;
                }

                var start = x;
                while (x < _grid.Width && _grid.CanEnter(x, z, profile)) x++;
                var length = x - start;

                var position = new Vector3(
                    o.x + (start + length * 0.5f) * s,
                    o.y - Thickness * 0.5f,
                    o.z + (z + 0.5f) * s);

                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one),
                    size = new Vector3(length * s, Thickness, s),
                    area = 0,
                });
            }
        }

        return sources;
    }

    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            if (entry.Instance.valid) entry.Instance.Remove();
            if (entry.Data != null) UnityEngine.Object.Destroy(entry.Data);
            NavMesh.RemoveSettings(entry.AgentTypeId);
        }

        _entries.Clear();
        _failed.Clear();
    }

    
    
}