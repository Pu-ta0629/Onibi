using UnityEngine;

/// <summary>
/// 火の色(FireColor)ごとのEffectの色。FireManagerが1つだけ受け取る
/// Effect本体(Prefab)は、状態ごとにFireStateData.EffectPrefabが持つ
/// </summary>
[CreateAssetMenu(menuName = "Game/FireEffectCatalog", fileName = "FireEffectCatalog")]
public class FireEffectCatalog : ScriptableObject
{
    [Tooltip("FireColorごとの色(FireColorData)")]
    [SerializeField] private FireEffectColorData[] _colors;

    public bool TryGetGradient(FireColor color, out Gradient gradient)
    {
        if (_colors != null)
        {
            for (int i = 0; i < _colors.Length; i++)
            {
                if (_colors[i] != null && _colors[i].Color == color)
                {
                    gradient = _colors[i].Gradient;
                    return true;
                }
            }
        }

        gradient = null;
        return false;
    }
}