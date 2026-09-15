# 🤝 HANDOFF — Gemini ↔ Claude Code (Pre_thesis_Game2.5D)

ไฟล์กลางสำหรับสื่อสารระหว่าง **Gemini (วางแผน)** กับ **Claude Code (เขียนโค้ด)**
ทั้งคู่อ่าน/เขียนไฟล์นี้ได้ · อัปเดตทุกครั้งที่ทำงานเสร็จเป็นก้อน
**บริบทเทคนิคของโปรเจกต์อยู่ที่ [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) — อ่านไฟล์นั้นก่อนเริ่ม แล้วไฟล์นี้เก็บแค่ "งานรอบนี้"**

> ⚠️ **[2026-09-15] ไฟล์นี้ถูกลบไปครั้งหนึ่งแล้วกู้คืนจากบทสนทนา** (ไม่เคย commit เข้า git เลย โดน "Discard changes" ทับตอนมีคน pull) — ดูหัวข้อ "กันไฟล์หายซ้ำ" ท้ายไฟล์ก่อนทำงานต่อ

---

## ROLES

| Agent | หน้าที่ | ห้ามทำ |
|---|---|---|
| **Gemini** | แตกงานเป็น task, ออกแบบ approach, เขียน PLAN ให้ละเอียดพอลงมือได้ | ไม่เขียนโค้ดจริง |
| **Claude Code** | ทำตาม PLAN, เขียน/แก้โค้ด, รันเทส, อัปเดต STATUS + LOG | ไม่เปลี่ยน scope เองโดยไม่โน้ตใน OPEN QUESTIONS |

---

## เจ้าของโปรเจกต์สื่อสารยังไง (อ่านก่อนเริ่มทุกครั้ง)

- เจ้าของเป็น **นศ.ฝึกงาน** อธิบายงานเป็น **ภาษาพูด / ความเข้าใจรวมๆ** ไม่ถนัดศัพท์เทคนิค — อย่าคาดหวัง spec ครบ
- หน้าที่ agent: **แปลงคำอธิบายกว้างๆ → task + spec ที่ชัดเจน** เอง แล้ว **ยืนยันกับเจ้าของก่อนลงมือเขียนโค้ดจริง** (approval gate)
- ข้อมูลไม่พอ → ถามเป็นข้อๆ **พร้อมเสนอ default** ไม่ใช่โยนศัพท์เทคนิคให้เจ้าของตัดสิน
- ทำงานเสร็จเป็นก้อน → **อัปเดตไฟล์นี้** (CURRENT TASK / PLAN / STATUS / LOG)
- Claude Code รัน Unity headless ไม่ได้ (Editor ล็อกโปรเจกต์) — งานที่ต้องรัน/เทสในเอนจิน ให้ส่งกลับเจ้าของทำ
- เจ้าของทำงานคู่กับเพื่อนร่วมทีม (`pongsatornthn-art`) ที่ commit/push ผ่าน GitHub Desktop เป็นหลัก บน branch `ทดสอบ`

---

## CURRENT TASK
> เป้าหมายรอบนี้

**ระบบเล็งปืน + แขนถือปืน แบบ 2.5D billboard (prototype โชว์ทีม)** — โค้ดเสร็จแล้ว อยู่ระหว่างเจ้าของต่อสาย/ตั้งค่าใน Unity Editor + จูนตัวเลขให้ลงตัวกับอาร์ตจริง

---

## STATUS
> todo / wip / done / blocked

| ระบบ | สถานะ | ไฟล์ | หมายเหตุ |
|---|---|---|---|
| เอกสารบริบท | done | `PROJECT_CONTEXT.md`, `HANDOFF.md` | สแกนโค้ดทั้งโปรเจกต์ครั้งแรก 2026-09-02 |
| ปืน 6 ทิศ + hand offset | **done (โค้ด)** | `ModularAimController.cs`, `WeaponSpriteRig.cs` | เจ้าของเทสแล้วผ่าน ปรับ `Left Side Push`/`Invert Mirror Rotation` จนโอเค |
| แขนถือปืน (elbow-anchor) | **wip (โค้ด done, จูนอยู่)** | `DirectionalArm.cs` | โค้ดนิ่งแล้ว เจ้าของกำลังไล่ตั้ง `Elbow_*` 6 จุด + trim 6 ทิศ ให้ตรงกับอาร์ต |
| ระบบ PTSD / minigame / storage / door / FPS cam | done (ฝั่งเพื่อน) | `PTSDManager.cs` ฯลฯ | เพื่อนร่วมทีมทำ push มาแล้ว (ดูหัวข้อถัดไป) |

