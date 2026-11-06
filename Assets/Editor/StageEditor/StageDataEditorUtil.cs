using UnityEditor;
using UnityEngine;

public static class StageDataEditorUtil
{
    // 修正: 名前をAddEmeny→AddEnemyに。敵はIDではなくEnemyData参照で持つ設計に合わせた
    public static void AddEnemy(StageData stage, EnemyData enemy, Vector3 position)
    {
        var so = new SerializedObject(stage);
        var list = so.FindProperty(StageData.EnemiesField);

        list.arraySize++;
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);

        element.FindPropertyRelative(EnemyPlacement.EnemyField).objectReferenceValue = enemy;
        element.FindPropertyRelative(EnemyPlacement.SizeField).enumValueIndex = (int)SizeClass.Medium;
        element.FindPropertyRelative(PlacementData.PositionField).vector3Value = position;
        element.FindPropertyRelative(PlacementData.RotationYField).floatValue = 0f;

        so.ApplyModifiedProperties();   // Undoと保存の通知もここで処理される
    }
}
