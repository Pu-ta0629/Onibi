using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class StageGridEditor
{
    public enum PaintLayer { Terrain, Faction }

    // 数字キーの順番。1番目が1キー
    public static readonly TerrainType[] TerrainOrder =
    {
        TerrainType.Ground, TerrainType.Water, TerrainType.Hole, TerrainType.Wall,
    };

    public static readonly Faction[] FactionOrder =
    {
        Faction.Fire, Faction.LostChild, Faction.Enemy,
    };

    // 格子の下地(Wall)と、陣営の初期値(全員が通れる)
    private const byte TerrainBase = (byte)TerrainType.Wall;
    private const byte FactionBase = 0;

    private const int MinBrush = 1;
    private const int MaxBrush = 9;
    private const int MaxCells = 256;

    // ---- ツールの状態(変わると、StateChangedで画面に知らせる) ----

    public static event Action StateChanged;

    private static bool _penEnabled;
    private static PaintLayer _currentLayer = PaintLayer.Terrain;
    private static TerrainType _paintTerrain = TerrainType.Ground;
    private static Faction _paintFaction = Faction.Fire;
    private static int _brushSize = 1;

    public static bool PenEnabled
    {
        get => _penEnabled;
        set
        {
            if (_penEnabled == value) return;
            _penEnabled = value;
            RaiseStateChanged();
        }
    }

    public static PaintLayer CurrentLayer
    {
        get => _currentLayer;
        set
        {
            if (_currentLayer == value) return;
            _currentLayer = value;
            RaiseStateChanged();
        }
    }

    public static TerrainType PaintTerrain
    {
        get => _paintTerrain;
        set
        {
            if (_paintTerrain == value) return;
            _paintTerrain = value;
            RaiseStateChanged();
        }
    }

    // 塗る陣営(1つだけ)
    public static Faction PaintFaction
    {
        get => _paintFaction;
        set
        {
            if (_paintFaction == value) return;
            _paintFaction = value;
            RaiseStateChanged();
        }
    }

    public static int BrushSize
    {
        get => _brushSize;
        set
        {
            int clamped = Mathf.Clamp(value, MinBrush, MaxBrush);
            if (_brushSize == clamped) return;
            _brushSize = clamped;
            RaiseStateChanged();
        }
    }

    private static void RaiseStateChanged()
    {
        StateChanged?.Invoke();
        SceneView.RepaintAll();
    }

    // ---- 陣営ごとの、動ける範囲の表示 ----

    private static readonly bool[] ShowRange = new bool[3];
    private static readonly MoveType[] RangeMove = { MoveType.Fly, MoveType.Walk, MoveType.Walk };

    public static bool GetShowRange(int index) => ShowRange[index];

    public static void SetShowRange(int index, bool show)
    {
        ShowRange[index] = show;
        SceneView.RepaintAll();
    }

    public static MoveType GetRangeMove(int index) => RangeMove[index];

    public static void SetRangeMove(int index, MoveType move)
    {
        RangeMove[index] = move;
        SceneView.RepaintAll();
    }

    public static Color FactionColor(int index)
    {
        switch (index)
        {
            case 0: return new Color(1f, 0.55f, 0.1f);   // Fire: オレンジ
            case 1: return new Color(0.7f, 0.5f, 1f);    // LostChild: 紫
            default: return new Color(1f, 0.3f, 0.6f);   // Enemy: ピンク
        }
    }

    // ---- 入力の状態 ----

    private static int _undoGroup;
    private static int _activeButton;           // 塗り始めたボタン(0: 左、1: 右)
    private static bool _middleDown;
    private static Vector2 _middleDownPosition;

    static StageGridEditor() => SceneView.duringSceneGui += OnSceneGUI;

    // ---- Sceneビューでの表示と操作 ----

    private static void OnSceneGUI(SceneView view)
    {
        StageData stage = StageSceneEditor.Target;
        if (stage == null) return;

        WalkableGrid grid = stage.WalkableGrid;
        Event e = Event.current;

        if (e.type == EventType.Repaint)
        {
            DrawCells(grid);
            DrawFactionRanges(grid);
            DrawOutline(grid);

            if (PenEnabled)
            {
                DrawGridLines(grid);
                if (TryGetCell(grid, e.mousePosition, out int hoverX, out int hoverZ))
                {
                    DrawBrush(grid, hoverX, hoverZ);
                    DrawHoverInfo(grid, hoverX, hoverZ);
                }
                DrawHud(view);
            }
        }

        if (!PenEnabled) return;
        if (e.type == EventType.MouseMove) view.Repaint();

        HandleMiddleClick(e);
        HandleKeys(e);
        HandleScroll(e);

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        switch (e.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(controlId);
                break;

            // 左クリック: 塗る、右クリック: 消す
            case EventType.MouseDown when (e.button == 0 || e.button == 1) && !e.alt
                                          && HandleUtility.nearestControl == controlId:
                GUIUtility.hotControl = controlId;
                _activeButton = e.button;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Paint Walkable Grid");
                _undoGroup = Undo.GetCurrentGroup();
                PaintAtMouse(stage, e.mousePosition, _activeButton == 1);
                e.Use();
                break;

            case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                PaintAtMouse(stage, e.mousePosition, _activeButton == 1);
                e.Use();
                break;

            case EventType.MouseUp when GUIUtility.hotControl == controlId:
                GUIUtility.hotControl = 0;
                Undo.CollapseUndoOperations(_undoGroup);
                StageGroundPreview.Refresh(); // 塗り終わった時に、地面のプレビューを作り直す
                e.Use();
                break;
        }
    }

    // ミドルクリックで、TerrainとFactionを切り替える。ドラッグ(カメラの移動)の時は、切り替えない
    // Sceneビューのカメラ操作が、イベントを使い切ることがあるので、rawTypeで見る
    private static void HandleMiddleClick(Event e)
    {
        if (e.button != 2) return;

        if (e.rawType == EventType.MouseDown)
        {
            _middleDown = true;
            _middleDownPosition = e.mousePosition;
        }
        else if (e.rawType == EventType.MouseUp && _middleDown)
        {
            _middleDown = false;
            if ((e.mousePosition - _middleDownPosition).sqrMagnitude < 16f)
            {
                CurrentLayer = CurrentLayer == PaintLayer.Terrain ? PaintLayer.Faction : PaintLayer.Terrain;
            }
        }
    }

    // 数字キーで、Terrainまたは陣営を選ぶ
    private static void HandleKeys(Event e)
    {
        if (e.type != EventType.KeyDown) return;
        if (e.alt || e.control || e.command || e.shift) return;

        int index = KeyToIndex(e.keyCode);
        if (index < 0) return;

        if (CurrentLayer == PaintLayer.Terrain)
        {
            if (index >= TerrainOrder.Length) return;
            PaintTerrain = TerrainOrder[index];
        }
        else
        {
            if (index >= FactionOrder.Length) return;
            PaintFaction = FactionOrder[index];
        }
        e.Use();
    }

    private static int KeyToIndex(KeyCode key)
    {
        if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9) return key - KeyCode.Alpha1;
        if (key >= KeyCode.Keypad1 && key <= KeyCode.Keypad9) return key - KeyCode.Keypad1;
        return -1;
    }

    // ホイールで、ペンサイズを変える。Ctrlを押している間は、Sceneビューのズームに使える
    private static void HandleScroll(Event e)
    {
        if (e.type != EventType.ScrollWheel) return;
        if (e.control || e.command || e.alt) return;

        // 上にスクロール(deltaが負)で大きく、下で小さく
        BrushSize += e.delta.y < 0f ? 1 : -1;
        e.Use();
    }

    // ---- 描画 ----

    private static void DrawCells(WalkableGrid grid)
    {
        // 地形: 4種類すべてを塗る(下地のWallも、赤で出す)
        for (int z = 0; z < grid.Depth; z++)
        {
            int x = 0;
            while (x < grid.Width)
            {
                TerrainType terrain = grid.GetTerrain(x, z);
                int start = x;
                while (x < grid.Width && grid.GetTerrain(x, z) == terrain) x++;

                // 黒は、薄いと見えないので、濃くする
                float fillAlpha = terrain == TerrainType.Hole ? 0.7f : 0.35f;
                DrawRect(grid, start, z, x - start, 1, TerrainColor(terrain, fillAlpha), TerrainColor(terrain, 0.8f));
            }
        }

        if (CurrentLayer != PaintLayer.Faction) return;

        // 陣営の層: 選んでいる陣営が、通れないマスを、黄色で出す
        Color fill = new Color(1f, 0.85f, 0.1f, 0.35f);
        Color outline = new Color(1f, 0.85f, 0.1f, 0.9f);
        for (int z = 0; z < grid.Depth; z++)
        {
            int x = 0;
            while (x < grid.Width)
            {
                if ((grid.GetBlockedFactions(x, z) & PaintFaction) == 0)
                {
                    x++;
                    continue;
                }

                int start = x;
                while (x < grid.Width && (grid.GetBlockedFactions(x, z) & PaintFaction) != 0) x++;
                DrawRect(grid, start, z, x - start, 1, fill, outline);
            }
        }
    }

    // 表示をオンにした陣営の、動ける範囲(地形と陣営の制限の両方を見る)を、陣営の色で重ねる
    private static void DrawFactionRanges(WalkableGrid grid)
    {
        for (int i = 0; i < FactionOrder.Length; i++)
        {
            if (!ShowRange[i]) continue;

            MovementProfile profile = new MovementProfile(RangeMove[i], SizeClass.Small, FactionOrder[i]);
            Color color = FactionColor(i);
            Color fill = new Color(color.r, color.g, color.b, 0.3f);
            Color outline = new Color(color.r, color.g, color.b, 0.8f);

            for (int z = 0; z < grid.Depth; z++)
            {
                int x = 0;
                while (x < grid.Width)
                {
                    if (!grid.CanEnter(x, z, profile))
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x < grid.Width && grid.CanEnter(x, z, profile)) x++;
                    DrawRect(grid, start, z, x - start, 1, fill, outline);
                }
            }
        }
    }

    // Ground: 緑、Water: 青、Hole: 黒、Wall: 赤
    private static Color TerrainColor(TerrainType terrain, float alpha)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return new Color(0.2f, 0.8f, 0.25f, alpha);
            case TerrainType.Water: return new Color(0.2f, 0.5f, 1f, alpha);
            case TerrainType.Hole: return new Color(0f, 0f, 0f, alpha);
            default: return new Color(1f, 0.15f, 0.15f, alpha);
        }
    }

    private static void DrawOutline(WalkableGrid grid)
    {
        Vector3 o = grid.Origin;
        float x1 = o.x + grid.Width * grid.CellSize;
        float z1 = o.z + grid.Depth * grid.CellSize;

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
        Vector3 o = grid.Origin;
        float s = grid.CellSize;
        float x1 = o.x + grid.Width * s;
        float z1 = o.z + grid.Depth * s;

        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        for (int x = 1; x < grid.Width; x++)
            Handles.DrawLine(new Vector3(o.x + x * s, o.y, o.z), new Vector3(o.x + x * s, o.y, z1));
        for (int z = 1; z < grid.Depth; z++)
            Handles.DrawLine(new Vector3(o.x, o.y, o.z + z * s), new Vector3(x1, o.y, o.z + z * s));
    }

    // 左(塗る)は橙、右(消す)は緑。右ボタンを押している間も、緑にする
    private static void DrawBrush(WalkableGrid grid, int cx, int cz)
    {
        BrushRange(cx, out int x0, out int x1);
        BrushRange(cz, out int z0, out int z1);

        bool erase = GUIUtility.hotControl != 0 && _activeButton == 1;
        Color color = erase
            ? new Color(0.3f, 1f, 0.5f, 0.25f)
            : new Color(1f, 0.5f, 0.2f, 0.25f);

        DrawRect(grid, x0, z0, x1 - x0 + 1, z1 - z0 + 1, color, Color.white);
    }

    // マウスの下のマスの、地形と、通れる陣営を文字で出す
    private static void DrawHoverInfo(WalkableGrid grid, int x, int z)
    {
        if (x < 0 || z < 0 || x >= grid.Width || z >= grid.Depth) return;

        Handles.color = Color.white;
        Handles.Label(
            grid.CellCenter(x, z) + Vector3.up * 0.5f,
            $"({x},{z}) {grid.GetTerrain(x, z)} / 通れる陣営: {grid.GetPassableFactions(x, z)}");
    }

    // Sceneビューの左下に、今の状態と操作を出す
    private static void DrawHud(SceneView view)
    {
        string target = CurrentLayer == PaintLayer.Terrain
            ? PaintTerrain.ToString()
            : $"{PaintFaction}(左:通れる / 右:通れない)";

        Handles.BeginGUI();
        GUI.Label(
            new Rect(10f, view.position.height - 100f, 760f, 36f),
            $"Pen: {CurrentLayer} / {target}   Size {BrushSize}\n" +
            "左:塗る  右:消す  中クリック:Terrain/Faction切替  ホイール:サイズ  数字キー:選択",
            EditorStyles.boldLabel);
        Handles.EndGUI();
    }

    private static void DrawRect(WalkableGrid grid, int x, int z, int w, int d, Color fill, Color outline)
    {
        Vector3 o = grid.Origin;
        float s = grid.CellSize;
        float xa = o.x + x * s;
        float xb = o.x + (x + w) * s;
        float za = o.z + z * s;
        float zb = o.z + (z + d) * s;

        using (new Handles.DrawingScope(Color.white))
        {
            Handles.DrawSolidRectangleWithOutline(new[]
            {
                new Vector3(xa, o.y, za),
                new Vector3(xa, o.y, zb),
                new Vector3(xb, o.y, zb),
                new Vector3(xb, o.y, za),
            }, fill, outline);
        }
    }

    // ---- 塗る処理 ----

    private static void BrushRange(int center, out int start, out int end)
    {
        start = center - (BrushSize - 1) / 2;
        end = start + BrushSize - 1;
    }

    private static bool TryGetCell(WalkableGrid grid, Vector2 mousePosition, out int x, out int z)
    {
        x = 0;
        z = 0;

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.up, grid.Origin);
        if (!plane.Raycast(ray, out float distance)) return false;

        Vector3 point = ray.GetPoint(distance);
        x = Mathf.FloorToInt((point.x - grid.Origin.x) / grid.CellSize);
        z = Mathf.FloorToInt((point.z - grid.Origin.z) / grid.CellSize);
        return true;
    }

    private static void PaintAtMouse(StageData stage, Vector2 mousePosition, bool erase)
    {
        if (!TryGetCell(stage.WalkableGrid, mousePosition, out int cx, out int cz)) return;

        SerializedObject so = new SerializedObject(stage);
        SerializedProperty gridProp = so.FindProperty(StageData.WalkableGridField);
        int width = gridProp.FindPropertyRelative(WalkableGrid.WidthField).intValue;
        int depth = gridProp.FindPropertyRelative(WalkableGrid.DepthField).intValue;
        SerializedProperty terrainCells = gridProp.FindPropertyRelative(WalkableGrid.CellsField);
        SerializedProperty factionCells = gridProp.FindPropertyRelative(WalkableGrid.FactionBlocksField);

        EnsureSize(terrainCells, width * depth, TerrainBase);
        EnsureSize(factionCells, width * depth, FactionBase);

        SerializedProperty target = CurrentLayer == PaintLayer.Terrain ? terrainCells : factionCells;

        BrushRange(cx, out int x0, out int x1);
        BrushRange(cz, out int z0, out int z1);

        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || z < 0 || x >= width || z >= depth) continue;

                SerializedProperty element = target.GetArrayElementAtIndex(z * width + x);
                int next = NextValue(element.intValue, erase);
                if (element.intValue != next) element.intValue = next;
            }
        }

        so.ApplyModifiedProperties();
    }

    // 塗る値。
    // 地形: 塗るは、選んだ地形。消すは、下地のWall
    // 陣営: 保存しているのは、通れない陣営のビット。
    //       塗るは、選んだ陣営が通れるようにする(ビットを消す)。消すは、通れなくする(ビットを立てる)
    private static int NextValue(int current, bool erase)
    {
        if (CurrentLayer == PaintLayer.Terrain)
            return erase ? TerrainBase : (int)PaintTerrain;

        int mask = (int)PaintFaction;
        return erase ? current | mask : current & ~mask;
    }

    private static void EnsureSize(SerializedProperty array, int size, byte fill)
    {
        if (array.arraySize == size) return;

        array.arraySize = size;
        for (int i = 0; i < size; i++)
            array.GetArrayElementAtIndex(i).intValue = fill;
    }

    // 全面を、指定した地形で塗りつぶす(下地のWallに戻す時にも使う)
    public static void FillTerrain(StageData stage, TerrainType terrain)
    {
        SerializedObject so = new SerializedObject(stage);
        SerializedProperty gridProp = so.FindProperty(StageData.WalkableGridField);
        int width = gridProp.FindPropertyRelative(WalkableGrid.WidthField).intValue;
        int depth = gridProp.FindPropertyRelative(WalkableGrid.DepthField).intValue;
        SerializedProperty cells = gridProp.FindPropertyRelative(WalkableGrid.CellsField);

        EnsureSize(cells, width * depth, TerrainBase);
        for (int i = 0; i < cells.arraySize; i++)
            cells.GetArrayElementAtIndex(i).intValue = (int)terrain;

        so.ApplyModifiedProperties();
        StageGroundPreview.Refresh();
        SceneView.RepaintAll();
    }

    // ---- 格子の設定 ----

    // 幅と奥行きから、中心がcenterになるOriginを返す
    // 幅や奥行きが奇数の時は、半マスだけ中心がずれる(格子の線を、セルサイズの倍数の位置に保つため)
    public static Vector3 CenteredOrigin(int width, int depth, float cellSize, Vector3 center)
    {
        return new Vector3(
            center.x - (width / 2) * cellSize,
            center.y,
            center.z - (depth / 2) * cellSize);
    }

    // centeredがtrueの時は、newOriginのx、zを使わず、中心が(0, y, 0)になるOriginにする
    public static void ApplySettings(StageData stage, int newWidth, int newDepth, float newCellSize, Vector3 newOrigin, bool centered)
    {
        newWidth = Mathf.Clamp(newWidth, 1, MaxCells);
        newDepth = Mathf.Clamp(newDepth, 1, MaxCells);
        newCellSize = Mathf.Max(0.1f, newCellSize);

        SerializedObject so = new SerializedObject(stage);
        SerializedProperty gridProp = so.FindProperty(StageData.WalkableGridField);
        SerializedProperty widthProp = gridProp.FindPropertyRelative(WalkableGrid.WidthField);
        SerializedProperty depthProp = gridProp.FindPropertyRelative(WalkableGrid.DepthField);
        SerializedProperty cellSizeProp = gridProp.FindPropertyRelative(WalkableGrid.CellSizeField);
        SerializedProperty originProp = gridProp.FindPropertyRelative(WalkableGrid.OriginField);
        SerializedProperty cells = gridProp.FindPropertyRelative(WalkableGrid.CellsField);
        SerializedProperty factionCells = gridProp.FindPropertyRelative(WalkableGrid.FactionBlocksField);

        int oldWidth = widthProp.intValue;
        int oldDepth = depthProp.intValue;
        Vector3 oldOrigin = originProp.vector3Value;
        float oldCellSize = cellSizeProp.floatValue;

        // 中心基準の時は、Originを計算で決める。y(高さ)は、渡された値をそのまま使う
        if (centered)
            newOrigin = CenteredOrigin(newWidth, newDepth, newCellSize, new Vector3(0f, newOrigin.y, 0f));

        // 中心基準の時は、塗った内容のワールド上の位置を保つ(セルサイズが同じ時だけ)
        int shiftX = 0;
        int shiftZ = 0;
        if (centered && Mathf.Approximately(oldCellSize, newCellSize))
        {
            shiftX = Mathf.RoundToInt((oldOrigin.x - newOrigin.x) / newCellSize);
            shiftZ = Mathf.RoundToInt((oldOrigin.z - newOrigin.z) / newCellSize);
        }

        // 格子の外になって消える、塗り済み(下地以外)のセルがあれば、確認する
        int lost = CountLost(cells, oldWidth, oldDepth, newWidth, newDepth, shiftX, shiftZ, TerrainBase)
                 + CountLost(factionCells, oldWidth, oldDepth, newWidth, newDepth, shiftX, shiftZ, FactionBase);
        if (lost > 0 && !EditorUtility.DisplayDialog("格子の変更",
                $"塗り済みのデータ{lost}個が、格子の外になって消えます。続けますか?", "続ける", "キャンセル"))
            return;

        // 広げた部分は、地形は下地のWall、陣営は全員が通れる状態になる
        Remap(cells, oldWidth, oldDepth, newWidth, newDepth, shiftX, shiftZ, TerrainBase);
        Remap(factionCells, oldWidth, oldDepth, newWidth, newDepth, shiftX, shiftZ, FactionBase);

        widthProp.intValue = newWidth;
        depthProp.intValue = newDepth;
        cellSizeProp.floatValue = newCellSize;
        originProp.vector3Value = newOrigin;

        so.ApplyModifiedProperties();
        StageGroundPreview.Refresh();
        SceneView.RepaintAll();
    }

    // 古いセル(x, z)を、新しい(x + shiftX, z + shiftZ)へ移す。空いた所は、fillで埋める
    private static void Remap(SerializedProperty array, int oldWidth, int oldDepth, int newWidth, int newDepth,
        int shiftX, int shiftZ, byte fill)
    {
        byte[] old = new byte[array.arraySize];
        for (int i = 0; i < old.Length; i++)
            old[i] = (byte)array.GetArrayElementAtIndex(i).intValue;

        byte[] next = new byte[newWidth * newDepth];
        if (fill != 0)
        {
            for (int i = 0; i < next.Length; i++) next[i] = fill;
        }

        for (int z = 0; z < oldDepth; z++)
        {
            for (int x = 0; x < oldWidth; x++)
            {
                int oldIndex = z * oldWidth + x;
                int nx = x + shiftX;
                int nz = z + shiftZ;
                if (oldIndex >= old.Length) continue;
                if (nx < 0 || nz < 0 || nx >= newWidth || nz >= newDepth) continue;

                next[nz * newWidth + nx] = old[oldIndex];
            }
        }

        array.arraySize = next.Length;
        for (int i = 0; i < next.Length; i++)
            array.GetArrayElementAtIndex(i).intValue = next[i];
    }

    // 格子の外になって消える、下地(baseValue)以外のセルの数を数える
    private static int CountLost(SerializedProperty array, int oldWidth, int oldDepth, int newWidth, int newDepth,
        int shiftX, int shiftZ, byte baseValue)
    {
        int lost = 0;
        for (int z = 0; z < oldDepth; z++)
        {
            for (int x = 0; x < oldWidth; x++)
            {
                int oldIndex = z * oldWidth + x;
                if (oldIndex >= array.arraySize) continue;
                if (array.GetArrayElementAtIndex(oldIndex).intValue == baseValue) continue;

                int nx = x + shiftX;
                int nz = z + shiftZ;
                if (nx < 0 || nz < 0 || nx >= newWidth || nz >= newDepth) lost++;
            }
        }
        return lost;
    }
}