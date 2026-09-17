using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สมุดเก็บเอกสาร/โน้ต — เก็บแล้วอยู่ถาวร ไม่กินช่องกระเป๋า
/// ลงทะเบียนใน ServiceLocator แบบเดียวกับ service ตัวอื่นในโปรเจกต์
/// รองรับการบันทึก/โหลดสถานะเกมผ่าน ISaveable
/// </summary>
public class DocumentLog : MonoBehaviour, IDocumentLog, ISaveable
{
    [Header("Debug (ดูเฉยๆ ตอนเล่น)")]
    [SerializeField] private List<DocumentData> documents = new List<DocumentData>();

    [Header("Catalog สำรองสำหรับโหลดเซฟ (เว้นว่างได้ ระบบจะค้นหาจาก Resources ด้วย)")]
    [SerializeField] private List<DocumentData> documentCatalog = new List<DocumentData>();

    // เก็บ id ที่อ่านแล้ว แยกจากตัวเอกสาร เพื่อไม่ต้องไปแก้ค่าใน ScriptableObject
    // (ถ้าเขียนลง asset ตรงๆ ค่าจะค้างข้ามรอบเล่นใน Editor)
    private readonly HashSet<string> readIds = new HashSet<string>();

    public event Action OnChanged;
    public IReadOnlyList<DocumentData> All => documents;

    [System.Serializable]
    private struct SaveData
    {
        public List<string> savedDocumentIds;
        public List<string> savedReadIds;
    }

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
        Debug.Log($"<color=cyan>📄 เก็บเอกสาร: {doc.Title}</color>");
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

    #region ISaveable Implementation

    public string CaptureState()
    {
        SaveData data = new SaveData
        {
            savedDocumentIds = new List<string>(),
            savedReadIds = new List<string>(readIds)
        };

        foreach (DocumentData d in documents)
        {
            if (d != null && !string.IsNullOrEmpty(d.documentId))
            {
                data.savedDocumentIds.Add(d.documentId);
            }
        }

        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);

        // 1. คืนค่า readIds
        readIds.Clear();
        if (data.savedReadIds != null)
        {
            foreach (string id in data.savedReadIds)
            {
                if (!string.IsNullOrEmpty(id)) readIds.Add(id);
            }
        }

        // 2. คืนค่า documents
        documents.Clear();
        if (data.savedDocumentIds == null || data.savedDocumentIds.Count == 0)
        {
            OnChanged?.Invoke();
            return;
        }

        // โหลดจาก Resources แบบขี้เกียจ — ถ้า Catalog ครบก็ไม่ต้องแตะดิสก์เลย
        DocumentData[] resourceDocs = null;

        foreach (string docId in data.savedDocumentIds)
        {
            if (string.IsNullOrEmpty(docId)) continue;

            DocumentData matchedDoc = null;

            // 1. หาใน Catalog
            if (documentCatalog != null)
            {
                matchedDoc = documentCatalog.Find(d => d != null && d.documentId == docId);
            }

            // 2. หาใน Resources
            if (matchedDoc == null)
            {
                if (resourceDocs == null) resourceDocs = Resources.LoadAll<DocumentData>("");

                for (int i = 0; i < resourceDocs.Length; i++)
                {
                    if (resourceDocs[i] != null && resourceDocs[i].documentId == docId)
                    {
                        matchedDoc = resourceDocs[i];
                        break;
                    }
                }
            }

            // 3. หาไม่เจอ = สร้างตัวชั่วคราว แต่เอกสารจะไม่มีเนื้อหาและไม่มีรูป ต้องเตือนไว้
            if (matchedDoc == null)
            {
                Debug.LogWarning(
                    $"[DocumentLog] โหลดเซฟแล้วหา asset เอกสาร \"{docId}\" ไม่เจอ " +
                    $"— สร้างตัวชั่วคราวแทน (ชื่อกับเนื้อหาจะหาย)\n" +
                    $"แก้โดยลาก asset เอกสารใส่ช่อง Document Catalog ที่ก้อน [JOURNAL] หรือย้าย asset ไปโฟลเดอร์ Resources");

                matchedDoc = ScriptableObject.CreateInstance<DocumentData>();
                matchedDoc.documentId = docId;
                matchedDoc.titleKey = docId;
            }

            documents.Add(matchedDoc);
        }

        OnChanged?.Invoke();
    }

    #endregion
}
