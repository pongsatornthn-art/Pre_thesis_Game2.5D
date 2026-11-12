using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// รูปตัวอย่างของช่องเซฟ (แบบเกม AAA — การ์ดช่องเซฟมีรูปฉากตอนเซฟ)
///
/// ขั้นตอน: Capture() ถ่ายหน้าจอเก็บไว้ก่อน (ต้องถ่าย "ก่อน" เปิดหน้าจอเลือกช่อง/ก่อนเข้ามุมโต๊ะ ไม่งั้นได้รูป UI)
///        → SaveManager เซฟสำเร็จ → WritePending(ช่อง) เขียนเป็นไฟล์ PNG ข้างไฟล์เซฟ
/// ช่องอัตโนมัติไม่มีรูป · ลบช่องเซฟ = ลบรูปด้วย
/// [Claude 2026-11-11 งานข้อ 8]
/// </summary>
public static class SaveThumbnail
{
    public const int Width = 384;
    public const int Height = 216;

    private static Texture2D pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pending = null;

    public static string PathOf(int slot)
    {
        string name = slot == SaveManager.AutoSlot ? "autosave" : $"save_{slot}";
        return Path.Combine(Application.persistentDataPath, name + "_thumb.png");
    }

    /// <summary>ถ่ายหน้าจอตอนจบเฟรมนี้ เก็บไว้รอเซฟ (เรียกด้วย StartCoroutine แล้ว yield รอได้)</summary>
    public static IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();

        Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
        if (full == null) yield break;

        RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 0);
        Graphics.Blit(full, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        if (pending == null) pending = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        pending.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        pending.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(full);
    }

    /// <summary>เขียนรูปที่ถ่ายไว้ลงช่องนี้ (SaveManager เรียกหลังเซฟสำเร็จ)</summary>
    public static void WritePending(int slot)
    {
        if (pending == null || slot == SaveManager.AutoSlot) return;
        try
        {
            File.WriteAllBytes(PathOf(slot), pending.EncodeToPNG());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveThumbnail] เขียนรูปช่อง {slot} ไม่สำเร็จ (เซฟยังอยู่ครบ)\n{e.Message}");
        }
    }

    /// <summary>โหลดรูปของช่อง — ไม่มีรูป = null (ผู้เรียกต้อง Destroy เองเมื่อเลิกใช้)</summary>
    public static Texture2D Load(int slot)
    {
        string path = PathOf(slot);
        if (!File.Exists(path)) return null;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
        Object.Destroy(tex);
        return null;
    }

    public static void Delete(int slot)
    {
        string path = PathOf(slot);
        if (File.Exists(path)) File.Delete(path);
    }
}
