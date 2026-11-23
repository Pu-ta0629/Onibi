//#if UNITY_EDITOR

//using UnityEngine;

/// <summary>
/// 敵のPoolの動作確認用。画面のボタンで、Build、敵を倒す、強制回収などを試す
/// </summary>
//public class EnemyPoolDebug : MonoBehaviour
//{
//    [SerializeField] private StageData _stageData;
//    [SerializeField] private EnemyMoveConfig _config = new EnemyMoveConfig();
//    [SerializeField] private PoolManager _poolManager;
   // [SerializeField] private SpawnManager _spawnManager;
//    [Tooltip("迷子の代わりに置くオブジェクト。敵はこの位置に向かう")]
//    [SerializeField] private Transform _target;

//    private HitWorld _world;
//    private bool _isBuilt;

//    private void Update()
//    {
//        if (_isBuilt && _target != null)
//        {
//            _poolManager.Tick(_target.position);
//        }
//    }

//    private void OnGUI()
//    {
//        GUILayout.BeginArea(new Rect(10f, 10f, 420f, 520f));

//        if (!_isBuilt)
//        {
//            if (GUILayout.Button("Build"))
//            {
//                Build();
//            }
//        }
//        else
//        {
//            GUILayout.Label(_poolManager.DescribeStocks());

//            if (GUILayout.Button("位置で置く敵を出す"))
//            {
//                _poolManager.SpawnPlacedEnemies();
//            }

//            if (GUILayout.Button("出ている敵を1体倒す"))
//            {
//                KillActiveEnemies(1);
//            }

//            if (GUILayout.Button("出ている敵を全員倒す"))
//            {
//                KillActiveEnemies(int.MaxValue);
//            }

//            if (GUILayout.Button("強制回収(ReturnAll)"))
//            {
//                _poolManager.ReturnAll();
//            }

//            if (GUILayout.Button("Clear(全部破棄)"))
//            {
//                _poolManager.Clear();
//                _isBuilt = false;
//            }
//        }

//        GUILayout.EndArea();
//    }

//    private void Build()
//    {
//        // ★ HitWorldの作り方は、実際のステージ読み込みの処理に合わせる
//        _world = new HitWorld();

//        // ★ ここで、StageNavMeshSetを作る(普段のステージ読み込みで呼んでいる処理を、同じように呼ぶ)
//        //   NavMeshの準備ができていないと、湧く位置をNavMeshの上に寄せられない

//        _poolManager.Build(_stageData, _mo, _world);
//        _spawnManager.Build(_stageData);
//        _isBuilt = true;
//    }

//    // 出ている敵に、大きなダメージを与えて倒す。返却は、PoolManager.Tickの最後に行われる
//    private void KillActiveEnemies(int maxCount)
//    {
//        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
//        int killed = 0;

//        for (int i = 0; i < enemies.Length && killed < maxCount; i++)
//        {
//            if (enemies[i].isActive)
//            {
//                enemies[i].TakeDamage(999999);
//                killed++;
//            }
//        }
//    }
//}

//#endif