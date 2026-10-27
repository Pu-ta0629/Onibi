using System;
using System.Collections.Generic;
using UnityEditor.SettingsManagement;
using UnityEngine;
using UnityEngine.AI;

public sealed class StageNavMeshSet : IDisposable
{
    private float Thickness = 0.2f;

    private sealed class Entry
    {
        public int AgentTypeId;
        public NavMeshData Data;
        public NavMeshDataInstance Instance;
    }

    private readonly WalkableGrid _grid;
    private Dictionary<MovementProfile, Entry> _entries = new();

    public StageNavMeshSet(WalkableGrid grid) => _grid = grid;

    public int GetAgentTypeId(MovementProfile profile)
    {
        if (_entries.TryGetValue(profile, out var cached)) return cached.AgentTypeId;

        var entry = Build(profile);
        if (entry == null) return -1;

        _entries.Add(profile, entry);
        return entry.AgentTypeId;
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
        if(sources.Count == 0)
        {
            Debug.LogWarning($"[StageNavMeshSet]{profile}が通れるマスがないので、NavMeshを作りませんでした");
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

        for(var z = 0; z < _grid.Depth;z++)
        {
            var x = 0;
            while (x < _grid.Width)
            {
                if(!_grid.CanEnter(x,z,profile))
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
        foreach(var entry in _entries.Values)
        {
            if (entry.Instance.valid) entry.Instance.Remove();
            if (entry.Data != null) UnityEngine.Object.Destroy(entry.Data);
            NavMesh.RemoveSettings(entry.AgentTypeId);
        }

        _entries.Clear();
    }
}
