using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// NavMeshの生成と、エージェントの種類の設定を確認するための、テスト用スクリプト
// 空のGameObjectに付けて、Stageを設定して、再生する
public class StageNavMeshTest : MonoBehaviour
{
    private sealed class TestAgent
    {
        public MovementProfile Profile;
        public NavMeshAgent Agent;
        public GameObject Root;
    }

    [SerializeField] private StageData _stage;
    [SerializeField] private List<MovementProfile> _extraProfiles = new List<MovementProfile>(); // 警護対象など
    [SerializeField, Min(1)] private int _agentsPerProfile = 2;
    [SerializeField, Min(0.1f)] private float _moveSpeed = 3f;
    [SerializeField, Min(0.5f)] private float _repathInterval = 3f;
    [SerializeField] private bool _moveRandomly = true;
    [SerializeField] private bool _showGround = true;

    private readonly List<TestAgent> _agents = new List<TestAgent>();
    private float _timer;
    private Coroutine _respawn;

    private void Start()
    {
        Setup();
    }

    private void OnDestroy()
    {
        StageGroundProvider.Unload();
        StageNavMeshProvider.Unload();
    }

    private void Update()
    {
        if (!_moveRandomly) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = _repathInterval;
        MoveAll();
    }

    // NavMeshと地面を作って、プロファイルごとにエージェントを作る
    private void Setup()
    {
        if (_stage == null)
        {
            Debug.LogWarning("[NavMeshTest] _stage が未設定です");
            return;
        }

        MovementProfile[] extra = _extraProfiles.ToArray();
        StageNavMeshProvider.Load(_stage, extra);
        if (_showGround) StageGroundProvider.Load(_stage);

        HashSet<MovementProfile> profiles = StageNavMeshSet.CollectProfiles(_stage, extra);
        int profileIndex = 0;
        foreach (MovementProfile profile in profiles)
        {
            for (int i = 0; i < _agentsPerProfile; i++)
                SpawnAgent(profile, profileIndex, i);
            profileIndex++;
        }

        Debug.Log($"[NavMeshTest] プロファイル{profiles.Count}種類、エージェント{_agents.Count}体を作りました");
        Verify();
    }

    private void SpawnAgent(MovementProfile profile, int profileIndex, int number)
    {
        // Unit(Fire)の1体目だけ、スタート位置。それ以外は、そのプロファイルで通れるマスのどこか
        Vector3 position;
        if (profile.Equals(MovementProfile.Fire) && number == 0)
        {
            position = _stage.Start.Position;
        }
        else if (!TryGetRandomCell(profile, out position))
        {
            Debug.LogWarning($"[NavMeshTest] {profile}: 通れるマスが無いので、エージェントを作りませんでした");
            return;
        }

        GameObject root = new GameObject($"Test_{profile.Move}_{profile.Size}_{profile.Side}_{number}");
        root.transform.SetParent(transform, false);

        // NavMeshの無い状態で、NavMeshAgentが有効になって、エラーが出るのを防ぐ
        root.SetActive(false);
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        root.SetActive(true);

        CreateVisual(root.transform, profile, profileIndex);

        bool ok = NavAgentProfile.Apply(agent, profile, position);
        agent.speed = _moveSpeed;
        Debug.Log($"[NavMeshTest] {root.name}: Apply={ok}");

        _agents.Add(new TestAgent { Profile = profile, Agent = agent, Root = root });
    }

    // 見た目だけのカプセル。大きさは、エージェントの半径に合わせる
    private static void CreateVisual(Transform parent, MovementProfile profile, int profileIndex)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        UnityEngine.Object.Destroy(visual.GetComponent<Collider>()); // Colliderは使わない設計のため
        visual.name = "Visual";
        visual.transform.SetParent(parent, false);

