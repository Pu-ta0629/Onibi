using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 木霊1つ分。動かない。毎フレーム、接触の判定(Search)に迷子が入っていないかを調べる
/// 世界に体は登録しない。見た目はFactoryが差し込む
/// </summary>
public class KodamaController : MonoBehaviour
{
    private readonly List<ColliderDef> _touchDefs = new ();
    private readonly List<HitBody> _hits = new();

    private HitWorld _world;
    private KodamaData _data;
    private GameObject _visual;
    private ParticleSystem[] _systems;

    public KodamaData Data => _data;
    public bool IsActive { get; private set; }

    /// <summary>Factoryから、一度だけ呼ぶ</summary>
    public void Initialize(KodamaData data,GameObject visual,HitWorld world)
    {
        _data = data;
        _visual = visual;
        _world = world;
        _systems = visual.GetComponentsInChildren<ParticleSystem>(true);

        _touchDefs.Clear();
        data.Colliders.Collect(ColliderRole.Search, _touchDefs);

        if(_touchDefs.Count == 0)
        {
            Debug.LogError($"[KodamaController] Initialize : {data.name} has no Search collider!!!");
        }
    }

    ///<summary>木霊をステージに出す。接触の判定がなければFalse</summary>
    public bool Spawn(Vector3 position)
    {
        if(_touchDefs.Count == 0)
        {
            return false;
        }

        transform.position = position;
        gameObject.SetActive(true);

        for(int i = 0;i < _systems.Length;i++)
        {
            _systems[i].Clear(true);
            _systems[i].Play(true);
        }

        IsActive = true;
        return true;
    }

    ///<summary>木霊をステージから外す(取得されたときや、強制回収の時に、PoolManagerが呼ぶ)</summary>
    public void Despawn()
    {
        IsActive = false;

        for(int i = 0;i < _systems.Length;i++)
        {
            _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 接触の判定に入っている迷子を探す。いれば、その迷子を返す
    /// </summary>
    public bool TryDetectToucher(out LostChildController toucher)
    {
        toucher = null;
        if(!IsActive)
        {
            return false;
        }

        for (int i = 0; i < _touchDefs.Count; i++)
        {
            _hits.Clear();
            _touchDefs[i].Query(_world,transform.position,0f,_hits);

            for (int j = 0; j < _hits.Count; j++)
            {
                if (_hits[j].Owner is LostChildController child)
                {
                    toucher = child;
                    return true;
                }
            }
        }
        return false;
    }
#if UNITY_EDITOR

    // 接触範囲(Search)を、Sceneビューに描く。出ている木霊だけ。Gameビューでは、右上のGizmosをONにすると見える
    // 形は、木霊の位置を原点に、向きは回転0(前方=+Z)として描く
    private void OnDrawGizmos()
    {
        if (!IsActive)
        {
            return;
        }

        Vector3 origin = transform.position;

        // 木霊の位置の目印
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        for (int i = 0; i < _touchDefs.Count; i++)
        {
            DrawShape(origin, _touchDefs[i].Shape);
        }
    }

    private static void DrawShape(Vector3 origin, ColliderShape shape)
    {
        switch (shape.Kind)
        {
            case ColliderShapeKind.Circle:
                DrawArc(origin, shape.Radius, 0f, 360f);
                if (shape.InnerRange > 0f)
                {
                    DrawArc(origin, shape.InnerRange, 0f, 360f);
                }
                break;

            case ColliderShapeKind.Sector:
                float half = shape.Angle * 0.5f;
                DrawArc(origin, shape.Radius, -half, half);
                if (shape.InnerRange > 0f)
                {
                    DrawArc(origin, shape.InnerRange, -half, half);
                }
                if (shape.Angle < 360f)
                {
                    Vector3 left = Quaternion.Euler(0f, -half, 0f) * Vector3.forward;
                    Vector3 right = Quaternion.Euler(0f, half, 0f) * Vector3.forward;
                    Gizmos.DrawLine(origin + left * shape.InnerRange, origin + left * shape.Radius);
                    Gizmos.DrawLine(origin + right * shape.InnerRange, origin + right * shape.Radius);
                }
                break;

            case ColliderShapeKind.Box:
                {
                    // 原点から前方に Length、左右に Width の、四角と仮定
                    float w = shape.Width * 0.5f;
                    Vector3 a = origin + new Vector3(-w, 0f, 0f);
                    Vector3 b = origin + new Vector3(w, 0f, 0f);
                    Vector3 c = origin + new Vector3(w, 0f, shape.Length);
                    Vector3 d = origin + new Vector3(-w, 0f, shape.Length);
                    
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(b, c);
                    Gizmos.DrawLine(c, d);
                    Gizmos.DrawLine(d, a);
                    break;
                }

            case ColliderShapeKind.Capsule:
                {
                    // 原点から前方に Length の線を、半径 Radius でふくらませた形と仮定
                    Vector3 end = origin + Vector3.forward * shape.Length;
                    DrawArc(origin, shape.Radius, 0f, 360f);
                    DrawArc(end, shape.Radius, 0f, 360f);
                    Gizmos.DrawLine(origin + Vector3.left * shape.Radius, end + Vector3.left * shape.Radius);
                    Gizmos.DrawLine(origin + Vector3.right * shape.Radius, end + Vector3.right * shape.Radius);
                    break;
                }
        }
    }

    // XZ平面に円弧を描く。角度は、前方(+Z)から、Y軸まわりの度数
    private static void DrawArc(Vector3 center, float radius, float startDeg, float endDeg)
    {
        const int Segments = 32;
        Vector3 previous = center + Quaternion.Euler(0f, startDeg, 0f) * Vector3.forward * radius;
        for (int i = 1; i <= Segments; i++)
        {
            float deg = Mathf.Lerp(startDeg, endDeg, i / (float)Segments);
            Vector3 next = center + Quaternion.Euler(0f, deg, 0f) * Vector3.forward * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
