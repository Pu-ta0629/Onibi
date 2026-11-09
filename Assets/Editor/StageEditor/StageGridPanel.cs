using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class StageGridPanel : VisualElement
{
    private readonly ToolbarToggle _pen;
    private readonly EnumField _layer;
    private readonly RadioButtonGroup _terrain;
    private readonly RadioButtonGroup _faction;
    private readonly SliderInt _brush;

    private readonly IntegerField _width;
    private readonly IntegerField _depth;
    private readonly FloatField _cellSize;
    private readonly Vector3Field _origin;
    private readonly Toggle _centered;

    private StageData _stage;

    public StageGridPanel()
    {
        style.paddingLeft = 6;
        style.paddingRight = 6;
        style.paddingTop = 4;
        style.paddingBottom = 4;

        Label title = new Label("Walkable Grid");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        Add(title);

        // ペン(トグル)。オンにすると、Sceneビューで塗れる
        Toolbar penBar = new Toolbar();
        _pen = new ToolbarToggle { text = "Pen (ペン)" };
        _pen.RegisterValueChangedCallback(e => StageGridEditor.PenEnabled = e.newValue);
        penBar.Add(_pen);
        Add(penBar);

        // 層(中クリックでも切り替わる)
        _layer = new EnumField("Layer", StageGridEditor.CurrentLayer);
        _layer.RegisterValueChangedCallback(e => StageGridEditor.CurrentLayer = (StageGridEditor.PaintLayer)e.newValue);
        Add(_layer);

        // 塗る地形。数字キーでも選べる
        List<string> terrainChoices = new List<string>();
        for (int i = 0; i < StageGridEditor.TerrainOrder.Length; i++)
            terrainChoices.Add($"{i + 1}: {StageGridEditor.TerrainOrder[i]}");
        _terrain = new RadioButtonGroup("Terrain", terrainChoices);
        _terrain.RegisterValueChangedCallback(e =>
        {
            if (e.newValue >= 0 && e.newValue < StageGridEditor.TerrainOrder.Length)
                StageGridEditor.PaintTerrain = StageGridEditor.TerrainOrder[e.newValue];
        });
        Add(_terrain);

        // 塗る陣営(1つだけ)。数字キーでも選べる
        List<string> factionChoices = new List<string>();
        for (int i = 0; i < StageGridEditor.FactionOrder.Length; i++)
            factionChoices.Add($"{i + 1}: {StageGridEditor.FactionOrder[i]}");
        _faction = new RadioButtonGroup("Faction", factionChoices);
        _faction.RegisterValueChangedCallback(e =>
        {
            if (e.newValue >= 0 && e.newValue < StageGridEditor.FactionOrder.Length)
                StageGridEditor.PaintFaction = StageGridEditor.FactionOrder[e.newValue];
        });
        Add(_faction);

        Label help = new Label(
            "Pen: 左クリックで塗る、右クリックで消す、中クリックでTerrain/Faction切替、ホイールでペンサイズ、数字キーで選択\n" +
            "Terrain: 緑=Ground、青=Water、黒=Hole、赤=Wall(下地。消すとWallに戻る)\n" +
            "Faction: 最初は全陣営が通れない。選んだ陣営の、左=通れるようにする、右=通れなくする。色=選んだ陣営が通れるマス\n" +
            "ブラシを置くと、マスの地形と、通れる陣営が文字で出る");
        help.style.whiteSpace = WhiteSpace.Normal;
        Add(help);

        _brush = new SliderInt("Brush Size", 1, 9) { value = StageGridEditor.BrushSize, showInputField = true };
        _brush.RegisterValueChangedCallback(e => StageGridEditor.BrushSize = e.newValue);
        Add(_brush);

        Add(new Button(FillAll) { text = "Fill All (選択中のTerrainで全面を塗る)" });   // 既存
        Add(new Button(AllowAllFactions) { text = "Allow All Factions (全陣営を、全面で通れるようにする)" });

        // 陣営ごとの、動ける範囲の表示。移動手段によって通れる地形が変わるので、移動手段も選ぶ
        Label rangeTitle = new Label("動ける範囲の表示(陣営ごと)");
        rangeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        rangeTitle.style.marginTop = 6;
        Add(rangeTitle);

        for (int i = 0; i < StageGridEditor.FactionOrder.Length; i++)
        {
            int index = i; // ラムダ用

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            VisualElement swatch = new VisualElement();
            swatch.style.width = 12;
            swatch.style.height = 12;
            swatch.style.marginRight = 4;
            swatch.style.backgroundColor = StageGridEditor.FactionColor(i);
            row.Add(swatch);

            Toggle show = new Toggle($"{StageGridEditor.FactionOrder[i]}") { value = StageGridEditor.GetShowRange(i) };
            show.RegisterValueChangedCallback(e => StageGridEditor.SetShowRange(index, e.newValue));
            row.Add(show);

            EnumField move = new EnumField(StageGridEditor.GetRangeMove(i));
            move.RegisterValueChangedCallback(e => StageGridEditor.SetRangeMove(index, (MoveType)e.newValue));
            row.Add(move);

            Add(row);
        }

        // 格子の設定
        _width = new IntegerField("Width(cells)");
        _depth = new IntegerField("Depth(cells)");
        _cellSize = new FloatField("Cell Size");
        _origin = new Vector3Field("Origin");
        _centered = new Toggle("Keep Centered (0,0)") { value = true };
        Add(_width);
        Add(_depth);
        Add(_cellSize);
        Add(_origin);
        Add(_centered);
        Add(new Label("オンの時は、OriginのXとZは使われません(Yだけ使われます)"));
        Add(new Button(Apply) { text = "Apply Grid Setting" });

        // 中クリックや数字キー、ホイールで状態が変わった時に、画面の表示を合わせる
        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            StageGridEditor.StateChanged += Sync;
            Sync();
        });
        RegisterCallback<DetachFromPanelEvent>(_ => StageGridEditor.StateChanged -= Sync);

        Sync();
    }

    public void SetTarget(StageData stage)
    {
        _stage = stage;
        SetEnabled(stage != null);
        if (stage == null) return;

        WalkableGrid grid = stage.WalkableGrid;
        _width.SetValueWithoutNotify(grid.Width);
        _depth.SetValueWithoutNotify(grid.Depth);
        _cellSize.SetValueWithoutNotify(grid.CellSize);
        _origin.SetValueWithoutNotify(grid.Origin);
    }

    // ツールの状態を、画面に反映する(通知は出さない)
    private void Sync()
    {
        _pen.SetValueWithoutNotify(StageGridEditor.PenEnabled);
        _layer.SetValueWithoutNotify(StageGridEditor.CurrentLayer);
        _terrain.SetValueWithoutNotify(Array.IndexOf(StageGridEditor.TerrainOrder, StageGridEditor.PaintTerrain));
        _faction.SetValueWithoutNotify(Array.IndexOf(StageGridEditor.FactionOrder, StageGridEditor.PaintFaction));
        _brush.SetValueWithoutNotify(StageGridEditor.BrushSize);

        bool isTerrain = StageGridEditor.CurrentLayer == StageGridEditor.PaintLayer.Terrain;
        _terrain.style.display = isTerrain ? DisplayStyle.Flex : DisplayStyle.None;
        _faction.style.display = isTerrain ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void Apply()
    {
        if (_stage == null) return;
        StageGridEditor.ApplySettings(_stage, _width.value, _depth.value, _cellSize.value, _origin.value, _centered.value);
        SetTarget(_stage);
    }

    private void FillAll()
    {
        if (_stage == null) return;

        TerrainType terrain = StageGridEditor.PaintTerrain;
        if (!EditorUtility.DisplayDialog("全面を塗る", $"格子の全面を {terrain} で塗ります。続けますか?", "続ける", "キャンセル"))
            return;

        StageGridEditor.FillTerrain(_stage, terrain);
    }

    private void AllowAllFactions()
    {
        if (_stage == null) return;

        if (!EditorUtility.DisplayDialog("全陣営を通れるようにする",
                "格子の全面を、すべての陣営が通れるようにします。陣営の塗り分けは、消えます。続けますか?", "続ける", "キャンセル"))
            return;

        StageGridEditor.AllowAllFactions(_stage);
    }
}