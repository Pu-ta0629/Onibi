using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// MasterData の作成・検査メニュー
// Tools > Master Data > New > Enemy / Enemy Type / Fire State / Lost Childe : 新しいアセットを、次の空き ID で作る
// Tools > Master Data > Validate : 全アセットの検査
public static class MasterDataTools
{
    private const string Root = "Assets/MasterData";

    //---- 新規作成 ----
    #region newCreate
    [MenuItem("Tools/Master Data/New/Enemy")]
    private static void NewEnemy() => Create<EnemyData>("Enemies", "Enemy");

    [MenuItem("Tools/Master Data/New/Enemy Type")]
    private static void NewEnemyType() => Create<EnemyTypeData>("EnemyTypes", "EnemyType");

    [MenuItem("Tools/Master Data/New/New Fire State")]
    private static void NewFireState() => Create<FireStateData>("FireStates", "FireState");

    [MenuItem("Tools/Master Data/New/Lost Childe")]
    private static void NewLostChilde() => Create<LostChilde>("LostChildren", "LostChilde");

    private static void Create<T>(string folderName, string prefix) where T : MasterData
    {
        var folder = $"{Root}/{folderName}";
        EnsureFolder(folder);

        var id = NextFreeId<T>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}_{id:000}.asset");

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty(MasterData.IdField).intValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static int NextFreeId<T>() where T : MasterData
    {
        var all = LoadAll<T>();
        return all.Count == 0 ? 1 : all.Max(a => a.Id) + 1;
    }

    public static List<T> LoadAll<T>() where T : MasterData
    {
        var list = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) { list.Add(asset); }
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
        var childes = LoadAll<LostChilde>();

        CheckIds(types, issues);
        CheckIds(enemies, issues);
        CheckIds(fires, issues);
        CheckIds(childes, issues);

        foreach (var type in types)
        {
            if (string.IsNullOrWhiteSpace(type.DisplayName))
                issues.Add(($"{type.name}: 名前が空です", type));
        }

        foreach (var enemy in enemies)
        {
            var label = enemy.name;

            if (string.IsNullOrWhiteSpace(enemy.DisplayName)) issues.Add(($"{label}: 名前が空です", enemy));
            if (enemy.Type == null) issues.Add(($"{label}: 種類が未設定です", enemy));
            if (enemy.MaxHP <= 0) issues.Add(($"{label}: HP は、1以上にしてください", enemy));
            if (enemy.BaseAttack < 0) issues.Add(($"{label}: 基礎攻撃力が、マイナスです", enemy));
            if (enemy.MoveSpeed < 0) issues.Add(($"{label}: 移動速度が、マイナスです", enemy));
            if (enemy.SearchRange < 0) issues.Add(($"{label}: 索敵範囲が、マイナスです", enemy));

            CheckColliders(label, enemy.Colliders, enemy, issues);
            CheckAttack($"{label} の Attack1", enemy.Attack1, enemy.Colliders, enemy, issues);
            CheckAttack($"{label} の Attack2", enemy.Attack2, enemy.Colliders, enemy, issues);
        }

        foreach (var fire in fires)
        {
            var label = fire.name;

            if (string.IsNullOrWhiteSpace(fire.FireState)) issues.Add(($"{label}: 火の状態(名前)が空です", fire));
            if (fire.Rank < 0) issues.Add(($"{label}: ランクが、マイナスです", fire));
            if (fire.MoveSpeed < 0) issues.Add(($"{label}: 移動速度が、マイナスです", fire));
            if (fire.LightRadius < 0) issues.Add(($"{label}: 明るさが、マイナスです", fire));
            if (fire.KodamaCount < 0) issues.Add(($"{label}: 木霊数が、マイナスです", fire));
            if (fire.NormalCooldown < 0 || fire.SpecialCooldown < 0) issues.Add(($"{label}: クールタイムが、マイナスです", fire));
            if (fire.NormalCost < 0 || fire.SpecialCost < 0) issues.Add(($"{label}: 消費量が、マイナスです", fire));
            if (fire.BurnPotency < 0 || fire.BurnCount < 0) issues.Add(($"{label}: 火傷威力か火傷回数が、マイナスです", fire));

            CheckColliders(label, fire.Colliders, fire, issues);
            CheckColliderKey($"{label} の通常攻撃", fire.NormalAttackKey, fire.Colliders, ColliderRole.Attack, fire, issues);
            CheckColliderKey($"{label} の固有行動", fire.SpecialAttackKey, fire.Colliders, ColliderRole.Attack, fire, issues);
        }

        foreach (var childe in childes)
        {
            var label = childe.name;

            if (string.IsNullOrWhiteSpace(childe.DisplayName)) issues.Add(($"{label}: 名前が空です", childe));

            CheckColliders(label, childe.Colliders, childe, issues);
        }

        foreach (var (message, context) in issues)
            Debug.LogWarning($"[MasterData] {message}", context);   // クリックすると、そのアセットが選ばれる

