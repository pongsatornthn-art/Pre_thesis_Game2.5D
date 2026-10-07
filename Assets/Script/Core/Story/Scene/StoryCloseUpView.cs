using UnityEngine;

/// <summary>
/// มุมกล้อง Close-up แบบ FPS (เช่น หันเข้าหาโต๊ะวาดรูป) — ใช้กล้อง + FPSCameraController ของปอที่วางในซีน
/// ฉากเนื้อเรื่องสั่งด้วย "กล้อง/เปลี่ยนเป็นมุม Close-up" (ใส่ Scene Id ให้ตรงกัน) และ "กล้อง/กลับมุมปกติ"
///
/// วิธีตั้ง (ทำในซีนทั้งหมด ไม่มีโค้ดสร้างของ):
///   1. สร้าง object เปล่า เช่น "CloseUp_DrawingTable" แปะสคริปต์นี้ · ตั้ง Scene Id = "drawing_table"
///   2. ลาก/ก๊อปกล้อง FPS (ที่มี Camera + FPSCameraController) มาเป็นลูกของมัน แล้วจัดมุมให้หันไปที่โต๊ะ
///   3. ลากกล้อง FPS ใส่ช่อง Fps Camera · ลาก Main Camera ใส่ช่อง Main Camera
///   4. ช่อง Hide / Show / Disable ใส่เหมือน MiniGameManager (HUD, เป้าเล็ง, สคริปต์ผู้เล่น)
/// ⚠️ ตัว object ที่แปะสคริปต์นี้ต้องเปิดอยู่เสมอ (ปิดได้แค่กล้องลูก) ไม่งั้นฉากหามุมนี้ไม่เจอ
///
/// ไม่แก้ MiniGameManager / FPSCameraController ของปอ — แค่เปิด/ปิดของที่ลากมาใส่
/// </summary>
public class StoryCloseUpView : SceneIdentified<StoryCloseUpView>
{
    [Header("กล้อง")]
    [Tooltip("กล้อง FPS ที่จัดมุมไว้ (มี Camera + FPSCameraController) — ปกติให้ปิดไว้ในซีน")]
    [SerializeField] private GameObject fpsCamera;

    [Tooltip("กล้องหลักของเกม — จะถูกปิดตอนเข้ามุมนี้")]
    [SerializeField] private GameObject mainCamera;

    [Tooltip("ให้ผู้เล่นหันมองได้ด้วยเมาส์ (FPSCameraController ของปอ) · ว่าง = กล้องนิ่ง")]
    [SerializeField] private FPSCameraController lookController;

    [Header("ระหว่างอยู่มุมนี้ (แบบเดียวกับ MiniGameManager)")]
    [Tooltip("ซ่อน เช่น HUD หลัก")]
    [SerializeField] private GameObject[] hideWhileActive = new GameObject[0];

    [Tooltip("โชว์ เช่น เป้าเล็งกลางจอ")]
    [SerializeField] private GameObject[] showWhileActive = new GameObject[0];

    [Tooltip("ปิดสคริปต์ เช่น PlayerMovement / PlayerInteraction / PlayerCombat / ตัวเล็งกล้อง")]
    [SerializeField] private Behaviour[] disableWhileActive = new Behaviour[0];

    [Tooltip("ล็อกเมาส์ไว้กลางจอ (ต้องเปิดถ้าให้หันมองด้วยเมาส์)")]
    [SerializeField] private bool lockCursor = true;

    /// <summary>มุมที่เปิดอยู่ตอนนี้ (มีได้ทีละมุม)</summary>
    public static StoryCloseUpView Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => Active = null;

    private void Start()
    {
        // เริ่มซีน (รวมตอนโหลดเซฟ) = มุมปกติเสมอ
        if (Active != this) ApplyActive(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Active == this) Active = null;
    }

    /// <summary>กล้องของมุมนี้ — มินิเกม 3D ใช้ยิงเรย์หาจุดเล็ง</summary>
    public Camera ViewCamera => fpsCamera != null ? fpsCamera.GetComponentInChildren<Camera>(true) : null;

    public void Enter()
    {
        if (Active == this) return;
        if (Active != null) Active.Exit();   // มีได้ทีละมุม

        Active = this;
        ApplyActive(true);
    }

    public void Exit()
    {
        if (Active != this) return;
        Active = null;
        ApplyActive(false);
    }

    private void ApplyActive(bool on)
    {
        if (fpsCamera != null) fpsCamera.SetActive(on);
        if (mainCamera != null) mainCamera.SetActive(!on);
        if (lookController != null) lookController.enabled = on;

        foreach (GameObject go in hideWhileActive) if (go != null) go.SetActive(!on);
        foreach (GameObject go in showWhileActive) if (go != null) go.SetActive(on);
        foreach (Behaviour b in disableWhileActive) if (b != null) b.enabled = !on;

        if (lockCursor)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }
    }
}
