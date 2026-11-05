using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全員のコライダーを、線で表示するデバッグ用コンポーネント（既存の HitDebug を使う）
/// 再生すると、エディタでは自動で作られる（Hierarchy に HitDebugView が出る）。止めたいときは、チェックを外すか削除する
/// Scene ビューに出る。Game ビューで見るときは、Gizmos をオンにする
///
/// 表示の見方:
///   緑          体(Hit)
///   薄い赤橙    攻撃(Attack)。判定していない間
///   濃い赤橙    攻撃(Attack)。判定している間（その Key で判定した直後）
///   黄          攻撃が、実際に当たった。相手の位置に黄色い二重の円と、そこへの線も出る
///   青          索敵(Search)
/// </summary>
public class HitDebugView : MonoBehaviour
{
    [Header("表示する役割")]
    [SerializeField] private bool _showBody = true;
    [SerializeField] private bool _showAttack = true;
    [SerializeField] private bool _showSearch = true;

    [Header("表示の長さ（秒）")]
    [Tooltip("攻撃を判定した後、濃い色で出し続ける時間（判定は短いので、見えるように伸ばす）")]
    [SerializeField, Range(0.05f, 2f)] private float _attackFlashTime = 0.3f;

    [Tooltip("攻撃が当たった後、黄色い印を出し続ける時間")]
    [SerializeField, Range(0.1f, 3f)] private float _hitMarkTime = 0.8f;

    private readonly HashSet<string> _warned = new HashSet<string>();

#if UNITY_EDITOR
    //エディタで再生したとき、自動で1つ作る
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<HitDebugView>() == null)
        {
            new GameObject("HitDebugView").AddComponent<HitDebugView>();
        }
    }
#endif

    private void OnEnable()
    {
        HitDetector.DebugRecord = true;
    }

    private void OnDisable()
    {
        HitDetector.DebugRecord = false;
    }

    private void LateUpdate()
    {
        IReadOnlyList<HitDetector> all = HitDetector.All;
        for (int i = 0; i < all.Count; i++)
        {
            HitDetector detector = all[i];
            if (!detector.IsActive)
            {
                continue;
            }

            WarnMissingKey(detector);
            DrawColliders(detector);
            DrawHitMarks(detector);
        }
    }

    //攻撃が指定した名前（Key）のコライダーを探して、一覧に無かったときの警告（1つの名前につき1回）
    private void WarnMissingKey(HitDetector detector)
    {
        string key = detector.MissingKey;
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        string id = $"{detector.Owner}:{key}";
        if (_warned.Add(id))
        {
            Debug.LogWarning($"[HitDebugView] コライダー {key} が、一覧にありません（Attack1 の Pattern などと、Key が違う）。持ち主 = {detector.Owner}");
        }
    }

    private void DrawColliders(HitDetector detector)
    {
        float now = Time.time;
        IReadOnlyList<ColliderDef> defs = detector.Set.All;

        for (int i = 0; i < defs.Count; i++)
        {
            ColliderDef def = defs[i];
            if (def == null)
            {
                continue;
            }

            Color color;
            switch (def.Role)
            {
                case ColliderRole.Hit:
                    if (!_showBody) continue;
                    color = ColliderColors.Hit;
                    break;

                case ColliderRole.Attack:
                    if (!_showAttack) continue;
                    bool flashing = detector.LastQueryKey == def.Key && now - detector.LastQueryTime <= _attackFlashTime;
                    bool hit = flashing && now - detector.LastHitTime <= _attackFlashTime;
                    color = hit ? ColliderColors.Contact
                          : flashing ? ColliderColors.Attack
                          : WithAlpha(ColliderColors.Attack, 0.3f);
                    break;

                case ColliderRole.Search:
                    if (!_showSearch) continue;
                    color = WithAlpha(ColliderColors.Search, 0.5f);
                    break;

                default:
                    color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    break;
            }

            def.Resolve(detector.Position, detector.Yaw, out Vector3 origin, out Vector3 forward);
            HitDebug.DrawShape(def.Shape, origin, forward, color);
        }
    }

    //攻撃が当たった相手の位置に、黄色い二重の円と、攻撃した側からの線を出す
    private void DrawHitMarks(HitDetector detector)
    {
        if (Time.time - detector.LastHitTime > _hitMarkTime)
        {
            return;
        }

        List<Vector3> positions = detector.LastHitPositions;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 p = positions[i];
            HitDebug.DrawCircle(p, 0.35f, ColliderColors.Contact);
            HitDebug.DrawCircle(p, 0.55f, ColliderColors.Contact);
            Debug.DrawLine(detector.Position, p, ColliderColors.Contact);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }
}
