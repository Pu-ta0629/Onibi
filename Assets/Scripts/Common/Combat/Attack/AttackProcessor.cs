using System.Collections.Generic;

/// <summary>
/// ダメージを受けられるもの（敵・迷子）。攻撃処理は、この口に対してダメージを渡す
/// </summary>
public interface IHitable
{
    void TakeDamage(int damage);
}

/// <summary>
/// 攻撃処理。当たり判定クラス（HitDetector）から受け取った情報（HitInfo）をもとに、ダメージを与える
/// 1回の攻撃（Begin〜）の間は、同じ相手に2回当てない
/// </summary>
public class AttackProcessor
{
    private readonly List<object> _hitOwners = new List<object>();

    /// <summary>
    /// 新しい攻撃の開始時に呼ぶ（当てた相手の記録を消す）
    /// </summary>
    public void Begin()
    {
        _hitOwners.Clear();
    }

    /// <summary>
    /// 当たった相手のうち、まだ当てていないIHitableへダメージを与える。与えた数を返す
    /// </summary>
    public int Apply(IReadOnlyList<HitInfo> hits, int damage)
    {
        int count = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            object owner = hits[i].Owner;
            if (!(owner is IHitable target) || _hitOwners.Contains(owner))
            {
                continue;
            }

            _hitOwners.Add(owner);
            target.TakeDamage(damage);
            count++;
        }

        return count;
    }
}
