using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// มินิเกมประกอบรูปบนขาตั้งวาดรูป — กระดานอยู่ในโลก 3D · เศษรูปที่เก็บมาอยู่ในช่อง UI ด้านขวา
///
/// วิธีเล่น (เมาส์โผล่ · กล้องนิ่ง ระหว่างเล่น):
///   ลากเศษจากช่องขวา → ไปปล่อยบนกระดาน · R = หมุน 90° ระหว่างลาก · ลากชิ้นบนกระดานย้ายที่ได้ · คลิกขวาที่ชิ้น = เอากลับเข้าช่อง
///   วางอิสระ (Loose): ชิ้นค้างตรงที่ปล่อย · ครบทุกชิ้นแล้วระบบเช็คว่า "ใกล้ที่ถูก + ทิศถูก" ทุกชิ้น → ผ่าน (ไม่ต้องเนียนเป๊ะ)
///   แบบดูด (Magnetic): ปล่อยใกล้ที่ถูกในระยะ Magnetic Radius → ดูดเข้าที่
///
/// วางในซีน (แก้ใน Editor ได้ทั้งหมด):
///   Board (ลูกของขาตั้ง) — แกน X/Y = ผิวกระดาน · แกน Z (น้ำเงิน) ชี้เข้าไปในกระดาน
///     └ Piece_1..N   AssemblyPiece + SpriteRenderer + BoxCollider · วางตรงตำแหน่งที่ถูก · ชิ้นติดอยู่แล้วติ๊ก Pre Placed
///   UI_Manager/PuzzleTray (CanvasGroup + UIPanelController) └ ช่อง AssemblyTrayItem ชิ้นละช่อง
/// </summary>
public class PictureAssemblyMinigame : MinigameBase
{
    public enum SnapMode { Loose, Magnetic }

    [Header("กระดาน")]
    [Tooltip("ระนาบกระดาน: แกน X/Y = ผิวกระดาน · ชิ้นทั้งหมดเป็นลูกของตัวนี้")]
    [SerializeField] private Transform board;

    [Tooltip("ครึ่งความกว้าง/สูงของพื้นที่วางได้ (หน่วยของกระดาน) — ปล่อยนอกนี้ = ชิ้นกลับเข้าช่อง")]
    [SerializeField] private Vector2 boardHalfSize = new Vector2(0.5f, 0.5f);

    [Tooltip("ชิ้นยกลอยจากผิวกระดานเล็กน้อยระหว่างลาก (กันจมหาย)")]
    [SerializeField] private float dragLift = 0.01f;

    [SerializeField] private List<AssemblyPiece> pieces = new List<AssemblyPiece>();

    [Header("ช่อง UI ด้านขวา")]
    [SerializeField] private UIPanelController trayPanel;
    [SerializeField] private List<AssemblyTrayItem> trayItems = new List<AssemblyTrayItem>();

    [Header("กล้อง")]
    [Tooltip("กล้องที่ใช้ยิงเรย์จากเมาส์ (เว้นว่าง = กล้องของมุม close-up ที่เปิดอยู่)")]
    [SerializeField] private Camera viewCamera;

    [Header("กติกา")]
    [SerializeField] private SnapMode snapMode = SnapMode.Loose;
    [Tooltip("ห่างจากที่ถูกได้ไม่เกินเท่านี้ ถึงนับว่าถูก (หน่วยของกระดาน)")]
    [SerializeField, Min(0.001f)] private float positionTolerance = 0.08f;
    [Tooltip("แบบดูด: ปล่อยใกล้ที่ถูกภายในระยะนี้ → ดูดเข้าที่")]
    [SerializeField, Min(0.001f)] private float magneticRadius = 0.08f;
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [Tooltip("สุ่มทิศเริ่มต้นของชิ้น (ต้องหมุนเองก่อนวาง)")]
    [SerializeField] private bool randomizeStartRotation = true;

    [Header("เสียง (ผ่าน IAudioService)")]
    [SerializeField] private AudioClip pickSound;
    [SerializeField] private AudioClip rotateSound;
    [SerializeField] private AudioClip placeSound;
    [Tooltip("วางครบแล้วแต่ยังไม่ถูก")]
    [SerializeField] private AudioClip notYetSound;

    private AssemblyPiece dragging;
    private bool draggingFromTray;
    private CursorLockMode savedLock;
    private bool savedVisible;

