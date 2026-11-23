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
        root.Add(new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.IdField))); ;
        root.Add(new PropertyField(kindProp));
        root.Add(new PropertyField(property.FindPropertyRelative(PlacementData.PositionField)));
        root.Add(new PropertyField(property.FindPropertyRelative(RadiusPlacement.RadiusField)));

        // 種類ごとの項目
        PropertyField enemyList = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.EnemyListField));
        PropertyField enemySize = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.EnemySizeField));
        PropertyField kodamaData = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.KodamaDataField));
        PropertyField kodamaCount = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.KodamaCountField));
        PropertyField specialKodamas = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.SpecialKodamasField));
        PropertyField spawnInterval = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.SpawnIntervalField));
        PropertyField resumeThreshold = new PropertyField(property.FindPropertyRelative(SpawnSpotPlacement.ResumeThresholdField));
        root.Add(enemyList);
        root.Add(enemySize);
        root.Add(kodamaData);
        root.Add(kodamaCount);
        root.Add(specialKodamas);
        root.Add(spawnInterval);
        root.Add(resumeThreshold);

        void Refresh()
        {
            SpawnKind kind = (SpawnKind)kindProp.enumValueIndex;
            bool isEnemy = kind == SpawnKind.Enemy;
            bool isKodama = kind == SpawnKind.Kodama;

            SetVisible(enemyList, isEnemy);
            SetVisible(enemySize, isEnemy);
            SetVisible(kodamaData, isKodama);
            SetVisible(kodamaCount, isKodama);
            SetVisible(specialKodamas, kind == SpawnKind.SpecialKodama);
            SetVisible(spawnInterval, isKodama || isEnemy);
            SetVisible(resumeThreshold, isKodama || isEnemy);
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