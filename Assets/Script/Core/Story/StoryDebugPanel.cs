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
    private Vector2 questScrollPos;
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

        IStoryCounters countersService = ServiceLocator.Get<IStoryCounters>();

        GUILayout.Label("<b>📜 สมุดเควส (เรียงตามที่ได้รับ):</b>");
        if (questService != null && questService.Journal.Count > 0)
        {
            questScrollPos = GUILayout.BeginScrollView(questScrollPos, GUILayout.Height(220));
            foreach (QuestData q in questService.Journal)
            {
                if (q == null) continue;
                QuestState state = questService.GetState(q);
                string stateText = state == QuestState.Completed ? "<color=green>[จบ]</color>" : "<color=yellow>[ทำอยู่]</color>";
                GUILayout.Label($"{stateText} {q.questId}");

                if (q.objectives == null) continue;
                foreach (QuestObjective obj in q.objectives)
                {
                    if (obj == null) continue;
                    DrawObjectiveRow(questService, flagsService, countersService, q, obj);
                }
            }
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("  <i>(ยังไม่มีเควสในสมุด)</i>");
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
        GUI.DragWindow(new Rect(0, 0, 10000, 25));
    }

    private static void DrawObjectiveRow(IQuestService quests, IStoryFlags flags, IStoryCounters counters, QuestData q, QuestObjective obj)
    {
        bool done = quests.IsObjectiveDone(q, obj);
        bool visible = quests.IsObjectiveVisible(q, obj);
        string status = done ? "<color=green>✓</color>" : (visible ? "<color=red>□</color>" : "<color=grey>(ซ่อน)</color>");

        string detail = obj.descriptionKey;
        if (quests.TryGetObjectiveProgress(q, obj, out int cur, out int target)) detail += $" ({cur}/{target})";

        GUILayout.BeginHorizontal();
        GUILayout.Label($"    {status} {detail}");

        // ปุ่มโกง — ใช้ได้กับชนิดที่รู้วิธีทำให้สำเร็จเท่านั้น
        if (!done && obj is FlagObjective flagObj && flagObj.flag != null && GUILayout.Button("ผ่าน", GUILayout.Width(45)))
        {
            flags?.Set(flagObj.flag);
        }
        if (!done && obj is CounterObjective counterObj && counterObj.counter != null && GUILayout.Button("+1", GUILayout.Width(45)))
        {
            counters?.Add(counterObj.counter, 1);
        }
        GUILayout.EndHorizontal();
    }
}
