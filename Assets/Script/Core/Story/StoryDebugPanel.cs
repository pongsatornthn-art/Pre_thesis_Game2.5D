using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// แผงดีบักระบบเนื้อเรื่องและเควส (กดปุ่ม F1 ตอนเล่น)
/// แสดงสถานะธงทั้งหมด เควสปัจจุบัน และเป้าหมายที่เหลืออยู่
/// พร้อมปุ่มกดตั้ง/ล้างธงเองเพื่อข้ามไปเทสฉากกลางเกมได้ทันทีโดยไม่ต้องเริ่มเล่นใหม่ตั้งแต่ต้น
/// 
/// ใช้ OnGUI แบบ Standalone ทำให้สามารถใช้งานได้ทันทีโดยไม่ต้องต่อ Canvas UI ในฉาก
/// </summary>
public class StoryDebugPanel : MonoBehaviour
{
    [Header("การตั้งค่า")]
    [Tooltip("ปุ่มสำหรับเปิด/ปิดหน้าต่างดีบัก")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    private bool isVisible = false;
    private Rect windowRect = new Rect(20, 20, 360, 480);
    private Vector2 flagsScrollPos;
    private string inputFlagId = "";

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
        }
    }

    private void OnGUI()
    {
        if (!isVisible) return;
        windowRect = GUI.Window(9982, windowRect, DrawDebugWindow, "🛠️ Story & Quest Debug (F1)");
    }

    private void DrawDebugWindow(int windowID)
    {
        IStoryFlags flagsService = ServiceLocator.Get<IStoryFlags>();
        IQuestService questService = ServiceLocator.Get<IQuestService>();

        GUILayout.Label("<b>📜 เควสปัจจุบัน (Active Quest):</b>");
        if (questService != null && questService.ActiveQuest != null)
        {
            QuestData q = questService.ActiveQuest;
            GUILayout.Label($"  ID: <color=yellow>{q.questId}</color>");

            if (q.objectives != null)
            {
                for (int i = 0; i < q.objectives.Length; i++)
                {
                    var obj = q.objectives[i];
                    bool done = questService.IsObjectiveDone(obj);
                    string status = done ? "<color=green>[✓ เสร็จ]</color>" : "<color=red>[ ] ยังไม่เสร็จ</color>";
                    string flagText = obj.completedWhenFlagSet != null ? obj.completedWhenFlagSet.flagId : "none";

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"  {status} ธง: {flagText}");
                    if (!done && obj.completedWhenFlagSet != null)
                    {
                        if (GUILayout.Button("ทำให้ผ่าน", GUILayout.Width(75)))
                        {
                            flagsService?.Set(obj.completedWhenFlagSet);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }
        else
        {
            GUILayout.Label("  <i>(ไม่มีเควสที่กำลังทำอยู่)</i>");
        }

        GUILayout.Space(10);
        GUILayout.Label("<b>🚩 จัดการธงเนื้อเรื่อง (Story Flags):</b>");

        GUILayout.BeginHorizontal();
        inputFlagId = GUILayout.TextField(inputFlagId);
        if (GUILayout.Button("ตั้งธง", GUILayout.Width(60)) && !string.IsNullOrEmpty(inputFlagId))
        {
            StoryFlagId tempFlag = ScriptableObject.CreateInstance<StoryFlagId>();
            tempFlag.flagId = inputFlagId;
            flagsService?.Set(tempFlag);
        }
        if (GUILayout.Button("ล้างธง", GUILayout.Width(60)) && !string.IsNullOrEmpty(inputFlagId))
        {
            StoryFlagId tempFlag = ScriptableObject.CreateInstance<StoryFlagId>();
            tempFlag.flagId = inputFlagId;
            flagsService?.Clear(tempFlag);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("<b>รายการเควสที่จบแล้ว:</b> " + (questService != null ? questService.CompletedQuests.Count.ToString() : "0"));

        GUI.DragWindow(new Rect(0, 0, 10000, 25));
    }
}
