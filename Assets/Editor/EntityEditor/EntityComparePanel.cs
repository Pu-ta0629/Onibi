using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 重ねて比較する欄。ほかの Entity を選ぶと、そのコライダーが、図に、薄い点線で重なる
public sealed class EntityComparePanel : VisualElement
{
    public const int MaxEntries = 5;   // 重ねられる数(点の数で区別するため)

    private readonly ColliderCanvas _canvas;
    private readonly List<CompareEntry> _active = new List<CompareEntry>();
    private readonly VisualElement _legend = new VisualElement();
    private readonly Label _message = new Label();

    public EntityComparePanel(ColliderCanvas canvas, IReadOnlyList<CompareEntry> candidates)
    {
        _canvas = canvas;

        style.marginTop = 8;
        style.paddingLeft = 6;
        style.paddingRight = 6;

        var foldout = new Foldout { text = "重ねて比較(ほかの Entity のコライダーを、薄い点線で重ねる)", value = false };
        Add(foldout);

        if (candidates.Count == 0)
        {
            foldout.Add(new Label("比較できる、ほかの Entity が、ありません"));
            return;
        }

        var list = new ScrollView();
        list.style.maxHeight = 140;
        foreach (var candidate in candidates)
        {
            var entry = candidate;
            var toggle = new Toggle(entry.Label);
            toggle.RegisterValueChangedCallback(evt => OnToggle(toggle, entry, evt.newValue));
            list.Add(toggle);
        }

        foldout.Add(list);
        foldout.Add(_message);
        foldout.Add(_legend);

        canvas.SetCompare(_active);
        RegisterCallback<DetachFromPanelEvent>(_ => canvas.SetCompare(null));
    }

    private void OnToggle(Toggle toggle, CompareEntry entry, bool on)
    {
        if (on)
        {
            if (_active.Count >= MaxEntries)
            {
                toggle.SetValueWithoutNotify(false);
                _message.text = $"重ねられるのは、{MaxEntries} つまでです";
                return;
            }

            _active.Add(entry);
        }
        else
        {
            _active.Remove(entry);
        }

        _message.text = string.Empty;
        RebuildLegend();
        _canvas.MarkDirtyRepaint();
    }

    // 凡例: 点の数と、Entity の名前(図の上の、輪郭の外側の端にある点の数と、対応する)
    private void RebuildLegend()
    {
        _legend.Clear();

        for (var i = 0; i < _active.Count; i++)
            _legend.Add(new Label($"{new string('●', i + 1)}   {_active[i].Label}"));
    }
}