### สิ่งที่เพื่อนร่วมทีม push มา (อ่านก่อนแตะระบบพวกนี้ — ไม่ได้ทำโดย Claude Code)
- **PTSDManager.cs เขียนใหม่เกือบหมด**: สลับ GameObject โลกจริง/โลกความทรงจำ (`realWorldEnv`/`memoryWorldEnv`) แทนการยิง event เฉยๆ, จำ/คืนตำแหน่งผู้เล่นข้ามโลก, มี Post-Processing Volume transition (จอมืด), ปุ่มเทสเปลี่ยนจาก `L` เป็น **`O` เข้า / `P` ออก**, มี `UnityEvent OnEnterPTSD/OnExitPTSD` — **`OnPTSDStateChanged` (event ที่ `MonsterSpawner` ใช้) ยังอยู่เหมือนเดิม ไม่พัง**
- ไฟล์ใหม่: `PTSDCameraShake.cs`, `PTSDFocusController.cs`, `PTSDVisualController.cs`, `PTSDCircularMinigameUI.cs`, `PTSDMinigameCharger.cs`, `PTSDMinigameController.cs`, `PTSDMinigameUI.cs`, `StressTriggerZone.cs`, `MiniGameManager.cs`, `MiniGameInteractable.cs`
- ระบบกล่อง/คลัง: `StorageBox.cs`, `StorageBoxUI.cs`, `StorageInteract.cs`, `LootTableData.cs`
- `FPSCameraController.cs` (ใหม่) — น่าจะคือระบบซูมคลิกขวาที่เคยบอกว่า "ยังไม่ไฟนอล" ตอนคุยเรื่องปืน — **เช็คกับเจ้าของว่าจะรวมกับ `AimCameraController.cs`/`ModularAimController.cs` ยังไงก่อนแก้อะไรเรื่องกล้อง**
- `Scene_test01.unity` ถูกแก้เยอะมาก (ประกอบฉากใหม่จำนวนมาก)

---

## ให้เจ้าของทำต่อใน Unity Editor

### ส่วน A — ปืน 6 ทิศ (เสร็จแล้ว เก็บไว้อ้างอิง)

```
Player
└─ GunPivot                 ← ModularAimController.cs
   └─ GunSprite              ← SpriteRenderer + WeaponSpriteRig.cs   (ลูกของ GunPivot)
```
ต่อช่อง: `Gun Pivot`/`Sprite Rig`/`Aim Origin` บน ModularAimController · ลากรูปปืน 3 ใบ (NE/E/SE) ใส่ WeaponSpriteRig
ปุ่มเทส: **`G`** บังคับโชว์ปืน · ปรับ `Left Side Push`, `Invert Mirror Rotation`, `Max Barrel Tilt` ตามที่จำเป็น

### ส่วน B — แขน (กำลังจูน) *[DirectionalArm.cs · หลักการเดียวกับปืน + ฐานหมุนย้ายตามจุดข้อศอก 6 ทิศ]*

**อาร์ต:** แขน 6 รูป (`Right/Left/FrontRight/FrontLeft/BackRight/BackLeft`) วาดแยกซ้าย-ขวา **ไม่ mirror** — pivot ทุกใบที่ **ข้อศอก** (Sprite Editor → Pivot Custom)
รูปใหญ่ไป → เพิ่ม `Pixels Per Unit` ตอน import หรือย่อ `Scale` ของก้อน Arm (โค้ดไม่แตะ scale)

**Hierarchy** (สคริปต์อยู่ก้อนเดียวกับ SpriteRenderer ไม่มีก้อนลูก):
```
Player
├─ Elbow_Right / Elbow_Left / Elbow_FrontRight / Elbow_FrontLeft / Elbow_BackRight / Elbow_BackLeft   (empty)
└─ Arm                      ← SpriteRenderer + DirectionalArm.cs   (ตำแหน่งไม่ต้องตั้ง โค้ดย้ายไปเกาะ Elbow เอง)
```

**วางจุด Elbow:** สลับรูป body ทีละท่า (ผ่าน Animator หรือใส่รูปชั่วคราว) → เลื่อน empty ตัวนั้นให้ทับข้อศอกในรูป → ทำครบ 6 ท่า