        float radius = SizeRules.AgentRadius(profile.Size);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);

        visual.GetComponent<Renderer>().material.color = Color.HSVToRGB((profileIndex * 0.17f) % 1f, 0.8f, 1f);
    }

    // そのプロファイルで通れるマスの、中心のどこか
    private bool TryGetRandomCell(MovementProfile profile, out Vector3 position)
    {
        WalkableGrid grid = _stage.WalkableGrid;
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int z = 0; z < grid.Depth; z++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.CanEnter(x, z, profile)) cells.Add(new Vector2Int(x, z));
            }
        }

        position = Vector3.zero;
        if (cells.Count == 0) return false;

        Vector2Int cell = cells[Random.Range(0, cells.Count)];
        position = grid.CellCenter(cell.x, cell.y);
        return true;
    }

    // すべてのエージェントを、それぞれのプロファイルで通れる、ランダムな場所へ歩かせる
    private void MoveAll()
    {
        if (StageNavMeshProvider.Current == null) return;

        foreach (TestAgent test in _agents)
        {
            if (test.Agent == null || !test.Agent.isOnNavMesh) continue;
            if (!TryGetRandomCell(test.Profile, out Vector3 target)) continue;
            if (!StageNavMeshProvider.Current.TrySamplePosition(test.Profile, target, 1f, out Vector3 onMesh)) continue;

            test.Agent.SetDestination(onMesh);
        }
    }

    // エージェントの種類が、期待どおりに設定されているかを、Consoleに出す
    [ContextMenu("Verify")]
    private void Verify()
    {
        if (StageNavMeshProvider.Current == null)
        {
            Debug.LogWarning("[NavMeshTest] StageNavMeshProvider.Current が null です(Loadが呼ばれていません)");
            return;
        }

        int ngCount = 0;
        foreach (TestAgent test in _agents)
        {
            NavMeshAgent agent = test.Agent;
            int expected = StageNavMeshProvider.Current.GetAgentTypeId(test.Profile);
            float expectedRadius = SizeRules.AgentRadius(test.Profile.Size);

            bool typeOk = agent.agentTypeID == expected;
            bool onMesh = agent.isOnNavMesh;
            if (!typeOk || !onMesh) ngCount++;

            Debug.Log($"[NavMeshTest] {test.Root.name}: agentTypeID={agent.agentTypeID} 期待値={expected} " +
                      $"{(typeOk ? "OK" : "NG")} / isOnNavMesh={onMesh} / radius={agent.radius:0.##}(期待{expectedRadius:0.##})");
        }

        Debug.Log($"[NavMeshTest] 確認結果: {(ngCount == 0 ? "すべてOK" : $"NG {ngCount}件")}");
    }

    // 報告された状態を、わざと作る: 種類を、Unityの初期値(0)に戻す
    [ContextMenu("Tamper (種類を壊す)")]
    private void Tamper()
    {
        foreach (TestAgent test in _agents)
            test.Agent.agentTypeID = 0;

        Debug.Log("[NavMeshTest] すべてのエージェントの種類を、0に戻しました。Verifyで、NGになるはずです");
    }

    // NavAgentProfile.Applyで、種類を設定し直す
    [ContextMenu("Reapply (Applyで設定し直す)")]
    private void Reapply()
    {
        int okCount = 0;
        foreach (TestAgent test in _agents)
        {
            if (NavAgentProfile.Apply(test.Agent, test.Profile, test.Agent.transform.position)) okCount++;
            test.Agent.speed = _moveSpeed;
        }

        Debug.Log($"[NavMeshTest] Applyの成功 {okCount}/{_agents.Count}");
        Verify();
    }

    // エージェントを作り直して、NavMeshも作り直す
    [ContextMenu("Respawn")]
    private void Respawn()
    {
        if (_respawn != null) StopCoroutine(_respawn);
        _respawn = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        foreach (TestAgent test in _agents)
        {
            if (test.Root != null) Destroy(test.Root);
        }
        _agents.Clear();

        yield return null; // Destroyの完了を待つ

        StageGroundProvider.Unload();
        StageNavMeshProvider.Unload();
        Setup();
        _respawn = null;
    }

    // Gameビューのボタン
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 220f, 200f));
        if (GUILayout.Button("Verify")) Verify();
        if (GUILayout.Button("Tamper (種類を壊す)")) Tamper();
        if (GUILayout.Button("Reapply")) Reapply();
        if (GUILayout.Button("Move All")) MoveAll();
        if (GUILayout.Button("Respawn")) Respawn();
        GUILayout.EndArea();
    }
}