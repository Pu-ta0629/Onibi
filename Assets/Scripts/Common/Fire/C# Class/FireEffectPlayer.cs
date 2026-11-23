using UnityEngine;

/// <summary>
/// 火1つ分のEffectを管理する純粋なC#クラス。FireControllerが持つ
/// 進化・退化のたびに、古いEffectを捨てて、新しい状態のEffectを生成し、火の色のGradientで着色する
/// (進化は1つの火につき数回なので、プールやキャッシュは持たない)
/// </summary>
public class FireEffectPlayer
{
    private readonly Transform _parent;
    private readonly FireColor _color;
    private readonly FireEffectCatalog _catalog;
    private ParticleSystem _current;

    public FireEffectPlayer(Transform parent, FireColor color, FireEffectCatalog catalog)
    {
        _parent = parent;
        _color = color;
        _catalog = catalog;
    }

    /// <summary>古いEffectを捨てて、データのEffectを生成して、色を着ける</summary>
    public void Apply(FireStateData data)
    {
        if (data == null || data.EffectPrefab == null)
        {
            Debug.LogWarning($"[FireEffectPlayer] EffectPrefab is not set. color = {_color}, kind = {(data != null ? data.FireKind.ToString() : "null")}");
            return;
        }

        if (_current != null)
        {
            Object.Destroy(_current.gameObject);
        }

        _current = Object.Instantiate(data.EffectPrefab, _parent);
        _current.transform.localPosition = Vector3.zero;
        Tint(_current);
        Debug.Log($"[FireEffectPlayer] Apply : color = {_color}, kind = {data.FireKind}, prefab = {data.EffectPrefab.name}, parent = {_parent.name}");
    }

    private void Tint(ParticleSystem effect)
    {
        if (_catalog == null || !_catalog.TryGetGradient(_color, out Gradient gradient))
        {
            Debug.LogWarning($"[FireEffectPlayer] Gradient not found. color = {_color}");
            return;
        }

        ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.ColorOverLifetimeModule module = systems[i].colorOverLifetime;
            module.enabled = true;
            module.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}