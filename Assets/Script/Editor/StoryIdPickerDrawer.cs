using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ช่องธง (StoryFlagId) / ตัวนับ (StoryCounterId) ทุกช่องใน Inspector จะมีปุ่ม ▾ ด้านขวา:
///   - รายชื่อทั้งโปรเจกต์ จัดหมวดตามโฟลเดอร์ (เช่น Act1_PTSD/Flag_Piece1) → คลิกเลือกได้เลย ไม่ต้องไปงมในหน้าต่าง Project
///   - "+ สร้างใหม่..." → ตั้งชื่อแล้วสร้างไฟล์ให้ทันที และใส่ในช่องนี้ให้เลย
/// ใช้กับทุกช่องอัตโนมัติ ไม่ต้องตั้งค่าอะไร (เป็นแค่เครื่องมือใน Editor ไม่มีผลกับเกม)
/// </summary>
[CustomPropertyDrawer(typeof(StoryFlagId))]
[CustomPropertyDrawer(typeof(StoryCounterId))]
public class StoryIdPickerDrawer : PropertyDrawer
{
    private const float ButtonWidth = 22f;
    private const string DefaultFolder = "Assets/StoryData";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        System.Type type = fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType()
                         : fieldInfo.FieldType.IsGenericType ? fieldInfo.FieldType.GetGenericArguments()[0]
                         : fieldInfo.FieldType;

        Rect fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
        Rect buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, EditorGUIUtility.singleLineHeight);

        // โชว์รหัสจริงใน tooltip — ช่องรหัสว่างจะเห็นว่าระบบใช้ชื่อไฟล์แทน
        Object current = property.objectReferenceValue;
        string realId = current is StoryFlagId f ? f.Id : current is StoryCounterId c ? c.Id : null;
        if (realId != null) label = new GUIContent(label.text, $"รหัสที่ระบบใช้: {realId}");

        EditorGUI.PropertyField(fieldRect, property, label);

        if (GUI.Button(buttonRect, "▾"))
        {
            ShowMenu(property, type);
        }
    }

    private static void ShowMenu(SerializedProperty property, System.Type type)
    {
        SerializedObject so = property.serializedObject;
        string path = property.propertyPath;
        GenericMenu menu = new GenericMenu();

        string kind = type == typeof(StoryCounterId) ? "ตัวนับ" : "ธง";
        menu.AddItem(new GUIContent($"+ สร้าง{kind}ใหม่..."), false, () => CreateNew(so, path, type));
        menu.AddItem(new GUIContent("(ว่าง)"), property.objectReferenceValue == null, () => Assign(so, path, null));
        menu.AddSeparator("");

        List<(string label, Object asset)> items = new List<(string, Object)>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
            if (asset == null) continue;

            // Assets/StoryData/Act1_PTSD/Flags/Flag_Piece1.asset → Act1_PTSD/Flag_Piece1 (โฟลเดอร์ Flags ตัดทิ้งให้สั้น)
            string rel = assetPath.StartsWith(DefaultFolder + "/") ? assetPath.Substring(DefaultFolder.Length + 1) : assetPath.Replace("Assets/", "");
            rel = rel.Replace("/Flags/", "/").Replace(".asset", "");
            items.Add((rel, asset));
        }
        items.Sort((a, b) => string.CompareOrdinal(a.label, b.label));

        Object selected = property.objectReferenceValue;
        foreach (var item in items)
        {
            Object captured = item.asset;
            menu.AddItem(new GUIContent(item.label), captured == selected, () => Assign(so, path, captured));
        }

        if (items.Count == 0) menu.AddDisabledItem(new GUIContent($"(ยังไม่มี{kind}ในโปรเจกต์)"));
        menu.ShowAsContext();
    }

    private static void Assign(SerializedObject so, string path, Object value)
    {
        so.Update();
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null) return;
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void CreateNew(SerializedObject so, string path, System.Type type)
    {
        // สร้างไว้โฟลเดอร์เดียวกับไฟล์ที่กำลังแก้ (เช่น เควสใน Act1_PTSD/Quests → Act1_PTSD/Flags) ถ้าหาได้
        string folder = DefaultFolder;
        string ownerPath = AssetDatabase.GetAssetPath(so.targetObject);
        if (!string.IsNullOrEmpty(ownerPath))
        {
            string ownerDir = Path.GetDirectoryName(ownerPath)?.Replace('\\', '/');
            string sibling = ownerDir != null ? Path.GetDirectoryName(ownerDir)?.Replace('\\', '/') + "/Flags" : null;
            folder = sibling != null && AssetDatabase.IsValidFolder(sibling) ? sibling : ownerDir ?? DefaultFolder;
        }
        if (!AssetDatabase.IsValidFolder(folder)) folder = "Assets";

        string prefix = type == typeof(StoryCounterId) ? "Counter_" : "Flag_";
        string file = EditorUtility.SaveFilePanelInProject("สร้างใหม่", prefix + "New", "asset", "ตั้งชื่อ (ชื่อไฟล์ = รหัส ถ้าไม่กรอกช่องรหัส)", folder);
        if (string.IsNullOrEmpty(file)) return;

        ScriptableObject asset = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(asset, file);
        AssetDatabase.SaveAssets();
        Assign(so, path, asset);
        EditorGUIUtility.PingObject(asset);
    }
}
