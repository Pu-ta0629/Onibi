using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ColliderDef の Inspector 表示。役割(Role)と形に合わせて、使う項目だけを表示する。
// 見出しに、名前と役割を出し、左端の線と背景を、役割の色にする。
[CustomPropertyDrawer(typeof(ColliderDef))]
public sealed class ColliderDefDrawer : PropertyDrawer
{
    // 表示の切り替えに使う、要素と値のまとめ
    private sealed class Views
    {
        public Foldout Root;
        public SerializedProperty Key;
        public SerializedProperty Role;
        public SerializedProperty Kind;
        public PropertyField Yaw;
        public PropertyField Targets;
        public HelpBox Warning;
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var keyProperty = property.FindPropertyRelative(ColliderDef.KeyField);
        var roleProperty = property.FindPropertyRelative(ColliderDef.RoleField);
        var shapeProperty = property.FindPropertyRelative(ColliderDef.ShapeField);
        var kindProperty = shapeProperty.FindPropertyRelative(ColliderShape.KindField);

        var root = new Foldout { value = true };
        root.style.borderLeftWidth = 4;
        root.style.paddingLeft = 4;
        root.style.marginBottom = 4;

        var views = new Views
        {
            Root = root,
            Key = keyProperty,
            Role = roleProperty,
            Kind = kindProperty,
            Yaw = new PropertyField(property.FindPropertyRelative(ColliderDef.YawOffsetField)),
            Targets = new PropertyField(property.FindPropertyRelative(ColliderDef.TargetsField)),
            Warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning),
        };

        root.Add(new PropertyField(keyProperty));
        root.Add(new PropertyField(roleProperty));
        root.Add(views.Warning);
        root.Add(new PropertyField(shapeProperty));
        root.Add(new PropertyField(property.FindPropertyRelative(ColliderDef.OffsetField)));
        root.Add(views.Yaw);
        root.Add(views.Targets);

        Refresh(views);

        // 名前、役割、形が変わったら、表示を更新する
        root.TrackPropertyValue(keyProperty, _ => Refresh(views));
        root.TrackPropertyValue(roleProperty, _ => Refresh(views));
        root.TrackPropertyValue(kindProperty, _ => Refresh(views));

        return root;
    }

    private static void Refresh(Views views)
    {
        // enumValueIndex は、enum の値が 0 から順に並んでいる前提(新しい役割は、末尾に足す)
        var role = (ColliderRole)views.Role.enumValueIndex;
        var kind = (ColliderShapeKind)views.Kind.enumValueIndex;
        var color = ColliderColors.Of(role);

        // 見出しと、役割の色
        views.Root.text = $"{views.Key.stringValue} ({role})";
        views.Root.style.borderLeftColor = color;
        views.Root.style.backgroundColor = new Color(color.r, color.g, color.b, 0.08f);

        // 使わない項目を隠す
        views.Targets.style.display = ColliderRules.NeedsTargets(role) ? DisplayStyle.Flex : DisplayStyle.None;
        views.Yaw.style.display = kind == ColliderShapeKind.Circle ? DisplayStyle.None : DisplayStyle.Flex;

        // 役割で、使えない形を選んだときの警告
        var allowed = ColliderRules.Allows(role, kind);
        views.Warning.style.display = allowed ? DisplayStyle.None : DisplayStyle.Flex;
        views.Warning.text = $"役割 {role} では、{kind} の形は使えません";
    }
}
