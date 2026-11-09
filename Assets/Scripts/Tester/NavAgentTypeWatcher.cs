#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.AI;

// 調査用。Apply後に、agentTypeIDが、いつ、変わったかを見る
[DisallowMultipleComponent]
public sealed class NavAgentTypeWatcher : MonoBehaviour
{
    private NavMeshAgent _agent;
    private int _expected;
    private int _last;
    private int _applyFrame;
    private bool _reportedNextFrame;

    public void Expect(NavMeshAgent agent, int expectedTypeId)
    {
        _agent = agent;
        _expected = expectedTypeId;
        _last = agent.agentTypeID;
        _applyFrame = Time.frameCount;
        _reportedNextFrame = false;
    }

    private void LateUpdate()
    {
        if (_agent == null) return;

        int current = _agent.agentTypeID;

        // Applyの、次のフレームの状態を、1回だけ出す
        if (!_reportedNextFrame && Time.frameCount > _applyFrame)
        {
            _reportedNextFrame = true;
            Debug.Log($"[TypeWatcher] {name}: Applyの次のフレーム type={current} 期待={_expected} " +
                      $"isOnNavMesh={_agent.isOnNavMesh} enabled={_agent.enabled}");
        }

        // 変わった瞬間を出す
        if (current != _last)
        {
            Debug.LogWarning($"[TypeWatcher] {name}: frame {Time.frameCount} で type が {_last} から {current} に変わりました(期待 {_expected})");
            _last = current;
        }
    }
}

#endif