**ต่อช่อง DirectionalArm:** `Aim Controller` = GunPivot · `Arm Renderer` เว้นว่างได้ (หาเองในก้อน) · รูปแขน 6 ใบ + จุด Elbow 6 อัน **ต้องต่อครบ** (Console เตือนถ้าขาด)

**จูน (เจ้าของกำลังทำอยู่):**
- `Sprite Offset` = ปรับก่อนให้ทิศ **Right** ตรง (ทีละ 90)
- แต่ละทิศที่เหลือยังเอียงไม่ตรง → ใส่ `Trim Right / Trim Back Right / Trim Back Left / Trim Left / Trim Front Left / Trim Front Right` **ทีละอัน** (รูปแขนแต่ละใบวาดคนละองศา ต้องจูนแยก 6 ค่า อิสระต่อกัน)
- แขนไม่ตามจุด/ลอยกลางตัว → เช็ค Console ต้องไม่มี warning "ยังไม่ได้ลาก Elbow_..." · เปิด `Debug Log` ดูว่าทิศไหนเกาะ Elbow ตัวไหน
- แขนบัง/ถูกบังผิด → `Order Side/Back/Front` · ตามช้า/เร็วไป → `Lerp Speed` · เอียงมาก/น้อยไป → `Max Tilt`

**ยังไม่ทำ:** มือแตะด้ามปืนเป๊ะ (ต้อง IK จริง — เคยลองแล้วแขนล็อกตัว L เลยเลี่ยง), แขนข้างที่ 2, sync กับ `FPSCameraController.cs` ที่เพื่อนเพิ่งเพิ่มมา

---

## DECISIONS & NOTES

- แยกเอกสารเป็น 2 ไฟล์: `PROJECT_CONTEXT.md` (บริบทคงที่) + `HANDOFF.md` (งานรอบต่อรอบ) เพื่อประหยัดโทเคนตอนอ่านซ้ำ
- **ระบบแขนลองมาแล้ว 3 แบบ** ก่อนจะนิ่งที่แบบปัจจุบัน: (1) IK cosine 2 ท่อนเต็มรูปแบบ → แขนล็อกเป็นตัว L คุมยาก (2) แขนรูปเดียวหมุนอิสระ 360° → ทิศเพี้ยน/มินเรอกลับด้าน (3) **ปัจจุบัน**: แขนรูปเดียวต่อทิศ (6 รูป ไม่ mirror) หมุนแค่ "ส่วนโยก" เหมือนปืน + ฐานหมุนย้ายไปเกาะจุดข้อศอกที่วางมือ 6 จุด — แก้ทั้งปัญหาไหล่ไม่ตามตัวและผีแขน
- ปืนก็ตัดทิศบน/ล่างออกจาก 8 ทิศ เหลือ 6 ทิศ (ทิศละ 60°) เพราะปืนตั้งชันจ่อตัวละครแล้วท่าแปลก — ใช้หลักการเดียวกับแขน (ที่จริงแขนลอกปืนมา)

---

## OPEN QUESTIONS

- `FPSCameraController.cs` ที่เพื่อนเพิ่มมา จะเอามาแทน/ผสานกับ `AimCameraController.cs` เดิม (คลิกขวาซูม) ยังไง — ยังไม่ไฟนอล ต้องคุยกับเพื่อนก่อน
- แขน 2 ท่อนแบบ IK จริง (มือแตะด้ามปืนเป๊ะ) จะกลับมาทำไหม หรือพอใจกับแบบ 6-รูป-ต่อทิศแล้ว
- branch ทำงาน: ยังใช้ `ทดสอบ` ต่อ (เพื่อนก็ push เข้า branch นี้)

---

## HANDOFF LOG
> append เท่านั้น · ล่าสุดอยู่บนสุด

