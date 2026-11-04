using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// ColliderShape の Inspector 表示。形(Kind)に合わせて、使う項目だけを表示する
[CustomPropertyDrawer(typeof(ColliderShape))]
public sealed class ColliderShapeDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var kindProperty = property.FindPropertyRelative(ColliderShape.KindField);

        var root = new Foldout { text = property.displayName, value = true };

        var kindField = new PropertyField(kindProperty);
        var radiusField = new PropertyField(property.FindPropertyRelative(ColliderShape.RadiusField));
        var lengthField = new PropertyField(property.FindPropertyRelative(ColliderShape.LengthField));
        var widthField = new PropertyField(property.FindPropertyRelative(ColliderShape.WidthField));
        var angleField = new PropertyField(property.FindPropertyRelative(ColliderShape.AngleField));
        var innerRangeField = new PropertyField(property.FindPropertyRelative(ColliderShape.InnerRangeField));

        root.Add(kindField);
        root.Add(radiusField);
        root.Add(lengthField);
        root.Add(widthField);
        root.Add(angleField);
        root.Add(innerRangeField);

        // 最初の表示(enumValueIndex は、enum の値が 0 から順に並んでいる前提)
        Apply((ColliderShapeKind)kindProperty.enumValueIndex, radiusField, lengthField, widthField, angleField, innerRangeField);

        // 形が変わったら、表示する項目を切り替える
        root.TrackPropertyValue(kindProperty, changed =>
            Apply((ColliderShapeKind)changed.enumValueIndex, radiusField, lengthField, widthField, angleField, innerRangeField));

        return root;
    }

    // 形に合わせて、使う項目だけを表示し、項目の名前も合わせる
    private static void Apply(
        ColliderShapeKind kind,
        PropertyField radius,
        PropertyField length,
        PropertyField width,
        PropertyField angle,
        PropertyField innerRange)
    {
        SetVisible(radius, kind != ColliderShapeKind.Box);
        SetVisible(length, kind == ColliderShapeKind.Capsule || kind == ColliderShapeKind.Box);
        SetVisible(width, kind == ColliderShapeKind.Box);
        SetVisible(angle, kind == ColliderShapeKind.Sector);
        SetVisible(innerRange, kind == ColliderShapeKind.Circle || kind == ColliderShapeKind.Sector);

        radius.label = kind == ColliderShapeKind.Sector ? "範囲(外側の半径)" : "半径";
        length.label = kind == ColliderShapeKind.Box ? "前後の長さ" : "長さ(中心線)";
        width.label = "左右の幅";
        angle.label = "角度";
        innerRange.label = "手前の当たらない範囲";
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
