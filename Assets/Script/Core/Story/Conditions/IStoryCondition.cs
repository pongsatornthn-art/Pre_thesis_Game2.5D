/// <summary>
/// อินเตอร์เฟสสำหรับเงื่อนไขการดำเนินเนื้อเรื่อง (Story Condition)
/// มีเพียง 1 เมธอดตามหลัก Interface Segregation Principle (ISP)
/// ช่วยให้สร้างเงื่อนไขที่ซับซ้อน (Composite / Decorator) ได้โดยไม่ต้องผูกติดกับตรรกะเฉพาะ
/// </summary>
public interface IStoryCondition
{
    bool IsMet(StoryContext ctx);
}
