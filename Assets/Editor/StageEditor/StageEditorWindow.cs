using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Android.Gradle;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class Stageeditorwindow : EditorWindow
{
    const string StageFolder = "Assets/Stages";

    readonly List<StageData> _stages = new();
    ListView _list;
    ScrollView _detail;
    Label _message;
    StageGridPanel _gridPanel;

    [MenuItem("Tools/Stage Editor")]
    static void Open() => GetWindow<Stageeditorwindow>("Stage Editor");

    private void OnDisable()
    {
        StageSceneEditor.Target = null;

        StageGridEditor.CurrentTool = StageGridEditor.PaintTool.Off;
    }
    private void CreateGUI()
    {
        var root = rootVisualElement;

        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(CreateStage) { text = "新規" });
        toolbar.Add(new ToolbarButton(DuplicateSelected) { text = "複製" });
        toolbar.Add(new ToolbarButton(DeleteSelected) { text = "削除" });
        toolbar.Add(new ToolbarButton(Reload) { text = "リフレッシュ" });
        toolbar.Add(new ToolbarButton(Validate) { text = "検証" });
        root.Add(toolbar);

        var placeBar = new Toolbar();
        placeBar.Add(new ToolbarButton(StageSceneEditor.SetStartHere) { text = "Start設置" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddGoal) { text = "ゴール追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddEnemy) { text = "エネミー追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.AddItem) { text = "アイテム追加" });
        placeBar.Add(new ToolbarButton(StageSceneEditor.RemoveSelected) { text = "削除" });
        root.Add(placeBar);

        _gridPanel = new StageGridPanel();
        root.Add(_gridPanel);

        var split = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;
        root.Add(split);

        _list = new ListView(_stages)
        {
            fixedItemHeight = 22,
            selectionType = SelectionType.Single,
            makeItem = () => new Label(),
            bindItem = (element,index) =>
            {
                var stage = _stages[index];
                var name = string.IsNullOrEmpty(stage.StageName) ? "(no name)" : stage.StageName;
                ((Label)element).text = $"{stage.StageId:000} {name}";
            }
        };
        _list.selectionChanged += _ => ShowDetail(_list.selectedItem as StageData);
        split.Add(_list);

        _detail = new ScrollView();
        split.Add(_detail);

        _message = new Label();
        _message.style.whiteSpace = WhiteSpace.Normal;
        _message.style.paddingLeft = 6;
        _message.style.paddingBottom = 4;
        root.Add(_message);

        Reload();
    }

    void Reload()
    {
        var selected = _list.selectedItem as StageData;

        _stages.Clear();
        foreach(var guid in AssetDatabase.FindAssets("t:StageData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null) _stages.Add(stage);
        }
        _stages.Sort((a, b) => a.StageId.CompareTo(b.StageId));
        _list.Rebuild();

        if (selected != null && _stages.Contains(selected))
            Select(selected);
        else
            ShowDetail(null);
    }

    void Select(StageData stage)
    {
        var index = _stages.IndexOf(stage);
        if (index >= 0) _list.SetSelection(index);
        ShowDetail(stage);
    }

    void ShowDetail(StageData stage)
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

    void CreateStage()
    {
        EnsureFolder(StageFolder);
        Reload();

        var id = NextId();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{StageFolder}/Stage_{id:000}.asset");
        var stage = CreateInstance<StageData>();
        AssetDatabase.CreateAsset(stage, path);
        SetStageId(stage, id);

        Reload();
        Select(stage);
    }

    void DeleteSelected()
    {
        var stage = _list.selectedItem as StageData;
        if (stage == null) return;

        var name = string.IsNullOrWhiteSpace(stage.StageName) ? stage.name : stage.StageName;
        var path = AssetDatabase.GetAssetPath(stage);

        if (!EditorUtility.DisplayDialog("ステージの削除", $"{name}\n({path})\nを削除しますか？", "削除", "キャンセル"))
        return;

        StageSceneEditor.Target = null;

        AssetDatabase.MoveAssetToTrash(path);

        _list.ClearSelection();
        Reload();
    }

    void DuplicateSelected()
    {
        var source = _list.selectedItem as StageData;
        if (source == null) return;

        EnsureFolder(StageFolder);
        Reload();

        var id = NextId();
        var destPath = AssetDatabase.GenerateUniqueAssetPath($"{StageFolder}/Stage_{id:000}.asset");
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destPath)) return;

        var copy = AssetDatabase.LoadAssetAtPath<StageData>(destPath);
        SetStageId(copy, id);

        Reload();
        Select(copy);
    }

    void Validate()
    {
        Reload();
        var issues = new List<string>();

        foreach (var group in _stages.GroupBy(s => s.StageId).Where(g => g.Count() > 1))
            issues.Add($"stageId {group.Key}が重複: {string.Join(", ", group.Select(s => s.name))}");

        foreach(var stage in _stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageName))
                issues.Add($"{stage.name}: stageName が空です");
            if (stage.Goals.Count == 0)
                issues.Add($"{stage.name}: goals が0件です");
        }

        _message.text = issues.Count == 0 ? "問題なし" : string.Join("\n", issues);
        foreach (var issue in issues)
            Debug.LogWarning($"[StageEditor] {issue}");
    }

    int NextId() => StageIdCounter.Take(id => _stages.Any(s => s.StageId == id));

    static void SetStageId(StageData stage,int id)
    {
        var so = new SerializedObject(stage);
        so.FindProperty(StageData.StageIdField).intValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
