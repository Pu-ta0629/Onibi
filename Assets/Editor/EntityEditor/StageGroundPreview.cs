using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class StageGroundPreview
{
    private static readonly StageGroundBuilder Builder = new StageGroundBuilder();

    static StageGroundPreview()
    {
        Undo.undoRedoPerformed += Refresh;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Builder.Clear; //再コンパイルで、残骸を残さない
    }

    // 選択中のステージで、作り直す。選択が無ければ消す
    public static void Refresh()
    {
        StageData stage = StageSceneEditor.Target;

        // 再生中は、実行時の地面があるので、プレビューは出さない
        if(stage == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Builder.Clear();
            return;
        }

        Builder.Build(stage.WalkableGrid, new StageTerrainColors(), HideFlags.HideAndDontSave);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) Builder.Clear();
        else if (state == PlayModeStateChange.EnteredEditMode) Refresh();
    }
}
