using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Android;

public static class NavAgentProfile
{
    private const float SampleDistance = 1f;

    // StageNavMeshSetの agentHeight と同じ値にしておく
    private const float AgentHeight = 2f;

    //AgentのTypeと大きさを、profileに合わせる（位置には触らない。Agentは無効のまま残す）
    public static bool ConfigureType(NavMeshAgent agent, StageNavMeshSet navSet, MovementProfile profile)
    {
        if (agent == null || navSet == null) return false;

        var id = navSet.GetAgentTypeId(profile);
        if (id < 0)
        {
            Debug.LogWarning($"[NavAgentProfile] {agent.name}: {profile} のNavMeshがありません(通れるマスが0、または作成に失敗)");
            return false;
        }
        if (agent.agentTypeID == id) return true;

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
        // 設定を変えるので、一旦無効
        agent.enabled = false;

        if (!ConfigureType(agent, navSet, profile))
        {
            Debug.LogWarning($"[NavAgentProfile] {agent.name} : {profile}のNavMeshがありません（通れるマスがないか、作成に失敗しています）");
            return false; 
        }

        //NavMEshは格子の高さにあるので、高さを合わせてから探す
        var prode = new Vector3(position.x, navSet.GroundY, position.z);
        if(!navSet.TrySamplePosition(profile, prode, SampleDistance, out var onMesh))
        {
            Debug.LogWarning($"[NavAgentProfile]{agent.name}:{profile}のNavMesh上に、位置{position}が見つかりません / {navSet.DescribeCell(profile, position)}");
        }

        // 有効にしてから、NavMesh上の位置へ置く
        agent.enabled = true;
        return agent.Warp(onMesh);
    }


   

}
