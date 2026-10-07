using UnityEngine;

/// <summary>
/// "เหตุผลที่ของชิ้นนี้ควรโผล่/ไม่โผล่" 1 ข้อ — component ไหนอยากคุมการโผล่ของ object ให้ implement ตัวนี้
///   WorldPresence   : โผล่เฉพาะโลกที่เลือก
///   StoryCollectible: เก็บไปแล้ว = ไม่โผล่
/// หลาย component บน object เดียวกัน → ต้องผ่าน "ทุกข้อ" ถึงจะโผล่ (ไม่ชนกัน ไม่แย่งกันเปิด/ปิด)
/// อยากได้เหตุผลใหม่ (เช่น โผล่เฉพาะกลางคืน) = สร้าง component ใหม่ implement ตัวนี้ (Open-Closed)
/// </summary>
public interface IPresenceGate
{
    bool AllowVisible { get; }
}

/// <summary>
/// ตัวรวมผล — ถามทุก IPresenceGate บน object แล้วเปิด/ปิด "หน้าตา + ตัวชน" ของ object และลูก
/// **ไม่ปิดตัว object** (SetActive) เพราะ component ต้องยังฟังเหตุการณ์อยู่ (ปิดไปแล้วจะไม่มีวันเปิดกลับเอง)
/// ไม่สร้าง/แปะอะไรเพิ่มตอนเล่น — เปิดปิด Renderer/Collider/Canvas ที่วางไว้ในซีนอยู่แล้วเท่านั้น
/// </summary>
public static class ScenePresence
{
    public static bool IsAllowed(GameObject go)
    {
        foreach (IPresenceGate gate in go.GetComponents<IPresenceGate>())
        {
            if (gate is Behaviour b && !b.enabled) continue;   // ปิดคอมโพเนนต์ = ไม่นับเหตุผลข้อนั้น
            if (!gate.AllowVisible) return false;
        }
        return true;
    }

    /// <summary>คำนวณใหม่แล้วเปิด/ปิดหน้าตา+ตัวชน — gate ไหนเปลี่ยนให้เรียกตัวนี้</summary>
    public static void Refresh(GameObject go)
    {
        if (go == null) return;
        bool visible = IsAllowed(go);

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = visible;
        foreach (Canvas cv in go.GetComponentsInChildren<Canvas>(true)) cv.enabled = visible;
    }
}
