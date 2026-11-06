using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// コライダーの、コピーと貼り付け。クリップボードには、JSON の文章として入る
public static class ColliderClipboard
{
    private const string Marker = "EntityEditor.Colliders";

    // クリップボードに入れる中身(別のものが入っていても、見分けるための印を付ける)
    [Serializable]
    public sealed class Payload
    {
        public string Tag = Marker;
        public List<ColliderDef> Items = new List<ColliderDef>();
    }

    // コライダーをコピーする。コピーできたら true
    public static bool Copy(IEnumerable<ColliderDef> defs)
    {
        var payload = new Payload();
        foreach (var def in defs)
        {
            if (def != null) payload.Items.Add(def);
        }

        if (payload.Items.Count == 0) return false;

        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(payload);
        return true;
    }

    // クリップボードから、コライダーを読む。コピーしたものでなければ false
    public static bool TryRead(out List<ColliderDef> defs)
    {
        defs = null;

        var text = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(text) || !text.Contains(Marker)) return false;

        try
        {
            var payload = JsonUtility.FromJson<Payload>(text);
            if (payload == null || payload.Tag != Marker || payload.Items == null || payload.Items.Count == 0) return false;

            defs = payload.Items;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // コライダーの一覧の末尾に、貼り付ける。貼り付けた数を返す(0 は、貼り付けるものがなかった)
    // collidersPath: コライダーの一覧の、プロパティの場所
    public static int Paste(SerializedObject serializedObject, string collidersPath)
    {
        if (!TryRead(out var defs)) return 0;

        serializedObject.Update();
        var list = serializedObject.FindProperty(collidersPath);
        if (list == null) return 0;

        // すでにある名前(Key)を集めておき、重ならないようにする
        var used = new HashSet<string>();
        for (var i = 0; i < list.arraySize; i++)
            used.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative(ColliderDef.KeyField).stringValue);

        foreach (var def in defs)
        {
            list.arraySize++;
            var element = list.GetArrayElementAtIndex(list.arraySize - 1);
            WriteDef(element, def, UniqueKey(def.Key, used));
        }

        serializedObject.ApplyModifiedProperties();   // 元に戻す(Ctrl+Z)の記録も、ここで行われる
        return defs.Count;
    }

    // 名前(Key)が重なるときは、_2、_3 と番号を付ける
    private static string UniqueKey(string key, HashSet<string> used)
    {
        var baseKey = string.IsNullOrWhiteSpace(key) ? "collider" : key;
        var candidate = baseKey;

        for (var n = 2; used.Contains(candidate); n++) candidate = $"{baseKey}_{n}";

        used.Add(candidate);
        return candidate;
    }

    // 追加した要素には、直前の要素の値が入っているので、すべての項目を、明示的に書き込む
    private static void WriteDef(SerializedProperty element, ColliderDef def, string key)
    {
        element.FindPropertyRelative(ColliderDef.KeyField).stringValue = key;
        element.FindPropertyRelative(ColliderDef.RoleField).enumValueIndex = (int)def.Role;
        element.FindPropertyRelative(ColliderDef.OffsetField).vector2Value = def.Offset;
        element.FindPropertyRelative(ColliderDef.YawOffsetField).floatValue = def.YawOffset;
        element.FindPropertyRelative(ColliderDef.TargetsField).enumValueFlag = (int)def.Targets;

        var shape = element.FindPropertyRelative(ColliderDef.ShapeField);
        var source = def.Shape;
        shape.FindPropertyRelative(ColliderShape.KindField).enumValueIndex = (int)source.Kind;
        shape.FindPropertyRelative(ColliderShape.RadiusField).floatValue = source.Radius;
        shape.FindPropertyRelative(ColliderShape.LengthField).floatValue = source.Length;
        shape.FindPropertyRelative(ColliderShape.WidthField).floatValue = source.Width;
        shape.FindPropertyRelative(ColliderShape.AngleField).floatValue = source.Angle;
        shape.FindPropertyRelative(ColliderShape.InnerRangeField).floatValue = source.InnerRange;
    }
}