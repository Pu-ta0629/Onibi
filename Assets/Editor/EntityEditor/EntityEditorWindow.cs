using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Entityの編集ウィンドウ
/// </summary>
public sealed class EntityEditorWindow : EditorWindow
{
    //Enitityの種類ごとの定義。Entityの種類を足すときは、Kindsにひとつ足す
    private sealed class EntityKind
    {
        public readonly string Label;                                   //一覧に出す種類名
        public readonly Func<List<MasterData>> Load;                    //アセットを集める
        public readonly Func<MasterData, string> GetName;               //一覧に出す名前
        public readonly Func<MasterData, ColliderSet> GetColliders;     //コライダーの一覧を取り出す
        public readonly string[] FocusProperties;                       //コライダーと攻撃のタブに出す項目

        public EntityKind(
            string label,
            Func<List<MasterData>> load,
            Func<MasterData, string> getName,
            Func<MasterData, ColliderSet> getColliders,
            string[] focusProperties)
        {
            Label = label;
            Load = load;
            GetName = getName;
            GetColliders = getColliders;
            FocusProperties = focusProperties;
        }
    }

    // 一覧の1行分
    private sealed class Item
    {
        public readonly EntityKind Kind;
        public readonly MasterData Asset;
        public int IssueCount;   // 検査で見つかった、問題の数

        public Item(EntityKind kind, MasterData asset)
        {
            Kind = kind;
            Asset = asset;
        }

        // 種類、ID、名前
        public string DisplayName
        {
            get
            {
                var name = Kind.GetName(Asset);
                return $"[{Kind.Label}] {Asset.Id:000}  {(string.IsNullOrWhiteSpace(name) ? "(no name)" : name)}";
            }
        }

        // 一覧に出す文字。問題があるときは、末尾に、数を付ける
        public string Text => IssueCount > 0 ? $"{DisplayName}    (問題 {IssueCount})" : DisplayName;
    }

    //各 Entity が、コライダーの一覧(ColliderSet)を持つ、fieldの名前
    private const string ColliderSetFieldName = "_colliders";

    private static readonly EntityKind[] kinds =
    {
        new EntityKind(
            "敵",
            () => ToMasterList(MasterDataTools.LoadAll<EnemyData>()),
            asset => ((EnemyData)asset).DisplayName,
            asset => ((EnemyData)asset).Colliders,
            new[]{"_colliders","_attack1","_attack2"}),

        new EntityKind(
            "火",
            () => ToMasterList(MasterDataTools.LoadAll<FireStateData>()),
            asset => ((FireStateData)asset).FireState,
            asset => ((FireStateData)asset).Colliders,
            new[] { "_colliders", "_normalAttackKey", "_specialAttackKey", "_normalCooldown", "_specialCooldown" }),

        new EntityKind(
            "迷い子",
            () => ToMasterList(MasterDataTools.LoadAll<LostChilde>()),
            asset => ((LostChilde)asset).DisplayName,
            asset => ((LostChilde)asset).Colliders,
            new[] { "_colliders" }),
    };

    private readonly List<Item> _items = new();
    private ListView _list;
    private VisualElement _detail;
    private ColliderCanvas _canvas;
    private Item _current;

    private readonly Dictionary<UnityEngine.Object, List<string>> _issues = new();
    private VisualElement _issuePanel;
    private long _signature; 

    [MenuItem("Tools/Entity Editor")]
    private static void Open() => GetWindow<EntityEditorWindow>("Entity Editor");

    private static List<MasterData> ToMasterList<T>(List<T> assets) where T : MasterData
    {
        return assets.Cast<MasterData>().ToList();
    }

    private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

    private void OnUndoRedo() => _canvas?.MarkDirtyRepaint();

    //アセット追加、削除、名前の変更があったときに、一覧を更新
    private void OnProjectChange() => Reload();

    private void CreateGUI()
    {
        var root = rootVisualElement;

        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(Reload) { text = "Refresh" });
        root.Add(toolbar);

        var split = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;
        root.Add(split);

        _list = new ListView
        {
            itemsSource = _items,
            fixedItemHeight = 22,
            selectionType = SelectionType.Single,
            makeItem = () => new Label(),
            bindItem = BindItem,
        };
        _list.selectionChanged += _ => ShowEntity(_list.selectedItem as Item);
        split.Add(_list);

        _detail = new VisualElement();
        _detail.style.flexGrow = 1;
        split.Add(_detail);

        root.schedule.Execute(PollChanges).Every(500);

