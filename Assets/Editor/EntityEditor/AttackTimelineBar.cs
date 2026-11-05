using UnityEngine;
using UnityEngine.UIElements;

public sealed class AttackTimelineBar : VisualElement
{
    private EntityTestResult _result;
    private float _time;

    public AttackTimelineBar()
    {
        style.height = 26;
        style.marginTop = 4;
        style.marginBottom = 4;
        style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
    }

    // 段階ごとの色(凡例でも使う)
    public static Color PhaseColor(AttackPhase phase)
    {
        switch (phase)
        {
            case AttackPhase.Charge: return new Color(1f, 0.8f, 0.2f, 1f);            // 黄: 溜め
            case AttackPhase.Active: return ColliderColors.Attack;                    // 攻撃の色: 有効
            case AttackPhase.Recovery: return new Color(0.6f, 0.4f, 0.9f, 1f);        // 紫: 硬直
            case AttackPhase.Cooldown: return new Color(0.4f, 0.55f, 0.65f, 1f);      // 青灰: クールタイム
            default: return new Color(0.2f, 0.2f, 0.2f, 1f);                          // 暗い灰色: 待機
        }
    }

    public void SetResult(EntityTestResult result)
    {
        _result = result;
        MarkDirtyRepaint();
    }

    public void SetTime(float time)
    {
        _time = time;
        MarkDirtyRepaint();
    }

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var rect = contentRect;
        if (_result == null || _result.Frames.Length < 2 || rect.width < 4f) return;

        var painter = context.painter2D;
        var frames = _result.Frames;
        var count = frames.Length;
        var width = rect.width;
        var height = rect.height;

        // 同じ段階が続く範囲を、まとめて塗る
        var start = 0;
        for (var i = 1; i <= count; i++)
        {
            if (i < count && frames[i].Phase == frames[start].Phase) continue;

            FillRect(painter, width * start / count, 0f, width * i / count, height, PhaseColor(frames[start].Phase));
            start = i;
        }

        // 当たり始めた時刻に、黄色の印
        for (var i = 0; i < count; i++)
        {
            if (!frames[i].Hitting || (i > 0 && frames[i - 1].Hitting)) continue;

            var x = width * i / count;
            FillRect(painter, x - 1.5f, 0f, x + 1.5f, height * 0.45f, ColliderColors.Contact);
        }

        // いまの時刻の縦線
        var cursor = width * Mathf.Clamp01(_time / _result.Duration);
        FillRect(painter, cursor - 1f, 0f, cursor + 1f, height, Color.white);
    }

    private static void FillRect(Painter2D painter, float x0, float y0, float x1, float y1, Color color)
    {
        painter.fillColor = color;
        painter.BeginPath();
        painter.MoveTo(new Vector2(x0, y0));
        painter.LineTo(new Vector2(x1, y0));
        painter.LineTo(new Vector2(x1, y1));
        painter.LineTo(new Vector2(x0, y1));
        painter.ClosePath();
        painter.Fill();
    }
}
