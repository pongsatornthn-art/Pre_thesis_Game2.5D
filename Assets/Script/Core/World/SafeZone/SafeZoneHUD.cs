using TMPro;
using UnityEngine;

/// <summary>
/// หน้าต่าง HUD สำหรับระบบ Safe Zone (แสดงผลอย่างเดียว ไม่ตัดสินใจตรรกะเกม)
/// แสดงไอคอนล็อคแดง + ตัวเลขนับถอยหลังคูลดาวน์ และลูกศรบอกทิศทางไปยัง Safe Zone ใหม่
/// </summary>
public class SafeZoneHUD : MonoBehaviour
{
    [Header("อ้างอิงระบบ")]
    [Tooltip("ตัวควบคุมระบบ Safe Zone ในฉาก")]
    [SerializeField] private SafeZoneDirector director;

    [Header("แผงนับถอยหลัง (ห้องติดคูลดาวน์)")]
    [Tooltip("แผงไอคอนล็อกแดงและตัวเลข")]
    [SerializeField] private UIPanelController lockPanel;

    [Tooltip("ข้อความแสดงตัวเลขนับถอยหลัง")]
    [SerializeField] private TMP_Text countdownText;

    [Header("ลูกศรบอกทิศทาง Safe Zone ใหม่")]
    [Tooltip("แผงแสดงลูกศรบอกทิศทาง")]
    [SerializeField] private UIPanelController arrowPanel;

    [Tooltip("RectTransform ของลูกศร")]
    [SerializeField] private RectTransform arrow;

    [Tooltip("ระยะห่างจากขอบจอเมื่อลูกศรแสดงที่ขอบ (พิกเซล)")]
    [SerializeField] private float arrowEdgePadding = 60f;

    private bool showArrow = false;

    private void Awake()
    {
        if (director == null)
            director = Object.FindFirstObjectByType<SafeZoneDirector>();

        if (lockPanel != null) lockPanel.HideImmediate();
        if (arrowPanel != null) arrowPanel.HideImmediate();
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<SafeZoneRelocatedEvent>(HandleSafeZoneRelocated);
        GameEventBus.Subscribe<SafeZonePlayerChangedEvent>(HandlePlayerChangedZone);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<SafeZoneRelocatedEvent>(HandleSafeZoneRelocated);
        GameEventBus.Unsubscribe<SafeZonePlayerChangedEvent>(HandlePlayerChangedZone);

        showArrow = false;
        if (lockPanel != null && lockPanel.IsVisible) lockPanel.HideImmediate();
        if (arrowPanel != null && arrowPanel.IsVisible) arrowPanel.HideImmediate();
    }

    private void HandleSafeZoneRelocated(SafeZoneRelocatedEvent e)
    {
        showArrow = true;
        if (arrowPanel != null && !arrowPanel.IsVisible)
            arrowPanel.Show();
    }

    private void HandlePlayerChangedZone(SafeZonePlayerChangedEvent e)
    {
        // เมื่อผู้เล่นเดินเข้าสู่ห้อง Safe Zone ใหม่ ให้ซ่อนลูกศร
        if (e.Inside && director != null && e.Zone == director.CurrentSafeZone && e.Zone.State == SafeZoneState.Safe)
        {
            showArrow = false;
            if (arrowPanel != null && arrowPanel.IsVisible)
                arrowPanel.Hide();
        }
    }

    private void Update()
    {
        if (director == null || !director.IsRunning)
        {
            if (lockPanel != null && lockPanel.IsVisible) lockPanel.Hide();
            if (arrowPanel != null && arrowPanel.IsVisible) arrowPanel.Hide();
            showArrow = false;
            return;
        }

        UpdateLockDisplay();
        UpdateArrowDisplay();
    }

    private void UpdateLockDisplay()
    {
        if (lockPanel == null) return;

        SafeZone zone = director.ZonePlayerIsIn;
        bool shouldShowLock = zone != null && zone.CooldownRemaining > 0f;

        if (shouldShowLock)
        {
            if (!lockPanel.IsVisible)
                lockPanel.Show();

            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(zone.CooldownRemaining).ToString();
        }
        else
        {
            if (lockPanel.IsVisible)
                lockPanel.Hide();
        }
    }

    private void UpdateArrowDisplay()
    {
        if (!showArrow || director.CurrentSafeZone == null || arrow == null)
        {
            if (arrowPanel != null && arrowPanel.IsVisible)
                arrowPanel.Hide();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPoint = cam.WorldToScreenPoint(director.CurrentSafeZone.AnchorPosition);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool isBehindCamera = screenPoint.z < 0f;

        if (isBehindCamera)
        {
            screenPoint.x = -screenPoint.x;
            screenPoint.y = -screenPoint.y;
        }

        float minX = arrowEdgePadding;
        float maxX = Screen.width - arrowEdgePadding;
        float minY = arrowEdgePadding;
        float maxY = Screen.height - arrowEdgePadding;

        bool insideScreen = !isBehindCamera &&
                            screenPoint.x >= minX && screenPoint.x <= maxX &&
                            screenPoint.y >= minY && screenPoint.y <= maxY;

        if (insideScreen)
        {
            // เป้าหมายอยู่ในจอ วางลูกศรเหนือเป้าหมายชี้ลง
            arrow.position = new Vector3(screenPoint.x, screenPoint.y + 40f, 0f);
            arrow.rotation = Quaternion.Euler(0f, 0f, 180f);
        }
        else
        {
            // เป้าหมายอยู่นอกจอ ล็อคลูกศรติดขอบจอตามทิศ
            Vector2 dir = new Vector2(screenPoint.x, screenPoint.y) - screenCenter;
            float angle = Mathf.Atan2(dir.y, dir.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float targetX = 0f;
            float targetY = 0f;

            if (Mathf.Abs(cos) > 0.0001f)
            {
                float borderX = cos > 0 ? maxX : minX;
                float t = (borderX - screenCenter.x) / cos;
                float testY = screenCenter.y + sin * t;
                if (testY >= minY && testY <= maxY)
                {
                    targetX = borderX;
                    targetY = testY;
                }
            }

            if (targetX == 0f && Mathf.Abs(sin) > 0.0001f)
            {
                float borderY = sin > 0 ? maxY : minY;
                float t = (borderY - screenCenter.y) / sin;
                targetX = screenCenter.x + cos * t;
                targetY = borderY;
            }

            arrow.position = new Vector3(targetX, targetY, 0f);
            arrow.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
        }
    }
}
