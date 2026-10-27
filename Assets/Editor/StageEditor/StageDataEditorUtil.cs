using UnityEditor;
using UnityEngine;

public static class StageDataEditorUtil
{
    public static void AddEmeny(StageData stage,int enemyId,Vector3 position)
    {
        var so = new SerializedObject(stage);
        var list = so.FindProperty(StageData.EnemiesField);

        list.arraySize++;
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);

        element.FindPropertyRelative(EnemyPlacement.EnemyField).intValue = enemyId;
        element.FindPropertyRelative(PlacementData.PositionField).vector3Value = position;
        element.FindPropertyRelative(PlacementData.RotationYField).floatValue = 0f;

        so.ApplyModifiedProperties();   //Undo と保存の通知もここで処理される  
    }
}