        Reload();
    }

    private void BindItem(VisualElement element, int index)
    {
        var item = _items[index];
        var label = (Label)element;

        label.text = item.Text;
        label.style.color = item.IssueCount > 0
            ? new StyleColor(new Color(1f, 0.7f, 0.3f))      // 問題があるものは、橙色
            : new StyleColor(StyleKeyword.Null);            // 問題がないものは、いつもの色
    }

    private void Reload()
    {
        if (_list == null) return;

        var selectedAsset = (_list.selectedItem as Item)?.Asset;

        _items.Clear();
        foreach(var kind in kinds)
        {
            var assets = kind.Load();
            assets.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var asset in assets) _items.Add(new Item(kind, asset));
        }
        ComputeIssues();

        _list.RefreshItems();

        var index = selectedAsset == null ? -1 : _items.FindIndex(i => i.Asset == selectedAsset);
        if (index >= 0) _list.SetSelectionWithoutNotify(new[] { index });
        else ShowEntity(null);
    }

    // 検査して、アセットごとの、問題の一覧と、一覧に出す数を更新する
    private void ComputeIssues()
    {
        _issues.Clear();

        foreach (var (message, context) in MasterDataTools.CollectIssues())
        {
            if (context == null) continue;

            if (!_issues.TryGetValue(context, out var list))
            {
                list = new List<string>();
                _issues.Add(context, list);
            }

            list.Add(message);
        }

        foreach (var item in _items)
            item.IssueCount = _issues.TryGetValue(item.Asset, out var found) ? found.Count : 0;
    }

    // 選んでいる Entity の、検査の結果を、右の上に出す
    private void RefreshIssuePanel()
    {
        if (_issuePanel == null) return;

        _issuePanel.Clear();
        if (_current == null) return;

        if (!_issues.TryGetValue(_current.Asset, out var list) || list.Count == 0)
        {
            var ok = new Label("検査: 問題はありません");
            ok.style.color = new Color(0.4f, 0.9f, 0.5f);
            ok.style.paddingLeft = 6;
            _issuePanel.Add(ok);
            return;
        }

        const int maxShown = 8;
        for (var i = 0; i < list.Count && i < maxShown; i++)
            _issuePanel.Add(new HelpBox(list[i], HelpBoxMessageType.Warning));

        if (list.Count > maxShown)
            _issuePanel.Add(new Label($"ほかに {list.Count - maxShown} 件の問題があります(Tools > Master Data > Validate で、すべて確認できます)"));
    }

    // アセットの値が変わったら(保存前の変更も含む)、検査をやり直す
    private void PollChanges()
    {
        long signature = _items.Count;
        foreach (var item in _items) signature = signature * 31 + EditorUtility.GetDirtyCount(item.Asset);

        if (signature == _signature) return;
        _signature = signature;

        ComputeIssues();
        _list.RefreshItems();
        RefreshIssuePanel();
    }

    // ---- 右側  ----

    private void ShowEntity(Item item)
    {
        //同じEnitityを、すでに表示している場合は、作り直さない
        if (item != null && _current != null && item.Asset == _current.Asset && _detail.childCount > 0) return;

        _current = item;
        _canvas = null;
        _detail.Clear();

        if (item == null)
        {
            _detail.Add(new Label("左の一覧から、Entity を選んでください"));
            return;
        }

        _issuePanel = new VisualElement();
        _detail.Add(_issuePanel);
        RefreshIssuePanel();

        var tabs = new TabView();
        tabs.style.flexGrow = 1;
        tabs.Add(BuildFullTab(item));
        tabs.Add(BuildFocusTab(item));
        tabs.activeTabChanged += (_, _) => _canvas?.MarkDirtyRepaint();
        _detail.Add(tabs);
    }

    //全項目
    private static Tab BuildFullTab(Item item)
    {
        var tab = new Tab("全項目");

        var scroll = new ScrollView();
        scroll.Add(new InspectorElement(item.Asset));
        tab.Add(scroll);

        return tab;
    }

    // コライダーと攻撃: 図と、コピー、重ねて比較、動作テスト(敵だけ)、コライダーと攻撃に関する項目
    private Tab BuildFocusTab(Item item)
    {
        var tab = new Tab("コライダーと攻撃");

        var canvas = new ColliderCanvas();
        canvas.SetColliders(() => item.Kind.GetColliders(item.Asset));   // 毎回、取り出し直す
        _canvas = canvas;

        var so = new SerializedObject(item.Asset);
        var collidersPath = $"{ColliderSetFieldName}.{ColliderSet.CollidersField}";

        // ドラッグ編集と貼り付けで、値を書き込む先(Inspector の入力欄と、同じ SerializedObject を使う)
        canvas.SetEditTarget(so, collidersPath);

        var hint = new Label("図の中をクリックで、コライダーを選択。白い四角をドラッグで、位置、向き、大きさを変更(Ctrl で、区切りのよい値)。ホイールで、拡大と縮小");
        hint.style.paddingLeft = 6;
        hint.style.opacity = 0.6f;

        var fields = new VisualElement();
        foreach (var name in item.Kind.FocusProperties)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                fields.Add(new HelpBox($"項目 {name} が見つかりません(EntityEditorWindow の Kinds の定義を確認してください)", HelpBoxMessageType.Warning));
                continue;
            }

            fields.Add(new PropertyField(property));
        }
        fields.Bind(so);

        // 一定の間隔で、図を描き直す(結びつけの予約を使わないので、エラーの心配がない。1秒に 10 回の、軽い処理)
        canvas.schedule.Execute(() => canvas.MarkDirtyRepaint()).Every(100);

        // 全体を、1つのスクロールにまとめる
        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;
        scroll.Add(BuildLegend());
        scroll.Add(BuildCopyRow(item, canvas, so, collidersPath));
        scroll.Add(canvas);
        scroll.Add(hint);
        scroll.Add(new EntityComparePanel(canvas, BuildCompareCandidates(item)));

        // 動作テストは、敵だけに対応している
        if (item.Asset is EnemyData enemy) scroll.Add(new EntityTestPanel(enemy, canvas));
        else scroll.Add(new Label("動作テストは、いまは、敵だけに対応しています"));

        scroll.Add(fields);
        tab.Add(scroll);

        return tab;
    }

    // コピーと貼り付けのボタン
    private static VisualElement BuildCopyRow(Item item, ColliderCanvas canvas, SerializedObject so, string collidersPath)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 6;
        row.style.paddingTop = 2;

        var message = new Label();
        message.style.marginLeft = 8;
        message.style.opacity = 0.8f;

        row.Add(new Button(() =>
        {
            var set = item.Kind.GetColliders(item.Asset);
            var index = canvas.SelectedIndex;

            if (index < 0 || index >= set.All.Count)
            {
                message.text = "図の上で、コピーするコライダーを選んでください";
                return;
            }

            message.text = ColliderClipboard.Copy(new[] { set.All[index] })
                ? $"{set.All[index].Key} を、コピーしました"
                : "コピーできませんでした";
        })
        { text = "選んだコライダーをコピー" });

        row.Add(new Button(() =>
        {
            var set = item.Kind.GetColliders(item.Asset);
            message.text = ColliderClipboard.Copy(set.All)
                ? $"{set.All.Count} 個のコライダーを、コピーしました"
                : "コピーできませんでした";
        })
        { text = "すべてコピー" });

        row.Add(new Button(() =>
        {
            var count = ColliderClipboard.Paste(so, collidersPath);
            message.text = count > 0
                ? $"{count} 個のコライダーを、追加しました(名前が重なるものは、番号を付けました)"
                : "貼り付けるコライダーが、コピーされていません";
        })
        { text = "貼り付け" });

        row.Add(message);
        return row;
    }

    // 重ねて比較に選べる Entity(いま選んでいるもの以外のすべて)
    private List<CompareEntry> BuildCompareCandidates(Item current)
    {
        var list = new List<CompareEntry>();

        foreach (var other in _items)
        {
            if (other.Asset == current.Asset) continue;

            var set = other.Kind.GetColliders(other.Asset);
            if (set != null) list.Add(new CompareEntry(other.DisplayName, set));
        }

        return list;
    }

    private static VisualElement BuildLegend()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.paddingLeft = 6;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 2;

        foreach(ColliderRole role in Enum.GetValues(typeof(ColliderRole)))
        {
            var swatch = new VisualElement();
            swatch.style.width = 12;
            swatch.style.height = 12;
            swatch.style.marginTop = 2;
            swatch.style.marginRight = 4;
            swatch.style.backgroundColor = ColliderColors.Of(role);

            var label = new Label(RoleLabel(role));
            label.style.marginRight = 12;

            row.Add(swatch);
            row.Add(label);
        }

        return row;
    }

    private static string RoleLabel(ColliderRole role)
    {
        switch(role)
        {
            case ColliderRole.Hit: return "体(Hit)";
            case ColliderRole.Attack: return "攻撃(Attack)";
            case ColliderRole.Search: return "索敵(Search)";
            default: return role.ToString();
        }
    }

}
