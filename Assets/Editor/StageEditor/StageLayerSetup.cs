using UnityEditor;
using UnityEngine;

// StageGroundProviderが使うレイヤーが、プロジェクトに無ければ、空いている場所に作る
[InitializeOnLoad]
public static class StageLayerSetup
{
    // 0から7は、Unityが予約している。User Layerは、8から
    private const int FirstUserLayer = 8;
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";

    static StageLayerSetup()
    {
        // 起動の直後は、他の処理と重なるので、少し後で実行する
        EditorApplication.delayCall += EnsureAll;
    }

    [MenuItem("Tools/Stage Tools/Ensure Layers")]
    private static void EnsureAll()
    {
        // 再生中は、設定ファイルを書き換えない
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        foreach (TerrainType terrain in System.Enum.GetValues(typeof(TerrainType)))
            EnsureLayer(StageLayers.GetLayerName(terrain));
    }

    // レイヤーが無ければ作る。すでにある、または作れたらtrue。空きが無いなどで作れなければfalse
    public static bool EnsureLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0) return true;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"[StageLayerSetup] {TagManagerPath} が読み込めません");
            return false;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogWarning("[StageLayerSetup] TagManagerの layers が見つかりません");
            return false;
        }

        for (int i = FirstUserLayer; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[StageLayerSetup] レイヤー {layerName} を、User Layer {i} に作りました");
            return true;
        }

        Debug.LogWarning($"[StageLayerSetup] User Layerに空きがなく、{layerName} を作れませんでした");
        return false;
    }
}