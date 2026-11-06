using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 索敵で見つけた1件分の結果。Weightが大きいほど、優先して狙う
/// </summary>
public struct SearchResult
{
    public object Owner;
    public Vector3 Position;
    public float Weight;
}

/// <summary>
/// 重み付き索敵。Searchの役割のコライダーで見つけた相手に、起点からの近さで重みを振る
/// 重み = 1 / (1 + 距離)。近いほど大きく、結果は重みが大きい順に並ぶ
/// T: 見つける相手の種類（EnemyControllerなど）
/// </summary>
public class WeightedSearch<T> where T : class
{
    private static readonly Comparison<SearchResult> CompareByWeight = (a, b) => b.Weight.CompareTo(a.Weight);

    private readonly HitDetector _detector;
    private readonly List<HitInfo> _hits = new List<HitInfo>();
    private readonly List<SearchResult> _results = new List<SearchResult>();

    /// <summary>最後のTickの結果（重みが大きい順）</summary>
    public IReadOnlyList<SearchResult> results => _results;

    public WeightedSearch(HitDetector detector)
    {
        _detector = detector;
    }

    /// <summary>
    /// 索敵して、重みを振り直す。origin: 重みを計算する起点（自分の位置）
    /// 呼ぶ前に、HitDetectorのSetTransformで位置を更新しておく
    /// </summary>
    public void Tick(Vector3 origin)
    {
        _results.Clear();
        _detector.Detect<T>(ColliderRole.Search, _hits);

        for (int i = 0; i < _hits.Count; i++)
        {
            //高さは無視して、XZ平面での距離で重みを決める
            Vector3 delta = _hits[i].Position - origin;
            delta.y = 0f;

            _results.Add(new SearchResult
            {
                Owner = _hits[i].Owner,
                Position = _hits[i].Position,
                Weight = 1f / (1f + delta.magnitude),
            });
        }

        _results.Sort(CompareByWeight);
    }
}
