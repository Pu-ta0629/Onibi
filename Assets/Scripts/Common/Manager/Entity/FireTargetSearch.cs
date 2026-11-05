using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火の攻撃対象の索敵結果。次の2つの重み付き索敵結果を、敵ごとに重みを足して1つにまとめる
/// ・迷子からの索敵結果（迷子が見つけた敵）
/// ・火自身の索敵結果（火が見つけた敵）
/// 両方に見つかった敵は重みが足されるので、狙われやすくなる。結果は重みが大きい順
/// </summary>
public class FireTargetSearch
{
    private static readonly Comparison<SearchResult> CompareByWeight = (a, b) => b.Weight.CompareTo(a.Weight);

    private readonly WeightedSearch<EnemyController> _own;
    private readonly LostChildController _lostChild;
    private readonly List<SearchResult> _results = new List<SearchResult>();

    /// <summary>最後のTickの結果（重みが大きい順）</summary>
    public IReadOnlyList<SearchResult> results => _results;

    public FireTargetSearch(WeightedSearch<EnemyController> own, LostChildController lostChild)
    {
        _own = own;
        _lostChild = lostChild;
    }

    /// <summary>
    /// 火自身の索敵を行い、迷子の結果と合わせる。position: 火の位置
    /// 迷子の結果は、迷子側（UpdateCombat）が更新したものをそのまま読む
    /// </summary>
    public void Tick(Vector3 position)
    {
        _own.Tick(position);

        _results.Clear();
        AddAll(_own.results);
        AddAll(_lostChild.searchResults);
        _results.Sort(CompareByWeight);
    }

    /// <summary>
    /// 一番重みが大きい対象を返す。1件もなければfalse
    /// </summary>
    public bool TryGetTarget(out SearchResult target)
    {
        if (_results.Count == 0)
        {
            target = default;
            return false;
        }

        target = _results[0];
        return true;
    }

    //同じ敵なら重みを足し、初めての敵なら追加する
    private void AddAll(IReadOnlyList<SearchResult> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            SearchResult result = source[i];
            int index = IndexOf(result.Owner);
            if (index < 0)
            {
                _results.Add(result);
                continue;
            }

            SearchResult merged = _results[index];
            merged.Weight += result.Weight;
            _results[index] = merged;
        }
    }

    private int IndexOf(object owner)
    {
        for (int i = 0; i < _results.Count; i++)
        {
            if (ReferenceEquals(_results[i].Owner, owner))
            {
                return i;
            }
        }

        return -1;
    }
}
