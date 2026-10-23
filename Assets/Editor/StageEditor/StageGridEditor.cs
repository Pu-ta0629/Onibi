using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class StageGridEditor
{
    public enum PaintTool { Off, Paint, Erase };

    public enum PaintLayer { Terrain,Faction};

    public static PaintLayer CurrentLayer { get; set; } = PaintLayer.Terrain;
    public static TerrainType PaintTerrain { get; set; } = TerrainType.Wall;
    public static Faction PaintFactions { get; set; } = Faction.Enemy;

    public static PaintTool CurrentTool { get; set; } = PaintTool.Off;

    private static int _brushSize = 1;

    public static int BrushSize
    {
        get => _brushSize;
        set => _brushSize = Mathf.Clamp(value, 1, 5);
    }

    private const int MaxCells = 256;

    private static int _undoGroup;

    static StageGridEditor() => SceneView.duringSceneGui += OnSceneGUI;

    //---- Sceneビューでの表示と操作 ----

    private static void OnSceneGUI(SceneView view)
    {
        var stage = StageSceneEditor.Target;
        if (stage == null) return;

        var grid = stage.WalkableGrid;
        var e = Event.current;

        if(e.type == EventType.Repaint)
        {
            DrawCells(grid);
            DrawOutline(grid);

            if(CurrentTool != PaintTool.Off)
            {
                DrawGridLines(grid);
                if(TryGetCell(grid,e.mousePosition,out var hoverX,out var hoverZ))
                    DrawBrush(grid,hoverX,hoverZ);
            }
        }

        if (CurrentTool == PaintTool.Off) return;
        if (e.type == EventType.MouseMove) view.Repaint();

        var controlId = GUIUtility.GetControlID(FocusType.Passive);
        switch (e.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(controlId);
                break;

                case EventType.MouseDown when e.button == 0 && !e.alt && HandleUtility.nearestControl == controlId:
                GUIUtility.hotControl = controlId;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Paint Walkable Grid");
                _undoGroup = Undo.GetCurrentGroup();
                PaintAtMouse(stage, e.mousePosition);
                e.Use();
                break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                PaintAtMouse(stage, e.mousePosition);
                e.Use();
                break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                GUIUtility.hotControl = 0;
                Undo.CollapseUndoOperations(_undoGroup);
                e.Use();
                break;
        }

    }
    // ---- 描画 ---- 

    private static void DrawCells(WalkableGrid grid)
    {
        for (var z = 0; z < grid.Depth; z++)
        {
            var x = 0;
            while (x < grid.Width)
            {
                var terrain = grid.GetTerrain(x, z);
                if (terrain == TerrainType.Ground)
                {
                    x++;
                    continue;
                }

                var start = x;
                while (x < grid.Width && grid.GetTerrain(x, z) == terrain) x++;
                DrawRect(grid, start, z, x - start, 1, TerrainColor(terrain, 0.35f), TerrainColor(terrain, 0.8f));
            }
        }

        if (CurrentLayer != PaintLayer.Faction) return;

        var fill = new Color(1f, 0.85f, 0.1f, 0.35f);
        var outline = new Color(1f, 0.85f, 0.1f, 0.9f);
        for(var z =0;z < grid.Depth;z++)
        {
            var x = 0;
            while(x < grid.Width)
            {
                if((grid.GetBlockedFactions(x,z) & PaintFactions) == 0)
                {
                    x++;
                    continue;

                }

                var start = x;
                while (x < grid.Width && (grid.GetBlockedFactions(x, z) & PaintFactions) != 0) x++;
                DrawRect(grid,start,z,x-start,1,fill, outline);
            }
        }
    }

    private static Color TerrainColor(TerrainType terrain,float alpha)
    {
        switch(terrain)
        {
            case TerrainType.Wall: return new Color(1f, 0.15f, 0.15f, alpha);
            case TerrainType.Water: return new Color(0.2f, 0.5f, 1f, alpha);
            case TerrainType.Pit: return new Color(0.6f, 0.3f, 0.9f, alpha);
            default: return new Color(1f, 1f, 1f, 0f);
        }
    }
    private static void DrawOutline(WalkableGrid grid)
    {
        var o = grid.Origin;
        var x1 = o.x + grid.Width * grid.CellSize;
        var z1 = o.z + grid.Depth * grid.CellSize;

        Handles.color = new Color(1f, 1f, 1f, 0.7f);
        Handles.DrawPolyLine(
            new Vector3(o.x, o.y, o.z),
            new Vector3(x1, o.y, o.z),
            new Vector3(x1, o.y, z1),
            new Vector3(o.x, o.y, z1),
            new Vector3(o.x, o.y, o.z));
    }

    private static void DrawGridLines(WalkableGrid grid)
    {
        var o = grid.Origin;
        var s = grid.CellSize;
        var x1 = o.x + grid.Width * s;
        var z1 = o.z + grid.Depth * s;

        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        for (var x = 1; x < grid.Width; x++)
            Handles.DrawLine(new Vector3(o.x + x * s, o.y, o.z), new Vector3(o.x + x * s, o.y, z1));
        for (var z = 1; z < grid.Depth; z++)
            Handles.DrawLine(new Vector3(o.x, o.y, o.z + z * s), new Vector3(x1, o.y, o.z + z * s));

    }

    private static void DrawBrush(WalkableGrid grid,int cx,int cz)
    {
        BrushRange(cx, out var x0, out var x1);
        BrushRange(cz, out var z0, out var z1);

        var color = CurrentTool == PaintTool.Paint
            ? new Color(1f, 0.5f, 0.2f, 0.25f)
            : new Color(0.3f, 1f, 0.5f, 0.25f);

        DrawRect(grid, x0, z0, x1 - x0 + 1, z1 - z0 + 1, color, Color.white);
    }

    private static void DrawRect(WalkableGrid grid,int x,int z,int w,int d,Color fill,Color outline)
    {
        var o = grid.Origin;
        var s = grid.CellSize;
        var xa = o.x + x * s;
        var xb = o.x + (x + w) * s;
        var za = o.z + z * s;
        var zb = o.z + (z + d) * s;

        using (new Handles.DrawingScope(Color.white))
        {
            Handles.DrawSolidRectangleWithOutline(new[]
            {
                new Vector3(xa,o.y,za),
                new Vector3(xa,o.y,zb),
                new Vector3(xb,o.y,zb),
                new Vector3(xb,o.y,za),
            }, fill, outline);
        }
    }

    // ---- 塗る処理 ----

    private static void BrushRange(int center,out int start,out int end)
    {
        start = center - (BrushSize - 1) / 2;
        end = start + BrushSize -1;
    }

    private static bool TryGetCell(WalkableGrid grid,Vector2 mousePosition,out int x,out int z)
    {
        x  = 0;
        z = 0;

        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var plane = new Plane(Vector3.up, grid.Origin);
        if (!plane.Raycast(ray, out var distance)) return false;

        var point = ray.GetPoint(distance);
        x = Mathf.FloorToInt((point.x - grid.Origin.x) / grid.CellSize);
        z = Mathf.FloorToInt((point.z - grid.Origin.z) / grid.CellSize);
        return true;
    }

    private static void PaintAtMouse(StageData stage,Vector2 mousePosition)
    {
        if (!TryGetCell(stage.WalkableGrid, mousePosition, out var cx, out var cz)) return;

        var so = new SerializedObject(stage);
        var gridProp = so.FindProperty(StageData.WalkableGridField);
        var width = gridProp.FindPropertyRelative(WalkableGrid.WidthField).intValue;
        var depth = gridProp.FindPropertyRelative(WalkableGrid.DepthField).intValue;
        var terrainCells = gridProp.FindPropertyRelative(WalkableGrid.CellsField);
        var factionCells = gridProp.FindPropertyRelative(WalkableGrid.FactionBlocksField);

        EnsureSize(terrainCells, width * depth);
        EnsureSize(factionCells, width * depth);

        var target = CurrentLayer == PaintLayer.Terrain ? terrainCells : factionCells;

        BrushRange(cx, out var x0, out var x1);
        BrushRange(cz, out var z0, out var z1);

        for(var z = z0; z <= z1; z++)
        {
            for(var x = x0;x <= x1; x++)
            {
                if (x < 0 || z < 0 || x >= width || z >= depth) continue;

                var element = target.GetArrayElementAtIndex(z * width + x);
                var next = NextValue(element.intValue);
                if (next != element.intValue) element.intValue = next;
            }
        }

        so.ApplyModifiedProperties();
    }

    private static int NextValue(int current)
    {
        if (CurrentLayer == PaintLayer.Terrain)
            return CurrentTool == PaintTool.Paint ? (int)PaintTerrain : (int)TerrainType.Ground;

        var mask = (int)PaintFactions;
        return CurrentTool == PaintTool.Paint ? current | mask : current & ~mask;
    }

    private static void EnsureSize(SerializedProperty array,int size)
    {
        if(array.arraySize == size) return;

        array.arraySize = size;
        for (var i = 0; i < size; i++)
            array.GetArrayElementAtIndex(i).intValue = 0;
    }

    // ---- 格子の設定 ----

    public static void ApplySettings(StageData stage,int newWidth,int newDepth,float newCellSize,Vector3 newOrigin)
    {
        newWidth = Mathf.Clamp(newWidth, 1, MaxCells);
        newDepth = Mathf.Clamp(newDepth, 1,MaxCells);
        newCellSize = Mathf.Max(0.1f, newCellSize);

        var so = new SerializedObject(stage);
        var gridProp = so.FindProperty(StageData.WalkableGridField);
        var widthProp = gridProp.FindPropertyRelative(WalkableGrid.WidthField);
        var depthProp = gridProp.FindPropertyRelative(WalkableGrid.DepthField);
        var cellSizeProp = gridProp.FindPropertyRelative(WalkableGrid.CellSizeField);
        var originProp = gridProp.FindPropertyRelative(WalkableGrid.OriginField);
        var cells = gridProp.FindPropertyRelative(WalkableGrid.CellsField);
        var factionCells = gridProp.FindPropertyRelative(WalkableGrid.FactionBlocksField);

        var oldWidth = widthProp.intValue;
        var oldDepth = depthProp.intValue;

        Remap(cells, oldWidth, oldDepth, newWidth, newDepth);
        Remap(factionCells,oldWidth, oldDepth, newWidth, newDepth);

        widthProp.intValue = newWidth;
        depthProp.intValue = newDepth;
        cellSizeProp.floatValue = newCellSize;
        originProp.vector3Value = newOrigin;

        so.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private static void Remap(SerializedProperty array,int oldWidth,int oldDepth,int newWidth,int newDepth)
    {
        var old = new byte[array.arraySize];
        for (var i = 0; i < old.Length; i++)
            old[i] = (byte)array.GetArrayElementAtIndex(i).intValue;

        var next = new byte[newWidth * newDepth];
        var copyWidth = Mathf.Min(oldWidth, newWidth);
        var copyDepth = Mathf.Min(oldDepth, newDepth);
        for(var z =0;z <copyDepth;z++)
        {
            for(var x = 0;x < copyWidth;x++)
            {
                var oldIndex = z * oldWidth + x;
                if (oldIndex < old.Length) next[z * newWidth + x] = old[oldIndex];
            }
        }

        array.arraySize = next.Length;
        for (var i = 0; i < next.Length; i++)
            array.GetArrayElementAtIndex(i).intValue = next[i];

    }
}