        Debug.Log(issues.Count == 0
            ? $"[MasterData] 問題はありません(種類 {types.Count}、敵 {enemies.Count}、火の状態 {fires.Count}、迷い子 {childes.Count})"
            : $"[MasterData] 問題が {issues.Count} 件あります");
    }

    // ID が、1以上で、重複していないか
    private static void CheckIds<T>(List<T> assets, List<(string message, Object context)> issues) where T : MasterData
    {
        foreach (var asset in assets)
        {
            if (asset.Id <= 0)
                issues.Add(($"{asset.name}: ID {asset.Id} は、1以上にしてください", asset));
        }

        foreach (var group in assets.GroupBy(a => a.Id).Where(g => g.Count() > 1))
        {
            var names = string.Join("、", group.Select(a => a.name));
            foreach (var asset in group)
                issues.Add(($"{typeof(T).Name}: ID {group.Key} が重複しています({names})", asset));
        }
    }

    // 敵の攻撃1つ分の検査(Pattern は、コライダーの一覧にある、Key を指す)
    private static void CheckAttack(string label, EnemyAttack attack, ColliderSet set, Object context, List<(string message, Object context)> issues)
    {
        if (string.IsNullOrWhiteSpace(attack.Pattern))
        {
            issues.Add(($"{label}: パターン(当たり判定の名前)が、未設定です", context));
        }
        else
        {
            CheckColliderKey(label, attack.ColliderKey, set, ColliderRole.Attack, context, issues);
        }

        if (attack.DamageRate < 0) issues.Add(($"{label}: ダメージ倍率が、マイナスです", context));
        if (attack.ChargeTime < 0) issues.Add(($"{label}: 溜め時間が、マイナスです", context));
        if (attack.ActiveTime <= 0) issues.Add(($"{label}: 有効時間が、0 以下です(攻撃のコライダーが、判定されません)", context));
        if (attack.Cooldown < 0) issues.Add(($"{label}: クールタイムが、マイナスです", context));
        if (attack.RecoveryTime < 0) issues.Add(($"{label}: 硬直時間が、マイナスです", context));
    }

    // コライダーの一覧の検査
    private static void CheckColliders(string label, ColliderSet set, Object context, List<(string message, Object context)> issues)
    {
        var keys = new HashSet<string>();
        var hasBody = false;

        foreach (var def in set.All)
        {
            if (def == null)
            {
                issues.Add(($"{label}: コライダーの一覧に、空の要素があります", context));
                continue;
            }

            var name = $"{label} のコライダー {def.Key}";

            if (string.IsNullOrWhiteSpace(def.Key)) issues.Add(($"{label}: 名前(Key)が空のコライダーがあります", context));
            else if (!keys.Add(def.Key)) issues.Add(($"{name}: 名前(Key)が重複しています", context));

            if (ColliderRules.IsBody(def.Role)) hasBody = true;

            if (!ColliderRules.Allows(def.Role, def.Shape.Kind))
                issues.Add(($"{name}: 役割 {def.Role} では、{def.Shape.Kind} の形は使えません", context));

            if (ColliderRules.NeedsTargets(def.Role) && def.Targets == Faction.None)
                issues.Add(($"{name}: 対象の陣営(Targets)が、未設定です", context));

            CheckShape(name, def.Shape, context, issues);
        }

        if (!hasBody) issues.Add(($"{label}: 体(Hit)のコライダーが、1つもありません", context));
    }

    // 形の検査
    private static void CheckShape(string label, ColliderShape shape, Object context, List<(string message, Object context)> issues)
    {
        switch (shape.Kind)
        {
            case ColliderShapeKind.Circle:
                if (shape.Radius <= 0f) issues.Add(($"{label}: 円の半径が、0 以下です", context));
                if (shape.InnerRange < 0f || (shape.InnerRange > 0f && shape.InnerRange >= shape.Radius))
                    issues.Add(($"{label}: 手前の当たらない範囲が、0 以上、半径未満になっていません", context));
                break;

            case ColliderShapeKind.Capsule:
                if (shape.Radius <= 0f) issues.Add(($"{label}: カプセルの半径が、0 以下です", context));
                if (shape.Length < 0f) issues.Add(($"{label}: カプセルの長さが、マイナスです", context));
                break;

            case ColliderShapeKind.Box:
                if (shape.Length <= 0f || shape.Width <= 0f) issues.Add(($"{label}: 四角の長さか幅が、0 以下です", context));
                break;

            case ColliderShapeKind.Sector:
                if (shape.Radius <= 0f) issues.Add(($"{label}: 扇形の範囲が、0 以下です", context));
                if (shape.Angle <= 0f || shape.Angle > 360f) issues.Add(($"{label}: 扇形の角度は、0 より大きく 360 以下にしてください", context));
                if (shape.InnerRange < 0f || (shape.InnerRange > 0f && shape.InnerRange >= shape.Radius))
                    issues.Add(($"{label}: 手前の当たらない範囲が、0 以上、範囲未満になっていません", context));
                break;
        }
    }

    // 攻撃などが指定した名前のコライダーが、一覧にあり、期待する役割になっているか
    private static void CheckColliderKey(string label, string key, ColliderSet set, ColliderRole expected, Object context, List<(string message, Object context)> issues)
    {
        if (!set.TryFind(key, out var def))
        {
            issues.Add(($"{label}: コライダー {key} が、一覧にありません", context));
            return;
        }

        if (def.Role != expected)
            issues.Add(($"{label}: コライダー {key} の役割が、{expected} ではありません(いまは {def.Role})", context));
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}