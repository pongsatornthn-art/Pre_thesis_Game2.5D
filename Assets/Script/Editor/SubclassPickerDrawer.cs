using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาดปุ่มเลือกชนิดให้ช่องที่มี [SerializeReference, SubclassPicker]
/// หาคลาสลูกทั้งหมดอัตโนมัติด้วย TypeCache → สร้างคลาสลูกใหม่ก็โผล่ในเมนูเองไม่ต้องลงทะเบียน (OCP)
/// </summary>
[CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
public class SubclassPickerDrawer : PropertyDrawer
{
    private static readonly Dictionary<Type, Type[]> cache = new Dictionary<Type, Type[]>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.HelpBox(position, "[SubclassPicker] ต้องใช้คู่กับ [SerializeReference]", MessageType.Error);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // ปุ่มเลือกชนิดวางทางขวาของบรรทัดหัว — วาดก่อน PropertyField เพื่อให้รับคลิกก่อนตัวพับ/กาง
        Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect buttonRect = new Rect(header.x + EditorGUIUtility.labelWidth + 2f, header.y,
                                   header.width - EditorGUIUtility.labelWidth - 2f, header.height);

        Type current = GetCurrentType(property);
        string buttonText = current == null ? "— ยังไม่เลือก (คลิกเพื่อเลือก) —" : GetDisplayName(current);

        if (EditorGUI.DropdownButton(buttonRect, new GUIContent(buttonText), FocusType.Keyboard))
        {
            ShowMenu(property, current);
        }

        if (current == null)
        {
            EditorGUI.LabelField(header, label);
        }
        else
        {
            EditorGUI.PropertyField(position, property, label, true);
        }

        EditorGUI.EndProperty();
    }

    private void ShowMenu(SerializedProperty property, Type current)
    {
        Type baseType = GetFieldBaseType(property);
        if (baseType == null) return;

        GenericMenu menu = new GenericMenu();
        string path = property.propertyPath;
        SerializedObject so = property.serializedObject;

        menu.AddItem(new GUIContent("(ว่าง)"), current == null, () => Assign(so, path, null));
        menu.AddSeparator("");

        foreach (Type t in GetCandidates(baseType))
        {
            Type captured = t;
            menu.AddItem(new GUIContent(GetDisplayName(t)), t == current, () => Assign(so, path, captured));
        }

        menu.ShowAsContext();
    }

    private static void Assign(SerializedObject so, string path, Type type)
    {
        so.Update();
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null) return;

        prop.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
        so.ApplyModifiedProperties();
    }

    private static Type[] GetCandidates(Type baseType)
    {
        if (cache.TryGetValue(baseType, out Type[] found)) return found;

        found = TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => !t.IsAbstract && !t.IsGenericType && !t.IsInterface
                        && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                        && t.IsDefined(typeof(SerializableAttribute), false)
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(GetDisplayName)
            .ToArray();

        cache[baseType] = found;
        return found;
    }

    private static string GetDisplayName(Type t)
    {
        PickerNameAttribute attr = t.GetCustomAttribute<PickerNameAttribute>();
        return attr != null ? attr.Name : ObjectNames.NicifyVariableName(t.Name);
    }

    private static Type GetCurrentType(SerializedProperty property)
    {
        return ParseTypename(property.managedReferenceFullTypename);
    }

    private static Type GetFieldBaseType(SerializedProperty property)
    {
        return ParseTypename(property.managedReferenceFieldTypename);
    }

    // รูปแบบที่ Unity ให้มา: "AssemblyName Namespace.TypeName"
    private static Type ParseTypename(string typename)
    {
        if (string.IsNullOrEmpty(typename)) return null;

        int space = typename.IndexOf(' ');
        if (space < 0) return null;

        string assembly = typename.Substring(0, space);
        string className = typename.Substring(space + 1).Replace('/', '+'); // คลาสซ้อน Unity ใช้ "/" แต่ .NET ใช้ "+"
        return Type.GetType($"{className}, {assembly}");
    }
}