    private Camera Cam => viewCamera != null ? viewCamera : StoryCloseUpView.Active != null ? StoryCloseUpView.Active.ViewCamera : Camera.main;

    // ---------------- เริ่ม / จบ ----------------

    protected override void OnBegin()
    {
        dragging = null;

        foreach (AssemblyPiece p in pieces)
        {
            if (p == null) continue;
            p.ResetForRound(randomizeStartRotation ? Random.Range(1, 4) : 0);
        }
        RefreshTray();
        if (trayPanel != null) trayPanel.Show();

        // เมาส์โผล่ + กล้องนิ่ง (ต้องลาก UI ด้วยเมาส์) — คืนค่าเดิมตอนจบ (ไม่แย่ง Cursor กับระบบอื่น)
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // กล้องนิ่ง + กลับไปมุมที่จัดไว้ในซีน (มุมเดียวกับตอนกด F ครั้งแรก)
        if (StoryCloseUpView.Active != null) StoryCloseUpView.Active.SetLookEnabled(false);

        if (RequiredPieceCount() == 0)
        {
            Debug.LogWarning($"[PictureAssembly] '{SceneId}' ไม่มีชิ้นให้ประกอบ (ลืมลากใส่ Pieces?) — นับว่าสำเร็จ", this);
            Finish(MinigameResult.Success);
        }
    }

    protected override void OnEnd(MinigameResult result)
    {
        if (dragging != null) ReturnToTray(dragging);
        dragging = null;
        if (trayPanel != null) trayPanel.Hide();

        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
        if (StoryCloseUpView.Active != null) StoryCloseUpView.Active.SetLookEnabled(true);
    }

    // ---------------- ลาก ----------------

    protected override void Update()
    {
        base.Update();
        if (IsInputBlocked) return;

        if (dragging != null)
        {
            FollowMouse(dragging);

            if (Input.GetKeyDown(rotateKey))
            {
                dragging.RotateOnce();
                PlaySfx(rotateSound);
            }

            // ปล่อยเมาส์ = วาง (ทั้งลากจากกระดานและจากช่อง UI — ไม่พึ่ง OnEndDrag ของ UI อย่างเดียว)
            // คลิกขวาระหว่างลาก = ยกเลิก เอากลับเข้าช่อง
            if (Input.GetMouseButtonUp(0)) EndDrag();
            else if (Input.GetMouseButtonDown(1))
            {
                AssemblyPiece cancelled = dragging;
                dragging = null;
                ReturnToTray(cancelled);
                RefreshTray();
            }
            return;
        }

        // ไม่ได้ถืออะไร: กดซ้ายที่ชิ้นบนกระดาน = หยิบย้าย · คลิกขวา = เอากลับเข้าช่อง
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUI) return;

