using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// コライダーを見下ろす形
/// 縦が前後、横が左右
/// </summary>
public sealed class ColliderCanvas : VisualElement
{
    private const int CircleSegments = 48;
    private const int ArcSegments = 24;

    //格子の間隔の候補
    private static readonly float[] GridSteps = { 2f, 5f, 20f, 100f };

    private Func<ColliderSet> _getSet;
    private SerializedObject _serializedObject;
    private string _collidersPath;
    private int _selected = -1;
    private HandleKind? _dragKind;
    private float _frozenScale;
    private int _undoGroup;
    private readonly List<HandleInfo> _handles = new();
    private readonly Label _info;

    private const float HandleRadius = 9f;
    private ColliderSet CurrentSet => _getSet?.Invoke();

    private float _zoom = 1f;
    private Vector2 _center;
    private float _scale = 1f;

    public ColliderCanvas()
    {
        style.minHeight = 240;
        style.overflow = Overflow.Hidden;
        style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);

        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        RegisterCallback<WheelEvent>(OnWheel);

        _info = new Label();
        _info.pickingMode = PickingMode.Ignore;
        _info.style.position = Position.Absolute;
        _info.style.left = 6;
        _info.style.top = 6;
        _info.style.paddingLeft = 6;
        _info.style.paddingRight = 6;
        _info.style.paddingTop = 2;
        _info.style.paddingBottom = 2;
        _info.style.color = Color.white;
        _info.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        _info.style.display = DisplayStyle.None;
        Add(_info);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    public void SetColliders(Func<ColliderSet> getSet)
    {
        _getSet = getSet;
        _selected = -1;
        MarkDirtyRepaint();
    }

    public void SetEditTarget(SerializedObject serializedObject,string collidersPath)
    {
        _serializedObject = serializedObject;
        _collidersPath = collidersPath;
    }

    public void OnWheel(WheelEvent evt)
    {
        _zoom = Mathf.Clamp(_zoom * (evt.delta.y > 0f ? 0.9f : 1.1f), 0.2f, 8f);
        MarkDirtyRepaint();
        evt.StopPropagation();
    }


    //--- 描画 ---
    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var rect = contentRect;
        if (rect.width < 10f || rect.height < 10f) return;

        var painter = context.painter2D;
        _center = rect.center;

        // ドラッグ中は、拡大率を固定する(形が変わっても、図が勝手に拡縮しないように)
        _scale = _dragKind.HasValue
            ? _frozenScale
            : Mathf.Min(rect.width, rect.height) * 0.45f / GetExtent() * _zoom;

        DrawGrid(painter, rect);
        DrawEntityMarker(painter);

        var set = CurrentSet;
        if (set == null) return;

        for (var i = 0; i < set.All.Count; i++)
        {
            var def = set.All[i];
            if (def != null) DrawCollider(painter, def, i == _selected);
        }