- **[2026-09-15] Claude Code:** เจ้าของแจ้งว่า `HANDOFF.md` + สคริปต์ปืน/แขน 3 ไฟล์ (`ModularAimController.cs`, `WeaponSpriteRig.cs`, `DirectionalArm.cs`) หายจากดิสก์ — เช็คแล้วไฟล์เหล่านี้**ไม่เคย commit เข้า git** เลย (ยัง untracked) ส่วนเพื่อนร่วมทีม push 4 commit ใหม่เข้ามาและมีคน pull (fast-forward) พร้อมกัน คาดว่าโดน "Discard changes" ใน GitHub Desktop ลบไฟล์ untracked ทิ้งไปด้วย (ไม่ใช่จาก pull โดยตรง) → **กู้คืนไฟล์ทั้งหมดจากเนื้อหาที่คุยกันในบทสนทนา** (โค้ดตรงเวอร์ชันล่าสุดที่เจ้าของเทสอยู่) → แนะนำให้ commit ทันทีหลังกู้ ดูหัวข้อ "กันไฟล์หายซ้ำ"
- **[2026-09-04] Claude Code:** วนแก้แขนหลายรอบตามฟีดแบ็ก (L-lock → หมุนอิสระ 360° → ทิศเพี้ยน) จนสรุปที่ดีไซน์ปัจจุบัน (6 รูปต่อทิศ + จุด Elbow 6 อัน + trim แยก 6 ทิศ)
- **[2026-09-03] Claude Code:** ปรับปืนจาก 8→6 ทิศ (ตัดบน/ล่าง) + `maxBarrelTilt`, เริ่มระบบแขน (ลอง IK ก่อนแล้วเปลี่ยนทาง), สร้าง/อัปเดต `PROJECT_CONTEXT.md`
- **[2026-09-02] Gemini:** แก้บัคบอสเดินติดกำแพง/วาร์ปตกโลก, ผสาน ItemData OOP เข้ากับ PlayerCombat, เริ่มระบบปืน 360° (`ModularAimController` เวอร์ชันแรก)
- **[2026-09-02] Claude Code:** สแกนโค้ดทั้งโปรเจกต์ครั้งแรก สร้าง `PROJECT_CONTEXT.md` + `HANDOFF.md`

---

## ⚠️ กันไฟล์หายซ้ำ (สำคัญ — อ่านก่อนทำงานรอบหน้า)

**สาเหตุที่ไฟล์หายรอบนี้:** ไฟล์ที่ Claude Code สร้าง/แก้ (โค้ดใหม่ + `HANDOFF.md`) ไม่เคยถูก `git commit` เลย ค้างเป็น **untracked** อยู่ในเครื่อง พอมีคน pull งานจากเพื่อนพร้อมกับกด "Discard changes" (GitHub Desktop) — ปุ่มนี้ลบไฟล์ untracked ทิ้งด้วย ไม่ใช่แค่ revert ของที่ commit แล้ว — ไฟล์เลยหายแบบกู้จาก git ไม่ได้ (เพราะไม่เคยอยู่ใน git ตั้งแต่แรก)

**ทางป้องกัน:**
1. **Commit บ่อยๆ** หลัง Claude Code แก้ไฟล์เสร็จเป็นก้อน (แม้จะยังไม่เทสสมบูรณ์ก็ commit ไว้ก่อน แก้ต่อได้)
2. ก่อนกด **"Discard changes"** ใน GitHub Desktop ทุกครั้ง — เช็คในลิสต์ว่ามีไฟล์ที่ยังไม่อยากทิ้งไหม (โดยเฉพาะไฟล์ .md และสคริปต์ใหม่ๆ)
3. ก่อน pull ถ้ามีการแก้ในเครื่องอยู่ ให้ **commit ก่อน pull เสมอ** (หรืออย่างน้อย stash) อย่าปล่อยเป็น untracked ค้างไว้ข้ามวัน
4. `HANDOFF.md` และ `PROJECT_CONTEXT.md` ควร commit เข้า repo ให้เป็นปกติ เหมือนไฟล์โค้ด ไม่ใช่ปล่อยลอยไว้

---

## REPO CONTEXT (คงที่ อัปเดตเมื่อโครงสร้างเปลี่ยน)

- Unity 6000.0.33f1 · URP · โปรเจกต์จริง = `d:/thesis/Pre_thesis_Game2.5D/` เท่านั้น
- โค้ด: `Assets/Script/` (Core / Combat / Enemies / Inventory / Player / Managers)
- ฉาก: `Assets/Scenes/` (`GameScene`, `SampleScene`, `Scene_test01`)
- prefab: `Assets/Prefab/` + ปนใน `Assets/Script/` บางส่วน
- git: repo แยกใน `Pre_thesis_Game2.5D/` · branch หลัก `main` · branch ทำงาน `ทดสอบ` (เจ้าของ + เพื่อนร่วมทีม `pongsatornthn-art` push เข้า branch นี้ทั้งคู่ผ่าน GitHub Desktop)
- รันเทส/รันเกม: เจ้าของทำใน Unity Editor (Claude Code รัน headless ไม่ได้)
- commit message: ภาษาไทย สั้นๆ
