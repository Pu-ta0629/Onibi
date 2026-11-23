using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class StageEditorWindow : EditorWindow
{
    private const string StageFolder = "Assets/Stages";

    // 境目をドラッグしても、これより小さくならない
    private const float MinPaneSize = 40f;

    // 境目の位置を、EditorPrefsに保存する時の名前
    private const string SplitPrefPrefix = "StageEditor.Split.";
    private static readonly string[] SplitKeys = { "List", "Objects", "GridDetail", "Message" };

    private readonly List<StageData> _stages = new();
    private ListView _list;
    private ScrollView _detail;
    private Label _message;
    private StageGridPanel _gridPanel;

    [MenuItem("Tools/Stage Editor")]
    private static void Open() => GetWindow<StageEditorWindow>("Stage Editor");

    private void OnDisable()
    {
        StageSceneEditor.Target = null;

        StageGridEditor.PenEnabled = false;
    }

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        // ---- 上部のツールバー(固定) ----
        Toolbar toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(CreateStage) { text = "新規" });
        toolbar.Add(new ToolbarButton(DuplicateSelected) { text = "複製" });
        toolbar.Add(new ToolbarButton(DeleteSelected) { text = "削除" });
        toolbar.Add(new ToolbarButton(Reload) { text = "リフレッシュ" });
        toolbar.Add(new ToolbarButton(Validate) { text = "検証" });
        toolbar.Add(new ToolbarButton(ResetLayout) { text = "レイアウト初期化" });
        root.Add(toolbar);

        // ---- Objectの配置のボタン(幅が足りない時は、折り返す) ----
        Toolbar placeBar = CreateWrapToolbar();
        placeBar.Add(new ToolbarButton(StageSceneEditor.SetStartHere) { text = "Start設置" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddGoal) { text = "ゴール追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddSpawnSpot) { text = "湧きスポット追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddEnemy) { text = "エネミー追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.RemoveSelected) { text = "削除" });

        ToolbarToggle snap = new ToolbarToggle { text = "Enemyと湧きスポットをマスに合わせる", value = StageSceneEditor.SnapToGrid };
        snap.RegisterValueChangedCallback(e => StageSceneEditor.SnapToGrid = e.newValue);
        placeBar.Add(snap);
        placeBar.Add(new ToolbarButton(StageSceneEditor.SnapAll) { text = "全部合わせる" });

        Toolbar visibleBar = CreateWrapToolbar();
        visibleBar.Add(new Label("表示:"));
        visibleBar.Add(CreateVisibleToggle("Start", StageSceneEditor.ObjectKind.Start));
        visibleBar.Add(CreateVisibleToggle("Goal", StageSceneEditor.ObjectKind.Goal));
        visibleBar.Add(CreateVisibleToggle("Enemy", StageSceneEditor.ObjectKind.Enemy));
        visibleBar.Add(CreateVisibleToggle("Spot", StageSceneEditor.ObjectKind.Spot));

        // ---- 各エリア ----

        // 左: ステージの一覧
        _list = new ListView(_stages)
        {
            fixedItemHeight = 22,
            selectionType = SelectionType.Single,
            makeItem = () => new Label(),
            bindItem = (element, index) =>
            {
                StageData stage = _stages[index];
                string stageName = string.IsNullOrEmpty(stage.StageName) ? "(no name)" : stage.StageName;
                ((Label)element).text = $"{stage.StageId:000} {stageName}";
            }
        };
        _list.selectionChanged += _ => ShowDetail(_list.selectedItem as StageData);
        _list.style.flexGrow = 1f;

        VisualElement listPane = new VisualElement();
        listPane.Add(_list);

        // 右上: Objectの配置と表示のボタン
        ScrollView objectsPane = new ScrollView();
        objectsPane.Add(placeBar);
        objectsPane.Add(visibleBar);

        // 右中: 格子とペン
        _gridPanel = new StageGridPanel();
        ScrollView gridPane = new ScrollView();
        gridPane.Add(_gridPanel);

        // 右下: 選んだステージの詳細
        _detail = new ScrollView();

        // 下: 検証の結果
        _message = new Label();
        _message.style.whiteSpace = WhiteSpace.Normal;
        _message.style.paddingLeft = 6f;
        _message.style.paddingBottom = 4f;

        ScrollView messagePane = new ScrollView();
        messagePane.Add(_message);

        // ---- 境目(ドラッグで、大きさを変えられる) ----
        // 内側から順に、組み立てる
        TwoPaneSplitView gridDetailSplit = CreateSplit(
            "GridDetail", gridPane, _detail, 0, 300f, TwoPaneSplitViewOrientation.Vertical);

        TwoPaneSplitView objectsSplit = CreateSplit(
            "Objects", objectsPane, gridDetailSplit, 0, 80f, TwoPaneSplitViewOrientation.Vertical);

        TwoPaneSplitView mainSplit = CreateSplit(
            "List", listPane, objectsSplit, 0, 220f, TwoPaneSplitViewOrientation.Horizontal);

        TwoPaneSplitView rootSplit = CreateSplit(
            "Message", mainSplit, messagePane, 1, 90f, TwoPaneSplitViewOrientation.Vertical);

        rootSplit.style.flexGrow = 1f;
        root.Add(rootSplit);

        Reload();
    }

    // 2つのエリアの間に、ドラッグできる境目を作る。
    // fixedPaneIndexのエリアが、大きさを保つほう(ウィンドウの大きさを変えた時は、もう片方が伸び縮みする)。
    // 境目を動かすと、その大きさを覚えて、次に開いた時に使う
    private static TwoPaneSplitView CreateSplit(
        string key,
        VisualElement first,
        VisualElement second,
        int fixedPaneIndex,
        float defaultDimension,
        TwoPaneSplitViewOrientation orientation)
    {
        string prefKey = SplitPrefPrefix + key;
        float dimension = Mathf.Max(MinPaneSize, EditorPrefs.GetFloat(prefKey, defaultDimension));

        TwoPaneSplitView split = new TwoPaneSplitView(fixedPaneIndex, dimension, orientation);
        split.Add(first);
        split.Add(second);

        // 小さくしすぎて、見えなくならないようにする
        bool horizontal = orientation == TwoPaneSplitViewOrientation.Horizontal;
        foreach (VisualElement pane in new[] { first, second })
        {
            if (horizontal) pane.style.minWidth = MinPaneSize;
            else pane.style.minHeight = MinPaneSize;
        }

        // 大きさが変わったら、少し待ってから保存する(ドラッグ中に何度も書かないため)
        VisualElement fixedPane = fixedPaneIndex == 0 ? first : second;
        IVisualElementScheduledItem saveTask = null;
        fixedPane.RegisterCallback<GeometryChangedEvent>(e =>
        {
            float size = horizontal ? e.newRect.width : e.newRect.height;
            if (size < MinPaneSize) return;

            saveTask?.Pause();
            saveTask = fixedPane.schedule.Execute(() => EditorPrefs.SetFloat(prefKey, size));
            saveTask.StartingIn(300);
        });

        return split;
    }

    // 幅が足りない時に、ボタンが折り返すツールバー
    private static Toolbar CreateWrapToolbar()
    {
        Toolbar bar = new Toolbar();
        bar.style.flexWrap = Wrap.Wrap;
        bar.style.height = StyleKeyword.Auto;
        bar.style.minHeight = 21f;
        bar.style.flexShrink = 0f;
        return bar;
    }

    // 境目の位置を、最初の状態に戻す(選んでいるステージは、そのまま)
    private void ResetLayout()
    {
        foreach (string key in SplitKeys)
            EditorPrefs.DeleteKey(SplitPrefPrefix + key);

        StageData keep = _list?.selectedItem as StageData;

        rootVisualElement.Clear();
        CreateGUI();

        if (keep != null && _stages.Contains(keep)) Select(keep);
    }

    private void Reload()
    {
        StageData selected = _list.selectedItem as StageData;

        _stages.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null) _stages.Add(stage);
        }
        _stages.Sort((a, b) => a.StageId.CompareTo(b.StageId));
        _list.Rebuild();

        if (selected != null && _stages.Contains(selected))
            Select(selected);
        else
            ShowDetail(null);
    }

    private void Select(StageData stage)
    {
        int index = _stages.IndexOf(stage);
        if (index >= 0) _list.SetSelection(index);
        ShowDetail(stage);
    }

    private void ShowDetail(StageData stage)
    {
        StageSceneEditor.Target = stage;

        _gridPanel?.SetTarget(stage);

        _detail.Clear();
        if (stage == null)
        {
            _detail.Add(new Label("ステージを選択してください"));
            return;
        }
        _detail.Add(new InspectorElement(stage));
    }

    private void CreateStage()
    {
        EnsureFolder(StageFolder);
        Reload();

        int id = NextId();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{StageFolder}/Stage_{id:000}.asset");
        StageData stage = CreateInstance<StageData>();
        AssetDatabase.CreateAsset(stage, path);
        SetStageId(stage, id);

        WalkableGrid grid = stage.WalkableGrid;
        StageGridEditor.ApplySettings(stage, grid.Width, grid.Depth, grid.CellSize, grid.Origin, true);
        EditorUtility.SetDirty(stage);

        Reload();
        Select(stage);
    }

    private void DeleteSelected()
    {
        StageData stage = _list.selectedItem as StageData;
        if (stage == null) return;

        string stageName = string.IsNullOrWhiteSpace(stage.StageName) ? stage.name : stage.StageName;
        string path = AssetDatabase.GetAssetPath(stage);

        if (!EditorUtility.DisplayDialog("ステージの削除", $"{stageName}\n({path})\nを削除しますか？", "削除", "キャンセル"))
            return;

        StageSceneEditor.Target = null;

        AssetDatabase.MoveAssetToTrash(path);

        _list.ClearSelection();
        Reload();
    }

    private void DuplicateSelected()
    {
        StageData source = _list.selectedItem as StageData;
        if (source == null) return;

        EnsureFolder(StageFolder);
        Reload();

        int id = NextId();
        string destPath = AssetDatabase.GenerateUniqueAssetPath($"{StageFolder}/Stage_{id:000}.asset");
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destPath)) return;

        StageData copy = AssetDatabase.LoadAssetAtPath<StageData>(destPath);
        SetStageId(copy, id);

        Reload();
        Select(copy);
    }

    private void Validate()
    {
        Reload();
        List<string> issues = new List<string>();

        foreach (IGrouping<int, StageData> group in _stages.GroupBy(s => s.StageId).Where(g => g.Count() > 1))
            issues.Add($"stageId {group.Key}が重複: {string.Join(", ", group.Select(s => s.name))}");

        foreach (StageData stage in _stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageName))
                issues.Add($"{stage.name}: stageName が空です");
            if (stage.Goals.Count == 0)
                issues.Add($"{stage.name}: goals が0件です");

            issues.AddRange(StageValidator.Validate(stage));
        }

        _message.text = issues.Count == 0 ? "問題なし" : string.Join("\n", issues);
        foreach (string issue in issues)
            Debug.LogWarning($"[StageEditor] {issue}");
    }

    private int NextId() => StageIdCounter.Take(id => _stages.Any(s => s.StageId == id));

    private static void SetStageId(StageData stage, int id)
    {
        SerializedObject so = new SerializedObject(stage);
        so.FindProperty(StageData.StageIdField).intValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static ToolbarToggle CreateVisibleToggle(string text, StageSceneEditor.ObjectKind objectKind)
    {
        ToolbarToggle toggle = new ToolbarToggle
        {
            text = text,
            value = (StageSceneEditor.VisibleKinds & objectKind) != 0,
        };
        toggle.RegisterValueChangedCallback(e => StageSceneEditor.SetVisible(objectKind, e.newValue));
        return toggle;
    }
}