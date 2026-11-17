using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全てのFireStateDataの一覧。MorphManagerが「今の木霊量でなれる形」を探すのに使う
/// (AssetDatabaseはEditor専用で実行時に使えないため、Inspectorで一覧を持つ)
/// </summary>
[CreateAssetMenu(menuName = "Game/FireStateCatalog", fileName = "FireStateCatalog")]
public class FireStateCatalog : ScriptableObject
{
    [Tooltip("種火~遺念火、狐火・隠火・鬼火のFireStateDataを全て入れる")]
    [SerializeField] private FireStateData[] _states;

    public IReadOnlyList<FireStateData> All => _states;

    public bool TryGet(FireKind kind, out FireStateData data)
    {
        if (_states != null)
        {
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] != null && _states[i].FireKind == kind)
                {
                    data = _states[i];
                    return true;
                }
            }
        }

        data = null;
        return false;
    }
}