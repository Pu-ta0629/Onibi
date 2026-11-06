using UnityEngine;
using UnityEngine.AI;

public static class NavAgentProfile
{
    private const float SampleDistance = 1f;

    // StageNavMeshSetの agentHeight と同じ値にしておく
    private const float AgentHeight = 2f;

    // 生成直後に1回呼ぶ。失敗したらfalse(通れるマスが無い、位置がNavMesh上にない、など)
    public static bool Apply(NavMeshAgent agent,StageNavMeshSet navSet,MovementProfile profile,Vector3 position)
    {
        // 設定を変えるので、一旦無効
        agent.enabled = false;

        int agentTypeId = navSet.GetAgentTypeId(profile);
        if (agentTypeId < 0) return false;

        if(!navSet.TrySamplePosition(profile,position,SampleDistance,out var onMesh))
        {
            Debug.LogWarning($"[NavAgentProfile] {agent.name}:{profile}のNavMesh上に、位置{position}が見つかりません");
            return false;
        }

        // 有効にする前に、種類と大きさを設定する
        agent.agentTypeID = agentTypeId;
        agent.radius = SizeRules.AgentRadius(profile.Size);
        agent.height = AgentHeight;
        agent.areaMask = NavMesh.AllAreas;

        // 有効にしてから、NavMesh上の位置へ置く
        agent.enabled = true;
        return agent.Warp(onMesh);
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

   

}
