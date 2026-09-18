using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// แม่แบบ (Base Class) สำหรับปริศนาทุกชนิดในโหมด PTSD Type B
/// </summary>
public abstract class PTSDPuzzleBase : MonoBehaviour
{
    [Header("Puzzle Status")]
    public bool isSolved = false;
    public string puzzleName = "Unamed Puzzle";

    [Header("Reward & Transition")]
    [Tooltip("ใส่ Event เมื่อแก้ปริศนาเสร็จ เช่น ปลดล็อคประตู หรือเล่นเสียงถอนหายใจ")]
    public UnityEvent OnPuzzleSolved;

    // ฟังก์ชันเริ่มปริศนา (บังคับให้ลูกทุกตัวต้องมี)
    public abstract void StartPuzzle();

    // ฟังก์ชันเช็คเงื่อนไขผ่าน (บังคับให้ลูกทุกตัวต้องมี)
    public abstract void CheckWinCondition();

    // ฟังก์ชันเมื่อแก้ปริศนาสำเร็จ (เรียกใช้ร่วมกันได้เลย)
    protected void CompletePuzzle()
    {
        if (isSolved) return;

        isSolved = true;
        Debug.Log($"[Puzzle] แก้ปริศนา {puzzleName} สำเร็จ!");

        OnPuzzleSolved?.Invoke(); // สั่งรัน Event รางวัลต่างๆ

        // สั่งให้ PTSDManager จบสภาวะ Type B และกลับโลกปกติ
        PTSDManager.Instance.ExitPTSD();
    }
}