using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สมุดเก็บเอกสาร/โน้ต — เก็บแล้วอยู่ถาวร ไม่กินช่องกระเป๋า
/// ลงทะเบียนใน ServiceLocator แบบเดียวกับ service ตัวอื่นในโปรเจกต์
/// </summary>
public class DocumentLog : MonoBehaviour, IDocumentLog
{
    [Header("Debug (ดูเฉยๆ ตอนเล่น)")]
    [SerializeField] private List<DocumentData> documents = new List<DocumentData>();

    // เก็บ id ที่อ่านแล้ว แยกจากตัวเอกสาร เพื่อไม่ต้องไปแก้ค่าใน ScriptableObject
    // (ถ้าเขียนลง asset ตรงๆ ค่าจะค้างข้ามรอบเล่นใน Editor)
    private readonly HashSet<string> readIds = new HashSet<string>();

    public event Action OnChanged;
    public IReadOnlyList<DocumentData> All => documents;

    private void Awake()
    {
        ServiceLocator.Register<IDocumentLog>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IDocumentLog>();
    }

    public void Collect(DocumentData doc)
    {
        if (doc == null) return;

        if (string.IsNullOrEmpty(doc.documentId))
        {
            Debug.LogWarning($"[DocumentLog] เอกสาร \"{doc.name}\" ยังไม่ได้ตั้ง Document Id — เก็บซ้ำได้ไม่จำกัด");
        }
        else if (HasCollected(doc.documentId))
        {
            return;   // เก็บไปแล้ว ไม่เก็บซ้ำ
        }

        documents.Add(doc);
        Debug.Log($"<color=cyan>📄 เก็บเอกสาร: {doc.title}</color>");
        OnChanged?.Invoke();
    }

    public bool HasCollected(string documentId)
    {
        if (string.IsNullOrEmpty(documentId)) return false;

        foreach (DocumentData d in documents)
        {
            if (d != null && d.documentId == documentId) return true;
        }
        return false;
    }

    public bool HasRead(string documentId)
    {
        return !string.IsNullOrEmpty(documentId) && readIds.Contains(documentId);
    }

    public void MarkRead(string documentId)
    {
        if (string.IsNullOrEmpty(documentId)) return;
        if (!readIds.Add(documentId)) return;   // อ่านไปแล้ว ไม่ต้องยิง event ซ้ำ

        OnChanged?.Invoke();
    }
}
