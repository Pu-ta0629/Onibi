using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 火の進化・退化を管理する純粋なC#クラス
/// 「今の木霊量と、その火が持つ特殊木霊で、なれる最上位の形」を毎回求める
///   木霊が増えれば進化、減れば退化になる（自爆で木霊が0になれば、種火まで戻る）
/// 今の形と違えば、TypeManagerへ遷移を申請する。認可されたら、FireControllerへ新しいFireStateDataを渡す（ChangeFire）
/// 判定は毎フレームではなく、木霊量が変わったときと、特殊木霊を取得したときだけ（次のTickでまとめて行う）
/// </summary>
public class MorphManager : IDisposable
{
    private readonly TypeManager _typeManager;
    private readonly FireRegistry _registry;
    private readonly FireStateCatalog _catalog;

    private bool _dirty = true;   //開始時の木霊量でも判定する

    public MorphManager(TypeManager typeManager, FireRegistry registry, FireStateCatalog catalog)
    {
        _typeManager = typeManager;
        _registry = registry;
        _catalog = catalog;

        _registry.OnKodamaChanged += MarkDirty;          //木霊は火ごとに持つ。どれかの火の木霊が変わったら再判定する
        _registry.OnSpecialKodamaChanged += MarkDirty;   //特殊木霊が変わったら（Inspectorでの変更を含む）再判定する
    }

    #region Public
    /// <summary>毎フレーム呼ぶ。再判定が必要なときだけ、全ての火を判定する</summary>
    public void Tick()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        for (int i = 0; i < _registry.all.Count; i++)
        {
            TryMorph(_registry.all[i].Color);
        }
    }

    /// <summary>次のTickで、全ての火を判定し直す（特殊木霊の取得・火の獲得のときに呼ぶ）</summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>火1つの進化・退化を試す。木霊量に合わせて、落ち着く形まで連続で進む</summary>
    public bool TryMorph(FireColor color)
    {
        bool morphed = false;
        while (TryMorphOnce(color))
        {
            morphed = true;
        }

        return morphed;
    }

    public void Dispose()
    {
        _registry.OnKodamaChanged -= MarkDirty;
        _registry.OnSpecialKodamaChanged -= MarkDirty;
    }
    #endregion

    #region Private
    private bool TryMorphOnce(FireColor color)
    {
        if (!_registry.TryGet(color, out FireController fire))
        {
            return false;
        }

        FireState state = _typeManager.GetFireState(color);
        FireStateData target = Resolve(fire.Kodama != null ? fire.Kodama.count : 0, fire.SpecialKodama);   //この火が持つ木霊量・特殊木霊で判定する
        if (target == null || target.FireKind == state.Kind)
        {
            return false;
        }

        //TypeManagerが遷移可能かを判定して、許可したときだけStateが変わる
        if (!_typeManager.TryMorph(color, target.FireKind, fire.SpecialKodama))
        {
            return false;
        }

        //認可されたら、火のデータと見た目を入れ替える
        fire.ChangeData(target);
        _typeManager.ChangeBrightness(color, target.InitialBrightness);
        Debug.Log($"[MorphManager] ChangeFire : {color} {state.Kind} -> {target.FireKind}");
        return true;
    }

    //今の木霊量と特殊木霊で、なれる最上位の形を返す。特殊な形を優先し、同じなら必要な木霊量が多い方
    private FireStateData Resolve(int kodamaCount, SpecialKodamaKind special)
    {
        FireStateData best = null;
        IReadOnlyList<FireStateData> all = _catalog.All;
        if (all == null)
        {
            return null;
        }

        for (int i = 0; i < all.Count; i++)
        {
            FireStateData data = all[i];
            if (data == null || data.KodamaCount > kodamaCount)
            {
                continue;
            }

            if (data.RequiredSpecial != SpecialKodamaKind.None && data.RequiredSpecial != special)
            {
                continue;
            }

            if (best == null || IsHigherPriority(data, best))
            {
                best = data;
            }
        }

        return best;
    }

    private static bool IsHigherPriority(FireStateData a, FireStateData b)
    {
        bool aSpecial = a.RequiredSpecial != SpecialKodamaKind.None;
        bool bSpecial = b.RequiredSpecial != SpecialKodamaKind.None;
        if (aSpecial != bSpecial)
        {
            return aSpecial;
        }

        return a.KodamaCount > b.KodamaCount;
    }
    #endregion

}