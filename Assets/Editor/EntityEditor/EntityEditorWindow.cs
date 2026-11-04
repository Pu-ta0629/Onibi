using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
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

    private sealed class Item
    {
        public readonly EntityKind Kind;
        public readonly MasterData Asset;

        public Item(EntityKind kind,MasterData asset)
        {
            Kind = kind;
            Asset = asset;
        }

        public string Text
        {
            get
            {
                var name = Kind.GetName(Asset);
                return $"[{Kind.Label}]{Asset.Id:000} {(string.IsNullOrWhiteSpace(name) ? "(no name)" : name)}";
            }
        }
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

        Reload();
    }

    private void BindItem(VisualElement element,int index)
    {
        ((Label)element).text = _items[index].Text;
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
        _list.RefreshItems();

        var index = selectedAsset == null ? -1 : _items.FindIndex(i => i.Asset == selectedAsset);
        if (index >= 0) _list.SetSelectionWithoutNotify(new[] { index });
        else ShowEntity(null);
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

    //コライダーと攻撃
    private Tab BuildFocusTab(Item item)
    {
        var tab = new Tab("コライダーと攻撃");

        var canvas = new ColliderCanvas();
        canvas.SetColliders(() => item.Kind.GetColliders(item.Asset));
        _canvas = canvas;

        var hint = new Label("図の中をクリックで、コライダーを選択。白い四角をドラッグで、位置、向き、大きさを変更(Ctrl で、区切りのよい値)。ホイールで、拡大と縮小");
        hint.style.paddingLeft = 6;
        hint.style.opacity = 0.6f;

        var so = new SerializedObject(item.Asset);

        canvas.SetEditTarget(so, $"{ColliderSetFieldName}.{ColliderSet.CollidersField}");

        var fields = new ScrollView();
        fields.style.flexGrow = 1;

        foreach(var name in item.Kind.FocusProperties)
        {
            var property = so.FindProperty(name);
            if(property == null)
            {
                fields.Add(new HelpBox($"項目 {name}が見つかりません(EntityEditorWindow の Kinds の定義を確認してください)", HelpBoxMessageType.Warning));
                continue;
            }

            fields.Add(new PropertyField(property));
        }

        fields.Bind(so);
        canvas.schedule.Execute(() => canvas.MarkDirtyRepaint()).Every(100); //一定の間隔で、図を書き直す

        tab.Add(BuildLegend());
        tab.Add(canvas);
        tab.Add(canvas);
        tab.Add(hint);
        tab.Add(fields);

        return tab;
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
