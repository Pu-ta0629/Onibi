using Mono.Cecil;
using UnityEngine;
using UnityEngine.AI;

public static class NavAgentProfile
{
    private const float SampleDistance = 1f;

    // StageNavMeshSetの agentHeight と同じ値にしておく
    private const float AgentHeight = 2f;

    //AgentのTypeと大きさを、profileに合わせる（位置には触らない。Agentは無効のまま残す）
    public static bool ConfigureType(NavMeshAgent agent, StageNavMeshSet navSet, MovementProfile profile)
    {
        if (agent == null || navSet == null) return false;

        if (!navSet.TryGetAgentTypeId(profile,out int id))
        {
            Debug.LogWarning($"[NavAgentProfile] {agent.name}: {profile} のNavMeshがありません(通れるマスが0、または作成に失敗)");
            return false;
        }

        agent.enabled = false;
        agent.agentTypeID = id;
        agent.radius = SizeRules.AgentRadius(profile.Size);
        agent.height = AgentHeight;
        agent.areaMask = NavMesh.AllAreas;

        if (agent.agentTypeID != id)
            Debug.LogWarning($"[NavAgentProfile] {agent.name}: agentTypeIDが反映されません(設定 {id} / 読み取り {agent.agentTypeID})");
        return true;
    }
    public static bool Apply(NavMeshAgent agent,MovementProfile profile,Vector3 position)
    {
        if(StageNavMeshProvider.Current == null)
        {
            Debug.LogWarning($"[NavAgent]{agent.name}: StageNavMeshProvider.Loadが、まだ呼ばれていません");
            return false;
        }
        return Apply(agent, StageNavMeshProvider.Current, profile, position);
    }

    // 生成直後に1回呼ぶ。失敗したらfalse(通れるマスが無い、位置がNavMesh上にない、など)
    public static bool Apply(NavMeshAgent agent,StageNavMeshSet navSet,MovementProfile profile,Vector3 position)
    {
        if (agent == null || navSet == null) return false;

        agent.enabled = false;
        if(!ConfigureType(agent,navSet,profile)) return false;

        Vector3 probe = new Vector3(position.x, navSet.GroundY, position.z);
        if(!navSet.TrySamplePosition(profile,probe,SampleDistance,out Vector3 onMesh))
        {
            Debug.LogWarning($"[NavAgentProfile] {agent.name}: {profile}のNavMesh上に、位置{position}が見つかりません / {navSet.DescribeCell(profile, position)}");
            return false;
        }

        agent.transform.position = onMesh;

        agent.enabled = true;
        bool placed = agent.isOnNavMesh || agent.Warp(onMesh);
        if (!placed)
            Debug.LogWarning($"[NavAgentProfile] {agent.name}: NavMesh上に置けませんでした(位置{onMesh}、type={agent.agentTypeID})");

#if UNITY_EDITOR
        navSet.TryGetAgentTypeId(profile, out int expected);
        Debug.Log($"[NavAgentProfile]{agent.name}: Apply完了 type={agent.agentTypeID} 期待={expected}" +
                  $"isOnNavMesh={agent.isOnNavMesh} placed={placed} frame={Time.frameCount}");
        NavAgentTypeWatcher watcher = agent.GetComponent<NavAgentTypeWatcher>();
        if(watcher == null) watcher = agent.gameObject.AddComponent<NavAgentTypeWatcher>();
        watcher.Expect(agent,expected);

#endif
        return placed;

    }


   

}
