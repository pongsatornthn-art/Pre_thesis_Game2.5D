using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ป้ายกดปุ่มลอยเหนือของ (World Space) — มีอันเดียวในซีน ย้ายไปอยู่เหนือของที่ใกล้ผู้เล่นที่สุด
/// เจ้าของตกลง 2026-11-14: เดินใกล้ = ขึ้นป้ายที่ตัวของ · บอกชัดว่า E หรือ F · กรอบขาว = E · กรอบทอง = F
///
/// วางในซีน: Canvas แบบ World Space (ลูกของ object นี้) ลากใส่ช่อง Visual Root + ข้อความปุ่ม + ข้อความการกระทำ (แปะ LocalizedText)
/// ใครอยากขึ้นป้าย → ServiceLocator.GetOptional&lt;IInteractPromptService&gt;().Show(...) / Hide(this)
///   ทางลัด: แปะ InteractPromptMarker ที่ของที่มี trigger อยู่แล้ว (ประตู/กล่อง/ตู้เซฟ/โต๊ะ)
/// ซ่อนเองตอน: หยุดเกม · เปิดสมุด · มุม close-up · ฉากเนื้อเรื่องเล่น · มินิเกม/นั่งโต๊ะ
/// </summary>
public class WorldInteractPrompt : MonoBehaviour, IInteractPromptService
{
    [Header("ชิ้นส่วนป้าย")]
    [Tooltip("ก้อน Canvas ของป้าย — ระบบเปิด/ปิดเอง")]
    [SerializeField] private GameObject visualRoot;
    [Tooltip("ข้อความตัวปุ่ม เช่น E")]
    [SerializeField] private TMP_Text keyText;
    [Tooltip("ข้อความการกระทำ เช่น เก็บ (ต้องแปะ LocalizedText)")]
    [SerializeField] private LocalizedText label;
    [Tooltip("กรอบรอบตัวปุ่ม — เปลี่ยนสีตามชนิดปุ่ม")]
    [SerializeField] private Graphic keyFrame;
    [Tooltip("จางเข้า/ออก (เว้นว่าง = โผล่ทันที)")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("สี")]
    [SerializeField] private Color pickupColor = new Color(0.92f, 0.92f, 0.92f);
    [SerializeField] private Color storyColor = new Color(1f, 0.78f, 0.3f);

    [Header("ท่าทาง")]
    [SerializeField, Min(0f)] private float fadeSpeed = 10f;

    private struct Request
    {
        public Transform anchor;
        public float height;
        public PromptKey key;
        public string labelKey;
    }

    private readonly Dictionary<object, Request> requests = new Dictionary<object, Request>();
    private readonly List<object> deadOwners = new List<object>();
    private Transform player;
    private object shownOwner;
    private PromptKey shownKey;
    private string shownLabel;

    private void Awake()
    {
        ServiceLocator.Register<IInteractPromptService>(this);
        if (visualRoot != null) visualRoot.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ServiceLocator.GetOptional<IInteractPromptService>(), this))
            ServiceLocator.Unregister<IInteractPromptService>();
    }

    public void Show(object owner, Transform anchor, float height, PromptKey key, string labelKey)
    {
        if (owner == null || anchor == null) return;
        requests[owner] = new Request { anchor = anchor, height = height, key = key, labelKey = labelKey };
    }

    public void Hide(object owner)
    {
        if (owner != null) requests.Remove(owner);
    }

    private void LateUpdate()
    {
        object owner = null;
        Request req = default;
        bool hasTarget = !IsBlocked() && TryPickNearest(out owner, out req);
        if (!hasTarget)
        {
            SetVisible(false);
            return;
        }

        if (!ReferenceEquals(owner, shownOwner) || req.key != shownKey || req.labelKey != shownLabel)
        {
            shownOwner = owner;
            shownKey = req.key;
            shownLabel = req.labelKey;
            if (keyText != null) keyText.text = req.key == PromptKey.Story ? InteractInput.KeyName : InteractInput.PickupKeyName;
            if (keyFrame != null) keyFrame.color = req.key == PromptKey.Story ? storyColor : pickupColor;
            if (label != null) label.SetKey(req.labelKey);
        }

        Vector3 world = req.anchor.position + Vector3.up * req.height;
        Camera cam = Camera.main;
        if (IsScreenSpace())
        {
            // ป้ายวาดทับทุกอย่าง (กำแพง/ตัวละครบังไม่ได้) แต่ตามตำแหน่งของในโลกบนจอ
            if (cam == null) { SetVisible(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) { SetVisible(false); return; }
            visualRoot.transform.position = new Vector3(sp.x, sp.y, 0f);
        }
        else
        {
            transform.position = world;
            if (cam != null) transform.rotation = cam.transform.rotation;   // หันหน้าเข้ากล้องเสมอ
        }
        SetVisible(true);
    }

    private bool TryPickNearest(out object owner, out Request best)
    {
        owner = null;
        best = default;
        if (requests.Count == 0) return false;

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        Vector3 from = player != null ? player.position : transform.position;

        float bestDist = float.MaxValue;
        deadOwners.Clear();
        foreach (KeyValuePair<object, Request> kv in requests)
        {
            // ของถูกทำลาย (เช่น เก็บไอเทมแล้ว) → ล้างคำขอทิ้ง
            if (kv.Value.anchor == null || (kv.Key is Object o && o == null)) { deadOwners.Add(kv.Key); continue; }
            float d = (kv.Value.anchor.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; owner = kv.Key; best = kv.Value; }
        }
        foreach (object dead in deadOwners) requests.Remove(dead);
        return owner != null;
    }

    private Canvas rootCanvas;
    private bool IsScreenSpace()
    {
        if (rootCanvas == null && visualRoot != null) rootCanvas = visualRoot.GetComponentInParent<Canvas>(true);
        return rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
    }

    private static bool IsBlocked()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return true;
        if (GameplayInputGate.Blocked || JournalController.IsAnyOpen) return true;
        if (StoryCloseUpView.Active != null) return true;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return true;
        return false;
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot == null) return;
        if (canvasGroup == null)
        {
            if (visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
            return;
        }

        if (visible && !visualRoot.activeSelf) visualRoot.SetActive(true);
        float target = visible ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        if (!visible && canvasGroup.alpha <= 0f && visualRoot.activeSelf)
        {
            visualRoot.SetActive(false);
            shownOwner = null;
        }
    }
}
