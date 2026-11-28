using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// [Claude 2026-11-28] วิดีโอพื้นหลังเมนูหลัก (Menu.mov) — ซ่อนภาพไว้จนกว่าวิดีโอเริ่มเล่นจริง
/// กันจอขาวแว้บตอนเปิดเมนู (Render Texture ยังว่างก่อนเฟรมแรกมา) · ใต้ภาพมี Background สีเข้มรองไว้
/// แปะที่ตัวเดียวกับ RawImage + VideoPlayer
/// </summary>
[RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
public class MenuVideoBackground : MonoBehaviour
{
    private RawImage image;
    private VideoPlayer player;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        player = GetComponent<VideoPlayer>();
        image.enabled = false;
        player.started += OnStarted;
    }

    private void OnDestroy()
    {
        if (player != null) player.started -= OnStarted;
    }

    private void Start()
    {
        if (!player.isPlaying) player.Play();
    }

    private void OnStarted(VideoPlayer vp) => image.enabled = true;
}