        if (Input.GetMouseButtonDown(0) && TryPickPieceUnderMouse(out AssemblyPiece hit))
        {
            dragging = hit;
            draggingFromTray = false;
            dragging.SetDragging(true);
            PlaySfx(pickSound);
        }
        else if (Input.GetMouseButtonDown(1) && TryPickPieceUnderMouse(out AssemblyPiece back))
        {
            ReturnToTray(back);
            RefreshTray();
        }
    }

    /// <summary>เริ่มลากจากช่อง UI ด้านขวา (AssemblyTrayItem เรียก)</summary>
    internal void BeginDragFromTray(AssemblyPiece piece)
    {
        if (IsInputBlocked || piece == null || piece.PrePlaced || piece.OnBoard || dragging != null) return;

        dragging = piece;
        draggingFromTray = true;
        dragging.SetDragging(true);
        RefreshTray();   // ซ่อนช่องของชิ้นที่กำลังลาก
        PlaySfx(pickSound);
    }

    /// <summary>ปล่อยชิ้นที่ลากอยู่ (ปล่อยเมาส์ / OnEndDrag ของ UI)</summary>
    internal void EndDrag()
    {
        if (dragging == null) return;

        AssemblyPiece piece = dragging;
        dragging = null;

        if (TryGetBoardPoint(out Vector3 local, clampToBoard: false) && InsideBoard(local))
        {
            if (snapMode == SnapMode.Magnetic && piece.RotationSteps == 0 &&
                Vector2.Distance(local, piece.TargetLocalPosition) <= magneticRadius)
            {
                piece.PlaceAtTarget();
            }
            else
            {
                piece.PlaceAt(new Vector3(local.x, local.y, piece.TargetLocalPosition.z));
            }
            PlaySfx(placeSound);
            CheckCompletion();
        }
        else
        {
            ReturnToTray(piece);   // ปล่อยนอกกระดาน = กลับเข้าช่อง
        }

        RefreshTray();
    }

    // ---------------- ผลแพ้ชนะ ----------------

    private void CheckCompletion()
    {
        int required = 0, onBoard = 0, correct = 0;
        foreach (AssemblyPiece p in pieces)
        {
            if (p == null || p.PrePlaced) continue;
            required++;
            if (!p.OnBoard) continue;
            onBoard++;
            if (p.RotationSteps == 0 && p.DistanceFromTarget() <= positionTolerance) correct++;
        }

        if (required == 0 || onBoard < required) return;   // ยังวางไม่ครบ

        if (correct == required)
        {
            // จัดให้เข้าที่เป๊ะก่อนจบ ภาพสุดท้ายจะได้เป็นรูปสมบูรณ์
            foreach (AssemblyPiece p in pieces) if (p != null) p.PlaceAtTarget();
            Finish(MinigameResult.Success);
        }
        else
        {
            PlaySfx(notYetSound);   // วางครบแล้วแต่ยังไม่ถูก — ผู้เล่นขยับต่อได้
        }
    }

    // ---------------- ตัวช่วย ----------------

    private void RefreshTray()
    {
        foreach (AssemblyTrayItem item in trayItems)
        {
            if (item == null || item.Piece == null) continue;
            AssemblyPiece p = item.Piece;
            bool owned = p.Fragment == null || ItemOwnership.Has(p.Fragment);
            item.SetAvailable(owned && !p.PrePlaced && !p.OnBoard && p != dragging, inUse: p == dragging && draggingFromTray);
        }
    }

    private void ReturnToTray(AssemblyPiece piece)
    {
        if (piece == null || piece.PrePlaced) return;
        piece.SetOnBoard(false);
    }

    private int RequiredPieceCount()
    {
        int n = 0;
        foreach (AssemblyPiece p in pieces) if (p != null && !p.PrePlaced) n++;
        return n;
    }

    private void FollowMouse(AssemblyPiece piece)
    {
        if (!TryGetBoardPoint(out Vector3 local, clampToBoard: true)) return;
        piece.transform.localPosition = new Vector3(local.x, local.y, piece.TargetLocalPosition.z - dragLift);
    }

    /// <summary>จุดที่เมาส์ชี้บนระนาบกระดาน (พิกัด local ของกระดาน)</summary>
    private bool TryGetBoardPoint(out Vector3 local, bool clampToBoard)
    {
        local = Vector3.zero;
        Camera cam = Cam;
        if (cam == null || board == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(board.forward, board.position);
        if (!plane.Raycast(ray, out float dist)) return false;

        local = board.InverseTransformPoint(ray.GetPoint(dist));
        if (clampToBoard)
        {
            local.x = Mathf.Clamp(local.x, -boardHalfSize.x, boardHalfSize.x);
            local.y = Mathf.Clamp(local.y, -boardHalfSize.y, boardHalfSize.y);
        }
        return true;
    }

    private bool InsideBoard(Vector3 local) => Mathf.Abs(local.x) <= boardHalfSize.x && Mathf.Abs(local.y) <= boardHalfSize.y;

    private bool TryPickPieceUnderMouse(out AssemblyPiece piece)
    {
        piece = null;
        Camera cam = Cam;
        if (cam == null) return false;

        // ยิงทะลุทุกอย่าง แล้วเลือกชิ้นที่ใกล้สุด — กันโซนกด F (trigger) / ตัวขาตั้ง บังเรย์ (เคยทำให้คลิกขวา "กลับบ้างไม่กลับบ้าง")
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 10f, ~0, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            AssemblyPiece p = hit.collider.GetComponentInParent<AssemblyPiece>();
            if (p == null || p.PrePlaced || !p.OnBoard || !pieces.Contains(p)) continue;
            if (hit.distance < best) { best = hit.distance; piece = p; }
        }
        return piece != null;
    }

#if UNITY_EDITOR
    // วาดกรอบพื้นที่วางได้ใน Scene view (ช่วยจัดกระดาน)
    private void OnDrawGizmosSelected()
    {
        if (board == null) return;
        Gizmos.matrix = board.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boardHalfSize.x * 2f, boardHalfSize.y * 2f, 0.001f));
    }
#endif
}
