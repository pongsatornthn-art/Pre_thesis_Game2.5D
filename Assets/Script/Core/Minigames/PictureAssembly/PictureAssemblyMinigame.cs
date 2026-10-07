using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// มินิเกมประกอบรูปวาด — เล่นในโลก 3D ผ่านมุม close-up + กล้อง FPS ของปอ
///
/// วิธีเล่น: เล็งชิ้น (เรืองแสง) → คลิกซ้าย = หยิบ (ลอยตามเป้า) → R = หมุน 90° → เล็งช่อง → คลิกซ้าย = วาง
///   ถูกช่อง + ถูกทิศ → ดูดเข้าล็อก · ผิด → เด้งกลับถาด · ครบทุกชิ้น → สำเร็จ
///
/// วางในซีน (ทั้งหมดแก้ใน Editor ได้):
///   โต๊ะ ─ Board (ระนาบกระดาน: แกน Y สีเขียว = ทิศ "ขึ้น" ของกระดาน)
///        ├ Slot_1..N   AssemblySlot + MiniGameInteractable(ปอ) + Collider
///        └ Piece_1..N  AssemblyPiece + MiniGameInteractable(ปอ) + Collider (วางตรงถาด หันทิศเดียวกับช่องที่ถูก)
///   ลากชิ้นทั้งหมดใส่ช่อง Pieces · ลากกล้อง FPS ใส่ View Camera (เว้นว่าง = ใช้กล้องของมุม close-up ที่เปิดอยู่)
/// ⚠️ ระหว่างเล่น PlayerCombat ต้องถูกปิด (ใส่ในช่อง Disable While Active ของ StoryCloseUpView) — ไม่งั้น R = รีโหลดปืนด้วย
/// </summary>
public class PictureAssemblyMinigame : MinigameBase
{
    [Header("กระดาน")]
    [Tooltip("ระนาบกระดาน — แกน Y (เขียว) ต้องชี้ขึ้นจากผิวโต๊ะ")]
    [SerializeField] private Transform board;

    [Tooltip("ชิ้นที่ถือลอยเหนือกระดานกี่หน่วย")]
    [SerializeField, Min(0f)] private float holdHeight = 0.05f;

    [Tooltip("ชิ้นทั้งหมด")]
    [SerializeField] private List<AssemblyPiece> pieces = new List<AssemblyPiece>();

    [Tooltip("กล้องที่ใช้หาจุดเล็ง (เว้นว่าง = กล้องของมุม close-up ที่เปิดอยู่)")]
    [SerializeField] private Camera viewCamera;

    [Header("กติกา")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [Tooltip("สุ่มทิศเริ่มต้นของชิ้น (ต้องหมุนเองก่อนวาง)")]
    [SerializeField] private bool randomizeStartRotation = true;

    [Header("เสียง (ผ่าน IAudioService)")]
    [SerializeField] private AudioClip pickSound;
    [SerializeField] private AudioClip rotateSound;
    [SerializeField] private AudioClip placeCorrectSound;
    [SerializeField] private AudioClip placeWrongSound;

    private AssemblyPiece held;
    private readonly List<AssemblyPiece> active = new List<AssemblyPiece>();

    private Vector3 BoardNormal => board != null ? board.up : Vector3.up;

    protected override void OnBegin()
    {
        held = null;
        active.Clear();

        // รอบใหม่: ล้างช่องที่เคยมีชิ้นวาง (เล่นซ้ำหลังออกกลางคัน)
        foreach (AssemblyPiece p in pieces)
        {
            if (p != null && p.CorrectSlot != null) p.CorrectSlot.Occupant = null;
        }

        foreach (AssemblyPiece p in pieces)
        {
            if (p == null) continue;

            // ชิ้นที่ยังไม่ได้เก็บมา ไม่โผล่บนโต๊ะ (ถามคลังที่ถูกต้องผ่าน ItemOwnership)
            bool owned = p.Fragment == null || ItemOwnership.Has(p.Fragment);
            p.gameObject.SetActive(owned);
            if (!owned) continue;

            int start = randomizeStartRotation ? Random.Range(1, 4) : 0;   // 1–3 = ไม่ตรงทิศแน่นอน
            p.ResetForNewRound(start, BoardNormal);
            active.Add(p);
        }

        if (active.Count == 0)
        {
            Debug.LogWarning($"[PictureAssembly] '{SceneId}' ไม่มีชิ้นให้ประกอบเลย (ยังไม่ได้เก็บ หรือลืมลากใส่ Pieces) — นับว่าสำเร็จ", this);
            Finish(MinigameResult.Success);
        }
    }

    protected override void OnEnd(MinigameResult result)
    {
        if (held != null) held.ReturnToTray(BoardNormal);
        held = null;
    }

    protected override void Update()
    {
        base.Update();
        if (IsInputBlocked || held == null) return;

        FollowAim(held);

        if (Input.GetKeyDown(rotateKey))
        {
            held.RotateOnce(BoardNormal);
            PlaySfx(rotateSound);
        }
    }

    // ---------- เรียกจาก MiniGameInteractable ของปอ (ผ่าน AssemblyPiece / AssemblySlot) ----------

    internal void OnPieceClicked(AssemblyPiece piece)
    {
        if (IsInputBlocked || piece == null || piece.IsPlaced || !active.Contains(piece)) return;

        if (held != null && held != piece) held.ReturnToTray(BoardNormal);   // สลับชิ้นที่ถือ
        held = piece;
        held.SetClickable(false);
        PlaySfx(pickSound);
    }

    internal void OnSlotClicked(AssemblySlot slot)
    {
        if (IsInputBlocked || held == null || slot == null || slot.Occupant != null) return;

        bool correct = held.CorrectSlot == slot && held.RotationSteps == 0;
        if (correct)
        {
            held.SnapInto(slot);
            slot.Occupant = held;
            held = null;
            PlaySfx(placeCorrectSound);

            if (AllPlaced()) Finish(MinigameResult.Success);
        }
        else
        {
            held.ReturnToTray(BoardNormal);
            held = null;
            PlaySfx(placeWrongSound);
        }
    }

    // ----------------------------------------------------------------------------------------------

    private void FollowAim(AssemblyPiece piece)
    {
        Camera cam = viewCamera != null ? viewCamera : StoryCloseUpView.Active != null ? StoryCloseUpView.Active.ViewCamera : null;
        if (cam == null || board == null) return;

        // เป้าเล็งของกล้อง FPS อยู่กลางจอ → ยิงจากกลางจอไปตัดระนาบกระดาน
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Plane plane = new Plane(BoardNormal, board.position);
        if (!plane.Raycast(ray, out float dist)) return;

        Quaternion aligned = piece.CorrectSlot != null ? piece.CorrectSlot.SnapPoint.rotation : board.rotation;
        piece.transform.SetPositionAndRotation(
            ray.GetPoint(dist) + BoardNormal * holdHeight,
            Quaternion.AngleAxis(piece.RotationSteps * 90f, BoardNormal) * aligned);
    }

    private bool AllPlaced()
    {
        foreach (AssemblyPiece p in active) if (!p.IsPlaced) return false;
        return true;
    }
}
