namespace Combat.Gun
{
    /// <summary>
    /// ค่าตัวเลขของกรวยกระสุนที่ถอดออกมาจาก RangedWeaponData
    ///
    /// ทำไมต้องมี struct นี้:
    /// WeaponSpread เป็นสูตรคณิตศาสตร์ล้วน ไม่ควรรู้จัก ScriptableObject ของ Unity
    /// พอตัดการพึ่งพานั้นออก โค้ดสูตรจึงย้ายมาอยู่ในแอสเซมบลีเล็ก ๆ ของตัวเองได้
    /// ทำให้ไฟล์เทส EditMode มองเห็นและทดสอบได้จริง (ของเดิมเทสคอมไพล์ไม่ติดเลยสักเคส)
    /// </summary>
    public struct SpreadSettings
    {
        /// <summary>กรวยกว้างสุดตอนเคลื่อนที่ (องศา)</summary>
        public float maxSpreadAngle;

        /// <summary>กรวยแคบสุดตอนยืนนิ่งและเล็งโดนศัตรูครบเวลา (องศา)</summary>
        public float minSpreadAngle;

        /// <summary>เวลาที่ใช้หุบกรวยจากกว้างสุดจนแคบสุด (วินาที)</summary>
        public float focusTime;

        /// <summary>กรวยบานเพิ่มต่อการยิง 1 นัด (องศา)</summary>
        public float recoilSpread;

        /// <summary>ตัวคูณกรวยเป้าหมายตอนกดคลิกขวาค้าง</summary>
        public float steadySpreadMultiplier;

        /// <summary>ตัวคูณความเร็วหุบกรวยตอนกดคลิกขวาค้าง</summary>
        public float steadyFocusSpeedMultiplier;

        /// <summary>ค่ามาตรฐานสำหรับใช้ในเทสหรือกรณีไม่มีข้อมูลปืน</summary>
        public static SpreadSettings Default => new SpreadSettings
        {
            maxSpreadAngle = 15f,
            minSpreadAngle = 0f,
            focusTime = 1.5f,
            recoilSpread = 5f,
            steadySpreadMultiplier = 0.35f,
            steadyFocusSpeedMultiplier = 2f
        };
    }

    /// <summary>
    /// บริบทสถานะของผู้เล่นที่ส่งผลต่อความแม่นยำ/ขนาดกรวยกระสุน
    /// </summary>
    public struct SpreadContext
    {
        public bool isMoving;
        public bool isDashing;
        public bool isSteady;        // คลิกขวาค้าง (เล็งนิ่ง)
        public bool isAimingAtEnemy; // เมาส์ชี้อยู่บนตัวศัตรู
    }
}
