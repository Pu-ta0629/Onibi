using System;
using UnityEditor;
using UnityEngine;

public class StageIdCounter : ScriptableObject
{
    private const string AssetPath = "Assets/Stages/StageIdCounter.asset";

    [SerializeField] int _nextId = 1;

    private const string NextIdField = nameof(_nextId);

    public static int Take(Predicate<int> isUsed)
    {
        var counter = LoadOrCreate();

        var so = new SerializedObject(counter);
        var prop = so.FindProperty(NextIdField);

        var id = Mathf.Max(1, prop.intValue);
        while (isUsed(id)) id++;

        prop.intValue = id + 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(counter);
        AssetDatabase.SaveAssets();

        return id;
    }

    private static StageIdCounter LoadOrCreate()
    {
        var counter = AssetDatabase.LoadAssetAtPath<StageIdCounter>(AssetPath);

        if (counter != null) return counter;

        counter = CreateInstance<StageIdCounter>();
        AssetDatabase.CreateAsset(counter, AssetPath);
        return counter;
    }
}
