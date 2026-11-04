using System;
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
        public SerializedProperty Property;   // コライダー1つ分の値(この Inspector の SerializedObject から取ったもの)
        public PropertyField Yaw;
        public PropertyField Targets;
        public HelpBox Warning;
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var root = new Foldout { value = true };
        root.style.borderLeftWidth = 4;
        root.style.paddingLeft = 4;
        root.style.marginBottom = 4;

        var views = new Views
        {
            Root = root,
            Property = property,
            Yaw = new PropertyField(property.FindPropertyRelative(ColliderDef.YawOffsetField)),
            Targets = new PropertyField(property.FindPropertyRelative(ColliderDef.TargetsField)),
            Warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning),
        };

        root.Add(new PropertyField(property.FindPropertyRelative(ColliderDef.KeyField)));
        root.Add(new PropertyField(property.FindPropertyRelative(ColliderDef.RoleField)));
        root.Add(views.Warning);
        root.Add(new PropertyField(property.FindPropertyRelative(ColliderDef.ShapeField)));
        root.Add(new PropertyField(property.FindPropertyRelative(ColliderDef.OffsetField)));
        root.Add(views.Yaw);
        root.Add(views.Targets);

        Refresh(views);

        // 名前(文字)、役割と形(列挙型)が変わったら、表示を更新する。
        // TrackPropertyValue は使わない(Inspector が作り直されたときに、消えた SerializedObject を参照して、エラーになることがあるため)
        root.RegisterCallback<ChangeEvent<string>>(_ => Refresh(views));
        root.RegisterCallback<ChangeEvent<Enum>>(_ => Refresh(views));

        return root;
    }

    private static void Refresh(Views views)
    {
        var property = views.Property;
        var key = property.FindPropertyRelative(ColliderDef.KeyField).stringValue;

        // enumValueIndex は、enum の値が 0 から順に並んでいる前提(新しい役割は、末尾に足す)
        var role = (ColliderRole)property.FindPropertyRelative(ColliderDef.RoleField).enumValueIndex;
        var kind = (ColliderShapeKind)property.FindPropertyRelative(ColliderDef.ShapeField)
            .FindPropertyRelative(ColliderShape.KindField).enumValueIndex;
        var color = ColliderColors.Of(role);

        // 見出しと、役割の色
        views.Root.text = $"{key} ({role})";
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
