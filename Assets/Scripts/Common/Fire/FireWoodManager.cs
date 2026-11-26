//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// StageDataのFiresの位置に焚き木（FireWood）を置き、火が付いtら、FireManagerへ火の獲得依頼をする
///// InitialzieManagerから、FireManager.Initializeの後に一度だけCallする
///// </summary>
//public class FireWoodManager : MonoBehaviour
//{
//    [Header("FireWood")]
//    [Tooltip("焚き火のPrefab。未設定なら火の色を塗った仮のCubeを作成")]
//    [SerializeField] private GameObject _woodPrefab;

//    [Tooltip("焚き木の体のコライダー。判定は敵陣営になる")]
//    [SerializeField] private ColliderSet _colliders = new();

//    private readonly List<FireWood> _woods = new List<FireWood>();  //本体
//    private readonly Queue<FireWood> _burnedQueue = new Queue<FireWood>(); //攻撃判定格納用

//    private FireManager _fireManager;
//    private bool _isInitialized;

//    #region Initialize
//    /// <summary>StageDataのFiresの位置に焚き木を置く</summary>
//    public void Initialize(StageData stage, HitWorld world, FireManager fireManager)
//    {
//        if(stage == null || world == null || fireManager == null)
//        {
//            Debug.Log($"[FireManager] Initialize : Reference is Null!!!");
//            return;
//        }

//        _fireManager = fireManager;
//        IReadOnlyList<FirePlacement> placements = stage.Fires;
//        for(int i = 0;i < placements.Count;i++)
//        {
//            if (placements[i] != null)
//            {
//                Spawn(placements[i], world);
//            }
//        }
//        _isInitialized = true;
//    }
//    #endregion

//    #region UnityLifeCycle
//    private void Update()
//    {
//        if (!_isInitialized) return;

//        while(_burnedQueue.Count > 0)
//        {
//            FireWood wood = _burnedQueue.Dequeue();
//            if (!_fireManager.TryLightFire(wood.Color, wood.Kind, wood.Position))
//            {
//                Debug.LogWarning($"[FireWoodManager] Update : couldn't light fire. color={wood.Color}, kind={wood.Kind}");
//            }
//        }
//    }

//    private void OnDestroy()
//    {
//        for(int i = 0; i < _woods.Count;i++)
//        {
//            if (_woods[i] != null)
//            {
//                _woods[i].OnBurned -= HandleBurned;
//            }
//        }
//    }
//    #region Private
//    private void Spawn(FirePlacement placement, HitWorld world)
//    {
//        GameObject view = CreateView(placement);
//        view.name = $"FireWood_{placement.Color}";

//        FireWood wood = view.AddComponent<FireWood>();
//        wood.Initialize(world, _colliders, placement.Color, placement.Kind, placement.Position);
//        wood.OnBurned += HandleBurned;
//        _woods.Add(wood);
//    }

//    //Prefabがあればそれを、なければ仮のCubeを作る（Colliderは使わない設計）
//    private GameObject CreateView(FirePlacement placement)
//    {
//        if (_woodPrefab != null)
//        {
//            return Instantiate(_woodPrefab, placement.Position, Quaternion.identity, transform);
//        }

//        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//        Destroy(cube.GetComponent<Collider>());
//        cube.transform.SetParent(transform);
//        cube.transform.position = placement.Position + Vector3.up * 0.5f;
//        cube.GetComponent<Renderer>().material.color = TypeManager.ToUnityColor(placement.Color);
//        return cube;
//    }

//    private void HandleBurned(FireWood wood)
//    {
//        _burnedQueue.Enqueue(wood);
//    }
//    #endregion
//}
