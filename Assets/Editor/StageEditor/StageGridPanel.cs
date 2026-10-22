using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class StageGridPanel : VisualElement 
{

    readonly IntegerField _width;
    readonly IntegerField _depth;
    readonly FloatField _cellSize;
    readonly Vector3Field _origin;

    StageData _stage;
    
    public StageGridPanel()
    {
        style.paddingLeft = 6;
        style.paddingRight = 6;
        style.paddingTop = 4;
        style.paddingBottom = 4;

        var title = new Label("Walkable Grid");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        Add(title);

        var tool = new EnumField("Paint Tool", StageGridEditor.CurrentTool);

        tool.RegisterValueChangedCallback(e =>
        {
            StageGridEditor.CurrentTool = (StageGridEditor.PaintTool)e.newValue;
            SceneView.RepaintAll();
        });
        Add(tool);

        var brush = new SliderInt("Brush Size", 1, 5) { value = StageGridEditor.BrushSize, showInputField = true };
        brush.RegisterValueChangedCallback(e => StageGridEditor.BrushSize = e.newValue);
        Add(brush);

        _width = new IntegerField("Width(cells)");
        _depth = new IntegerField("Depth(cells)");
        _cellSize = new FloatField("Cell Size");
        _origin = new Vector3Field("Origin");
        Add(_width);
        Add(_depth);
        Add(_cellSize);
        Add(_origin);
        Add(new Button(Apply) { text = "Apply Grid Setting" });
    }

    public void SetTarget(StageData stage)
    {
        _stage = stage;
        SetEnabled(stage != null);
        if (stage == null) return;

        var grid = stage.WalkableGrid;
        _width.SetValueWithoutNotify(grid.Width);
        _depth.SetValueWithoutNotify(grid.Depth);
        _cellSize.SetValueWithoutNotify(grid.CellSize);
        _origin.SetValueWithoutNotify(grid.Origin);
    }

    private void Apply()
    {
        if (_stage == null) return;
        StageGridEditor.ApplySettings(_stage, _width.value, _depth.value, _cellSize.value, _origin.value);
        SetTarget(_stage);
    }
}
