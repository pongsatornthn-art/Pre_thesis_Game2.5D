using System;
using UnityEngine;

/// <summary>
/// แปะคู่กับ [SerializeReference] เพื่อให้ Inspector มีปุ่มเลือก "ชนิด" (คลาสลูก) ของช่องนั้น
///
/// ทำไมต้องมี: Unity เก็บ interface / คลาสแม่แบบ abstract ได้ด้วย [SerializeReference]
/// แต่ Inspector ไม่มีเมนูให้เลือกว่าจะใส่คลาสลูกตัวไหน — ช่องจะว่างตลอดและกรอกอะไรไม่ได้เลย
/// ตัวลากเมนูอยู่ที่ Assets/Script/Editor/SubclassPickerDrawer.cs
///
/// วิธีใช้:
///   [SerializeReference, SubclassPicker] public IStoryCondition condition;
///   [SerializeReference, SubclassPicker] public List&lt;IStoryAction&gt; actions;
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class SubclassPickerAttribute : PropertyAttribute
{
}

/// <summary>
/// ตั้งชื่อที่โชว์ในเมนูเลือกชนิด (ไม่ใส่ = ใช้ชื่อคลาส) — ใส่ "/" เพื่อจัดหมวด เช่น "เป้าหมาย/เก็บของครบ N ชิ้น"
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PickerNameAttribute : Attribute
{
    public string Name { get; }
    public PickerNameAttribute(string name) => Name = name;
}