        DrawHandles(painter);
    }

    private float GetExtent()
    {
        var extent = 2f;

        var set = CurrentSet;
        if (set == null) return extent;

        foreach (var def in set.All)
        {
            if (def == null) continue;
            extent = Mathf.Max(extent, def.Offset.magnitude + def.Shape.BoundingRadius);
        }
        return extent;
    }

    private void DrawGrid(Painter2D painter,Rect rect)
    {
        var step = GridSteps[GridSteps.Length - 1];
        foreach(var candidate in GridSteps)
        {
            if(candidate * _scale >= 16f)
            {
                step = candidate;
                break;
            }
        }

        float halfWidth = rect.width * 0.5f / _scale;
        float halfHeight = rect.height * 0.5f / _scale;
        var countX = Mathf.CeilToInt(halfWidth / step);
        var countY = Mathf.CeilToInt(halfHeight / step);

        var faint = new Color(1f, 1f, 1f, 0.08f);
        var axis = new Color(1f, 1f, 1f, 0.3f);

        for(var i = -countX;i<= countX;i++)
        {
            var x = i * step;
            DrawLine(painter, ToScreen(new Vector2(x, -halfHeight)), ToScreen(new Vector2(x, halfHeight)), i == 0 ? axis : faint, 1f);
        }

        for (var j = -countY; j <= countX; j++)
        {
            var z = j * step;
            DrawLine(painter, ToScreen(new Vector2(-halfWidth, z)), ToScreen(new Vector2(halfWidth, z)), j == 0 ? axis : faint, 1f);
        }
    }

    private void DrawEntityMarker(Painter2D painter)
    {
        var gray = new Color(0.8f, 0.8f, 0.8f, 1f);

        painter.lineWidth = 1.5f;
        painter.strokeColor = gray;
        painter.fillColor = new Color(gray.r,gray.g,gray.b,0.5f);
        painter.BeginPath();
        for(int i = 0;i <= 16;i++)
        {
            float angle = i / 16f * Mathf.PI * 2f;
            var point = _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f;
            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();

        DrawLine(painter, _center, _center + new Vector2(0f, -18f),gray, 1.5f);
    }

    private void DrawCollider(Painter2D painter, ColliderDef def, bool selected)
    {
        // 起点と向きは、ゲーム中と同じ計算(ColliderDef.Resolve)を使う。Entity は、原点で、前を向いている
        def.Resolve(Vector3.zero, 0f, out var origin3, out var forward3);

        var origin = new Vector2(origin3.x, origin3.z);
        var forward = new Vector2(forward3.x, forward3.z);
        var right = new Vector2(forward.y, -forward.x);
        var color = ColliderColors.Of(def.Role);

        // 選んでいるものは、塗りを濃く、輪郭を白く太くする
        var fill = selected ? new Color(color.r, color.g, color.b, 0.4f) : ColliderColors.Fill(color);
        var stroke = selected ? Color.white : color;

        DrawLoops(painter, BuildLoops(def.Shape, origin, forward, right), fill, stroke, selected ? 3f : 2f);
        DrawRoleMarker(painter, def.Role, ToScreen(origin), ToScreenDirection(forward), color);
    }

    private static List<List<Vector2>> BuildLoops(ColliderShape shape, Vector2 origin, Vector2 forward, Vector2 right)
    {
        var loops = new List<List<Vector2>>();

        switch (shape.Kind)
        {
            case ColliderShapeKind.Circle:
                AddRing(loops, origin, shape, forward, right);
                break;
            case ColliderShapeKind.Capsule:
                {
                    var half = forward * (shape.Length * 0.5f);
                    var loop = Arc(origin + half, shape.Radius, forward, right, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, ArcSegments);
                    loop.AddRange(Arc(origin - half, shape.Radius, forward, right, Mathf.PI * 0.5f, Mathf.PI * 1.5f, ArcSegments));
                    loops.Add(loop);
                    break;
                }
            case ColliderShapeKind.Box:
                {
                    var hl = forward * (shape.Length * 0.5f);
                    var hw = right * (shape.Width * 0.5f);
                    loops.Add(new List<Vector2>
                    {
                        origin + hl - hw,
                        origin + hl + hw,
                        origin - hl + hw,
                        origin - hl - hw,
                    });
                    break;
                }

            case ColliderShapeKind.Sector:
                {
                    if(shape.Angle >= 360f)
                    {
                        AddRing(loops, origin, shape, forward, right);
                        break;
                    }

                    var half = shape.Angle * 0.5f * Mathf.Deg2Rad;
                    var loop = new List<Vector2>();
                    if (shape.InnerRange <= 0f) loop.Add(origin);
                    loop.AddRange(Arc(origin, shape.Radius, forward, right, -half, half, ArcSegments));
                    if (shape.InnerRange > 0f)
                        loop.AddRange(Arc(origin, shape.InnerRange, forward, right, half, -half, ArcSegments));
                    loops.Add(loop);
                    break;
                }
        }
        return loops;
    }

    private static void AddRing(List<List<Vector2>> loops, Vector2 origin,ColliderShape shape,Vector2 forward,Vector2 right)
    {
        loops.Add(Arc(origin, shape.Radius, forward, right, 0f, Mathf.PI * 2f, CircleSegments));
        if(shape.InnerRange > 0f)
            loops.Add(Arc(origin,shape.InnerRange,forward,right,0f,Mathf.PI *2f, CircleSegments));
    }

    private static List<Vector2> Arc(Vector2 center,float radius,Vector2 forward,Vector2 right,float from,float to,int segments)
    {
        var points = new List<Vector2>(segments + 1);
        for(var i = 0;i <= segments;i++)
        {
            var angle = Mathf.Lerp(from, to, i / (float)segments);
            points.Add(center + radius * (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)));
        }
        return points;
    }

    private void DrawLoops(Painter2D painter, List<List<Vector2>> loops, Color fill, Color stroke, float width)
    {
        painter.lineWidth = width;
        painter.strokeColor = stroke;
        painter.fillColor = fill;

        painter.BeginPath();
        foreach (var loop in loops)
        {
            if (loop.Count < 2) continue;

            painter.MoveTo(ToScreen(loop[0]));
            for (var i = 1; i < loop.Count; i++) painter.LineTo(ToScreen(loop[i]));
            painter.ClosePath();
        }
        painter.Fill(FillRule.OddEven);
        painter.Stroke();
    }

    private static void DrawRoleMarker(Painter2D painter,ColliderRole role,Vector2 position,Vector2 direction,Color color)
    {
        var side = new Vector2(-direction.y, direction.x);

        switch(role)
        {
            case ColliderRole.Hit:
                DrawLine(painter, position - new Vector2(6f, 0f), position + new Vector2(6f, 0f), color, 1.5f);
                DrawLine(painter, position - new Vector2(0f, 6f), position + new Vector2(0f, 6f), color, 1.5f);
                break;

            case ColliderRole.Attack:
                {
                    var tip = position + direction * 26f;
                    DrawLine(painter, position, tip, color, 2f);
                    DrawLine(painter, tip, tip - direction * 8f + side * 5f, color, 2f);
                    DrawLine(painter, tip, tip - direction * 8f - side * 5f, color, 2f);
                    break;
                }
            case ColliderRole.Search:
                {
                    var up = new Vector2(0f, -7f);
                    var rightPoint = new Vector2(7f, 0f);
                    DrawLine(painter, position + up, position + rightPoint, color, 1.5f);
                    DrawLine(painter, position + rightPoint, position - up, color, 1.5f);
                    DrawLine(painter, position - up, position - rightPoint, color, 1.5f);
                    DrawLine(painter, position - rightPoint, position + up, color, 1.5f);
                    break;
                }
        }
    }

    private static void DrawLine(Painter2D painter,Vector2 from,Vector2 to,Color color,float width)
    {
        painter.lineWidth = width;
        painter.strokeColor = color;
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }

    private Vector2 ToScreen(Vector2 world)
    {
        return new Vector2(_center.x + world.x * _scale,_center.y - world.y * _scale);
    }

    private static Vector2 ToScreenDirection(Vector2 world)
    {
        var direction = new Vector2(world.x,world.y);
        return direction.sqrMagnitude < 1e-6f ? new Vector2(0f, -1f) : direction.normalized;
    }

    //---- ドラッグ編集 ----

    private ColliderDef SelectedDef()
    {
        var set = CurrentSet;
        if (set == null || _selected < 0 || _selected >= set.All.Count) return null;
        return set.All[_selected];
    }

    private void DrawHandles(Painter2D painter)
    {
        var def = SelectedDef();
        if (def == null) return;

        ColliderHandles.Collect(def, ToScreen, _scale, _handles);
        var color = ColliderColors.Of(def.Role);

        foreach (var handle in _handles)
            DrawHandle(painter, handle.ScreenPosition, color, _dragKind == handle.Kind);
    }

    private static void DrawHandle(Painter2D painter, Vector2 position, Color color, bool active)
    {
        const float half = 5f;

        painter.lineWidth = 2f;
        painter.strokeColor = color;
        painter.fillColor = active ? color : Color.white;
        painter.BeginPath();
        painter.MoveTo(position + new Vector2(-half, -half));
        painter.LineTo(position + new Vector2(half, -half));
        painter.LineTo(position + new Vector2(half, half));
        painter.LineTo(position + new Vector2(-half, half));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || _serializedObject == null) return;

        var set = CurrentSet;
        if (set == null) return;

        var position = (Vector2)evt.localPosition;

        // 1. 選んでいるコライダーの、ハンドルを掴む(いちばん近いものを選ぶ)
        var def = SelectedDef();
        if (def != null)
        {
            ColliderHandles.Collect(def, ToScreen, _scale, _handles);

            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _handles.Count; i++)
            {
                var distance = Vector2.Distance(_handles[i].ScreenPosition, position);
                if (distance <= HandleRadius && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best >= 0)
            {
                BeginDrag(_handles[best].Kind, evt.pointerId);
                evt.StopPropagation();
                return;
            }
        }

        // 2. コライダーの中を押したら、そのコライダーを選ぶ(後ろに描かれているものが、上にある)
        var world = ToWorld(position);
        _selected = -1;
        for (var i = set.All.Count - 1; i >= 0; i--)
        {
            var candidate = set.All[i];
            if (candidate != null && ContainsPoint(candidate, world))
            {
                _selected = i;
                break;
            }
        }

        MarkDirtyRepaint();
        evt.StopPropagation();
    }

    private void BeginDrag(HandleKind kind, int pointerId)
    {
        _dragKind = kind;
        _frozenScale = _scale;
        this.CapturePointer(pointerId);

        // ドラッグ全体を、元に戻す操作の1回分にまとめる
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Edit Collider");
        _undoGroup = Undo.GetCurrentGroup();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_dragKind.HasValue || !this.HasPointerCapture(evt.pointerId)) return;

        var def = SelectedDef();
        if (def == null || _serializedObject == null) return;

        _serializedObject.Update();
        var list = _serializedObject.FindProperty(_collidersPath);
        if (list == null || _selected >= list.arraySize) return;

        var snap = evt.ctrlKey || evt.commandKey;
        ColliderHandles.Drag(_dragKind.Value, def, list.GetArrayElementAtIndex(_selected), ToWorld((Vector2)evt.localPosition), snap);
        _serializedObject.ApplyModifiedProperties();

        // 値の表示(書き込み後の値を、取り直して出す)
        def = SelectedDef();
        if (def != null)
        {
            _info.text = ColliderHandles.Describe(_dragKind.Value, def);
            _info.style.display = DisplayStyle.Flex;
        }

        MarkDirtyRepaint();
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_dragKind.HasValue) return;

        this.ReleasePointer(evt.pointerId);
        EndDrag();
        evt.StopPropagation();
    }

    // ウィンドウの外へ出るなどで、ポインターの捕捉が外れたときも、ドラッグを終える
    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (_dragKind.HasValue) EndDrag();
    }

    private void EndDrag()
    {
        _dragKind = null;
        _info.style.display = DisplayStyle.None;
        Undo.CollapseUndoOperations(_undoGroup);
        MarkDirtyRepaint();
    }

    // 画面の位置を、世界の座標(x は右、y は前。m)に直す
    private Vector2 ToWorld(Vector2 screen)
    {
        return new Vector2((screen.x - _center.x) / _scale, -(screen.y - _center.y) / _scale);
    }

    private static bool ContainsPoint(ColliderDef def, Vector2 point)
    {
        def.Resolve(Vector3.zero, 0f, out var origin3, out var forward3);
        var origin = new Vector2(origin3.x, origin3.z);
        var forward = new Vector2(forward3.x, forward3.z);
        var right = new Vector2(forward.y, -forward.x);

        var inside = false;
        foreach (var loop in BuildLoops(def.Shape, origin, forward, right))
        {
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var a = loop[i];
                var b = loop[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
        }
        return inside;
    }
}
