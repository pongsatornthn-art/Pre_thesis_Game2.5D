using UnityEngine;

public class ClockPuzzle : PTSDPuzzleBase
{
    public int targetHour = 3; // เวลาที่ถูกต้อง
    public int currentHour = 12;

    public override void StartPuzzle()
    {
        Debug.Log("เปิดหน้าต่าง UI ปริศนานาฬิกา");
        // โค้ดเปิด UI นาฬิกา
    }

    // สมมติว่าผูกฟังก์ชันนี้กับปุ่ม UI หมุนเข็มนาฬิกา
    public void RotateClock()
    {
        currentHour++;
        if (currentHour > 12) currentHour = 1;

        CheckWinCondition(); // หมุนเสร็จก็เช็คเงื่อนไขเลย
    }

    public override void CheckWinCondition()
    {
        if (currentHour == targetHour)
        {
            // ถ้าเวลาตรงกัน เรียกฟังก์ชันชนะจากคลาสแม่ได้เลย!
            CompletePuzzle();
        }
    }
}