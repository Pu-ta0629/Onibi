using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class StageSceneEditor
{
    private const float MarkerSize = 0.4f;

    private static StageData _target;
    private static string _selectedPath;

    public static StageData Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            _target = value;
            _selectedPath = null;
            SceneView.RepaintAll();
        }
    }

    static StageSceneEditor() => SceneView.duringSceneGui += OnSceneGUI;

    //新規は位置の基準位置(Sceneビューの注視点)
    private static Vector3 PivotPosition()
    {
        var view = SceneView.lastActiveSceneView;
        return view != null ? view.pivot : Vector3.zero;
    }

    //---- 描画と移動 -----

    private static void OnSceneGUI(SceneView view)
    {
        if (_target == null) return;

        var so = new SerializedObject(_target);
        so.Update();

        DrawElement(so.FindProperty(StageData.StartField), "Start", Color.cyan, false);
        DrawList(so, StageData.GoalsField, Color.green, "Goal", true);
        DrawList(so, StageData.EnemiesField, Color.red, "Enemy", false);
        DrawList(so, StageData.ItemsField, Color.yellow, "Item", false);

        so.ApplyModifiedProperties();
    }

    private static void DrawList(SerializedObject so,string listField,Color color,string label,bool isGoal)
    {
        var list = so.FindProperty(listField);
        for(var i = 0; i < list.arraySize;i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            DrawElement(element, $"{label}{i + 1}", color, isGoal);
        }
    }

    private static void DrawElement(SerializedProperty element,string label,Color color,bool isGoal)
    {
        var posProp = element.FindPropertyRelative(PlacementData.PositionField);
        var rotProp = element.FindPropertyRelative(PlacementData.RotationYField);
        var pos = posProp.vector3Value;
        var rot = Quaternion.Euler(0f, rotProp.floatValue, 0f);

        //敵とアイテムはIDもラベルに出す
        var enemyProp = element.FindPropertyRelative(EnemyPlacement.EnemyField);
        var itemId = element.FindPropertyRelative(ItemPlacement.ItemIdField);
        if (enemyProp != null)
        {
            var enemy = enemyProp.objectReferenceValue as EnemyData;
            var sizeProp = element.FindPropertyRelative(EnemyPlacement.SizeField);
            var sizeClass = (SizeClass)sizeProp.enumValueIndex;
            label += enemy != null ? $"({enemy.DisplayName} / {sizeClass})" : $"(未設定 / {sizeClass})";

        }
        if (itemId != null) label += $"(ID:{itemId.intValue})";

        var selected = element.propertyPath == _selectedPath;
        Handles.color = selected ? Color.white : color;

        if(isGoal)
        {
            var radius = element.FindPropertyRelative(GoalPlacement.RadiusField).floatValue;
            Handles.DrawWireDisc(pos, Vector3.up, radius);
        }

        Handles.DrawLine(pos, pos + rot * Vector3.forward * (MarkerSize * 3f));
        Handles.Label(pos + Vector3.up * (MarkerSize * 1.5f), label);

        var size = HandleUtility.GetHandleSize(pos) * MarkerSize;
        if (Handles.Button(pos, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
            _selectedPath = element.propertyPath;

        if (!selected) return;
        
        //選択中のものだけ、移動ハンドルを出す
        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
            posProp.vector3Value = newPos;

        if(isGoal)
        {
            var radiusProp = element.FindPropertyRelative(GoalPlacement.RadiusField);
            EditorGUI.BeginChangeCheck();
            var newRadius = Handles.RadiusHandle(Quaternion.identity, pos, radiusProp.floatValue);
            if (EditorGUI.EndChangeCheck())
                radiusProp.floatValue = Mathf.Max(0.1f, newRadius);
        }
    }

    // ---- 追加と削除(StageEditorWindow のボタンから呼ぶ) ----

    public static void AddGoal() => Add(StageData.GoalsField, element =>
        element.FindPropertyRelative(GoalPlacement.RadiusField).floatValue = 1f);

    public static void AddEnemy() => Add(StageData.EnemiesField, element =>
        {
            element.FindPropertyRelative(EnemyPlacement.EnemyField).objectReferenceValue = null;
            element.FindPropertyRelative(EnemyPlacement.SizeField).enumValueIndex = (int)SizeClass.Medium;
        });

    public static void AddItem() => Add(StageData.ItemsField, element =>
        element.FindPropertyRelative(ItemPlacement.ItemIdField).intValue = 1);

    private static void Add(string listField,Action<SerializedProperty> init)
    {
        if (_target == null) return;

        var so = new SerializedObject(_target);
        var list = so.FindProperty(listField);
        list.arraySize++;
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);

        element.FindPropertyRelative(PlacementData.PositionField).vector3Value = PivotPosition();
        element.FindPropertyRelative(PlacementData.RotationYField).floatValue = 0f;
        init(element);

        so.ApplyModifiedProperties();
        _selectedPath = element.propertyPath;
        SceneView.RepaintAll();
    }

    public static void SetStartHere()
    {
        if(_target == null) return;

        var so = new SerializedObject(_target);
        var start = so.FindProperty(StageData.StartField);
        start.FindPropertyRelative(PlacementData.PositionField).vector3Value = PivotPosition();
        so.ApplyModifiedProperties();

        _selectedPath = start.propertyPath;
        SceneView.RepaintAll();
    }

    public static void RemoveSelected()
    {
        if(_target == null || string.IsNullOrEmpty(_selectedPath)) return;

        var open = _selectedPath.IndexOf("[");
        if(open < 0) return;

        var close = _selectedPath.IndexOf("]");
        var listName = _selectedPath.Substring(0, _selectedPath.IndexOf('.'));
        var index = int.Parse(_selectedPath.Substring(open + 1,close - open -1));

        var so = new SerializedObject(_target);
        var list = so.FindProperty(listName);
        if (index < list.arraySize) list.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();

        _selectedPath = null;
        SceneView.RepaintAll();
    }
}
