using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// 湧きスポットのInspector。種類によって、使う項目だけを表示する
[CustomPropertyDrawer(typeof(SpawnSpotPlacement))]
public class SpawnSpotPlacementDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();

        SerializedProperty kindProp = property.FindPropertyRelative(SpawnSpotPlacement.KindField);

        // 常に表示する項目
        root.Add(new PropertyField(kindProp));
        root.Add(new PropertyField(property.FindPropertyRelative(PlacementData.PositionField)));
        root.Add(new PropertyField(property.FindPropertyRelative(RadiusPlacement.RadiusField)));

        // 種類ごとの項目
        PropertyField enemyList = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.EnemyListField));
        PropertyField enemySize = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.EnemySizeField));
        PropertyField specialKodamas = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.SpecialKodamasField));
        PropertyField spawnInterval = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.SpawnIntervalField));
        root.Add(enemyList);
        root.Add(enemySize);
        root.Add(specialKodamas);
        root.Add(spawnInterval);

        void Refresh()
        {
            SpawnKind kind = (SpawnKind)kindProp.enumValueIndex;
            SetVisible(enemyList, kind == SpawnKind.Enemy);
            SetVisible(enemySize, kind == SpawnKind.Enemy);
            SetVisible(specialKodamas, kind == SpawnKind.SpecialKodama);
            SetVisible(spawnInterval, kind == SpawnKind.Kodama || kind == SpawnKind.Enemy);
        }

        Refresh();
        root.TrackPropertyValue(kindProp, _ => Refresh());
        return root;
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}