using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class StageGridPanel : VisualElement 
{

    private readonly IntegerField _width;
    private readonly IntegerField _depth;
    private readonly FloatField _cellSize;
    private readonly Vector3Field _origin;

    private StageData _stage;
    
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

        var layer = new EnumField("Layer", StageGridEditor.CurrentLayer);
        var terrain = new EnumField("Terrain", StageGridEditor.PaintTerrain);
        var factions = new EnumFlagsField("Block Factions",StageGridEditor.PaintFactions);

        void RefreshLayerUi()
        {
            var isTerrain = StageGridEditor.CurrentLayer == StageGridEditor.PaintLayer.Terrain;
            terrain.style.display = isTerrain ? DisplayStyle.Flex : DisplayStyle.None;
            factions.style.display = isTerrain ? DisplayStyle.None : DisplayStyle.Flex;
            SceneView.RepaintAll();
        }

        layer.RegisterValueChangedCallback(e =>
        {
            StageGridEditor.CurrentLayer = (StageGridEditor.PaintLayer)e.newValue;
            RefreshLayerUi();
        });
        terrain.RegisterValueChangedCallback(e => StageGridEditor.PaintTerrain = (TerrainType)e.newValue);
        factions.RegisterValueChangedCallback(e =>
        {
            StageGridEditor.PaintFactions = (Faction)e.newValue;
            SceneView.RepaintAll();
        });

        Add(layer);
        Add(terrain);
        Add(factions);
        RefreshLayerUi();

        Add(new Label("赤：壁／青：川／紫：崖・穴／黄：陣営の制限(factionの層)"));

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
