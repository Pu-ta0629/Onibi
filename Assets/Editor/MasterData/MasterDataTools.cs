using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

//MasterDataの作成・検査メニュー
//Tools>MasterData>New>Enemy/EnemyType/FireState : 新しいアセットを、次の空きIDで作る
//Tools>MasterData>Validate:全アセットの検査
public static class MasterDataTools
{
    const string Root = "Assets/MasterData";

    //----新規作成----
    #region newCreate
    [MenuItem("Tools/Master Data/New/Enemy")]
    private static void NewEnemy() => Create<EnemyData>("Enemies", "Enemy");

    [MenuItem("Tools/Master Data/New/Enemy Type")]
    private static void NewEnemyType() => Create<EnemyTypeData>("EnemyTypes", "EnemyType");

    [MenuItem("Tools/Master Data/New/New Fire State")]
    private static void NewFireState() => Create<FireStateData>("FireStates", "FireState");
    private static void Create<T>(string folderName,string prefix) where T : MasterData
    {
        var folder = $"{Root}/{folderName}";
        EnsureFolder(folder);

        var id = NextFreeId<T>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}_{id:000}.asset");

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path );

        var so = new SerializedObject(asset);
        so.FindProperty(MasterData.IdField).intValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    static int NextFreeId<T>() where T : MasterData
    {
        var all = LoadAll<T>();
        return all.Count == 0 ? 1 : all.Max(a => a.Id) + 1;
    }

    public static List<T> LoadAll<T>() where T : MasterData
    {
        var list = new List<T>();
        foreach(var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if(asset != null ) { list.Add(asset); }
        }
        return list;
    }

    #endregion

    // ---- 検査 ----

    [MenuItem("Tools/Master Data/Validate")]
    private static void Validate()
    {
        var issues = new List<(string message, Object context)>();

        var types = LoadAll<EnemyTypeData>();
        var enemies = LoadAll<EnemyData>();
        var fires = LoadAll<FireStateData>();

        CheckIds(types, issues);
        CheckIds(enemies, issues);
        CheckIds(fires, issues);

        foreach(var type in types)
        {
            if (string.IsNullOrWhiteSpace(type.DisplayName))
                issues.Add(($"{type.name}:名前が空です",type));
        }

        foreach(var enemy in enemies)
        {
            var label = enemy.name;

            if (string.IsNullOrWhiteSpace(enemy.DisplayName)) issues.Add(($"{label}: 名前が空です", enemy));
            if (enemy.Type == null) issues.Add(($"{label}: 種類が未設定です", enemy));
            if (enemy.MaxHP <= 0) issues.Add(($"{label}: HP は、1以上にしてください", enemy));
            if (enemy.BaseAttack < 0) issues.Add(($"{label}: HP は、基礎攻撃力が、マイナスです", enemy));
            if (enemy.MoveSpeed < 0) issues.Add(($"{label}: 移動速度が、マイナスです", enemy));
            if (enemy.SearchRange < 0) issues.Add(($"{label}: 索敵範囲が、マイナスです", enemy));

            CheckAttack($"{label}のAttack1", enemy.Attack1, enemy, issues);
            CheckAttack($"{label}のAttack2", enemy.Attack2, enemy, issues);
        }

        foreach(var fire in fires)
        {
            var label = fire.name;

            if (string.IsNullOrWhiteSpace(fire.FireState)) issues.Add(($"{label}: 火の状態(名前)が空です", fire));
            if (fire.Rank < 0) issues.Add(($"{label}: ランクが、マイナスです",fire));
            if (fire.MoveSpeed < 0) issues.Add(($"{label}: 移動速度が、マイナスです", fire));
            if (fire.LightRadius < 0) issues.Add(($"{label}: 明るさが、マイナスです", fire));
            if (fire.KodamaCount < 0) issues.Add(($"{label}: 木霊数が、マイナスです", fire));
            if (fire.NormalCooldown < 0 || fire.SpecialCooldown < 0) issues.Add(($"{label}: クールタイムが、マイナスです", fire));
            if (fire.NormalCost < 0 || fire.SpecialCost < 0) issues.Add(($"{label}: クールタイムが、マイナスです",fire));

            if (fire.BurnPotency < 0 || fire.BurnCount < 0) issues.Add(($"{label}: 火傷威力か火傷回数が、マイナスです", fire));
        }

        foreach (var (message, context) in issues)
            Debug.LogWarning($"[MasterData]{message}", context);

        Debug.Log(issues.Count == 0
            ? $"[MasterData] 問題はありません(種類 {types.Count}、敵 {enemies.Count}、火の状態 {fires.Count})"
            : $"[MasterData] 問題が {issues.Count} 件あります");
    }

    private static void CheckIds<T>(List<T> assets,List<(string message, Object context)> issues) where T : MasterData
    {
        foreach(var asset in assets)
        {
            if (asset.Id <= 0)
                issues.Add(($"{asset.name}: ID{asset.Id}は、1以上にしてください", asset));
        }

        foreach(var group in assets.GroupBy(a => a.Id).Where(g => g.Count() > 1))
        {
            var names = string.Join("、", group.Select(a => a.name));
            foreach (var asset in group)
                issues.Add(($"{typeof(T).Name}: ID{group.Key} が重複しています({names})", asset));
        }
    }

    private static void CheckAttack(string label,EnemyAttack attack,Object context,List<(string message,Object context)> issues)
    {
        if (string.IsNullOrWhiteSpace(attack.Pattern)) issues.Add(($"{label}: パターンが、未設定です", context));
        if (attack.DamageRate < 0) issues.Add(($"{label}: ダメージ倍率が、マイナスです",context));
        if (attack.Range < 0) issues.Add(($"{label}: 攻撃範囲が、マイナスです", context));
        if (attack.ChargeTime < 0) issues.Add(($"{label}: 溜め時間が、マイナスです。", context));
        if (attack.Cooldown < 0) issues.Add(($"{label}: クールタイムが、マイナスです",context));
        if (attack.RecoveryTime < 0) issues.Add(($"{label}: 硬直時間が、マイナスです", context)); 
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
