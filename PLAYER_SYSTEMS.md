# 🎮 PLAYER_SYSTEMS — ทุกระบบที่อยู่บนตัวผู้เล่น

> สรุป ณ 2026-09-16 · ไล่อ่านจากโค้ดจริงทั้งหมด ไม่ใช่เดา
> ของบน Player มาจาก 3 แหล่ง: **[เดิม]** ของที่มีมาก่อน · **[เรา]** ที่ทำกับ Claude Code · **[เพื่อน]** ที่ `pongsatornthn-art` push มา
> ภาพรวมทั้งเกมอยู่ที่ [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) · งานรอบปัจจุบันอยู่ที่ [HANDOFF.md](HANDOFF.md)

---

## 1. การเคลื่อนที่ — `PlayerMovement.cs` [เดิม + เราแก้]

| เรื่อง | รายละเอียด |
|---|---|
| เดิน | `WASD` อิงทิศกล้อง (กล้องหมุนแล้วทิศเดินหมุนตาม) มี accel/decel ไม่ใช่หยุดทันที |
| ย่องเบา | `Ctrl` หรือ `C` = **toggle** (กดติดกดดับ) ความเร็ว × `sneakSpeedMultiplier` (0.5) · มอนสเตอร์เห็นช้าลงครึ่งนึง |
| แดช | `Space` กินสตามินา 25 · ระหว่างแดช **อมตะ (i-frame)** 0.2 วิ · คูลดาวน์ 1 วิ |
| สตามินา | ฟื้นเอง 15/วิ · มี Slider UI |
| เลือด | `maxHealth` 100 · `TakeDamage()` (เช็ค i-frame) / `TakeRawDamage()` (ทะลุ i-frame ใช้ในมินิเกมบอส) |
| สตัน | `ApplyStun(วินาที)` — มอนสเตอร์เรียกตอนจับขา/jumpscare |
| หยุดเดินตอนตี | `ApplyAttackPause(0.4)` — `PlayerCombat` เรียกตอนฟันดาบ |
| เดินช้าลง | ตอนชาร์จฟันหนัก (`heavyChargeSpeedMultiplier` 0.7) · **[เรา V7]** ตอนคลิกขวาค้างนิ่งปืน (`steadyAimSpeedMultiplier` 0.5) |
| หันหน้า | **[เรา V7]** หันตามเมาส์บนพื้นเสมอ — **ถือปืน = หันลื่น (raw)** / **มือเปล่า-ดาบ = snap 8 ทิศ** |

⚠️ `Die()` แค่ `Debug.Log` ยังไม่มี Game Over จริง · ไม่ได้ implement `IDamageable` (มอนสเตอร์เรียก `TakeDamage` ตรงๆ)

---

## 2. ระบบต่อสู้ — `PlayerCombat.cs` [เดิม + เราแก้ V7]

### อาวุธประชิด (ดาบ/ขวาน/มือเปล่า)
- คลิกซ้าย **กดค้าง ≥ 0.4 วิ = ฟันหนัก**, ต่ำกว่านั้น = ฟันเบา
- ชาร์จครบ 1.5 วิ ปล่อยฟันอัตโนมัติ
- คูลดาวน์แยกเบา/หนักตาม `MeleeWeaponData` · หยุดเดิน 0.4 วิ ตอนฟัน
- มือเปล่าใช้ค่า default ในสคริปต์ (damage 20 / heavy 35)

### ปืน — **[เรา V7] แบบ Alien Shooter**
- **ถือปืน = คลิกซ้ายยิงได้ทันที** (เมื่อก่อนต้องคลิกขวาเล็งก่อน — เอาออกแล้ว)
- **คลิกขวาค้าง = "นิ่งขึ้น"** → กรวยกระสุนแคบลง × `steadySpreadMultiplier` (0.35) + เดินช้าลง × `steadyAimSpeedMultiplier` (0.5)
- กรวยกระสุน (spread): เดิน = บานสุด · ยืนนิ่ง = หุบเข้าหา 0 ตาม `focusTime` · ยิงแต่ละนัดบานเพิ่ม (`recoilSpread`) · ใช้ `Mathf.MoveTowards` เส้นเดียวจบ
- ยิง 2 จังหวะ: raycast จากกล้องผ่านเมาส์หาจุดเป้าจริงก่อน → แล้วยิงจาก `attackPoint` ไปจุดนั้น (+สุ่มองศาในกรวย)
- `R` รีโหลด · **จำกระสุนแยกทีละกระบอก** (`Dictionary<ItemData,int>`) สลับปืนแล้วกระสุนไม่รีเซ็ต
- `isAiming` = "กดคลิกขวาค้างอยู่" เท่านั้น **ไม่ใช่โหมดกล้อง FPS อีกแล้ว**
- เปิด `HasRangedWeaponEquipped` ให้สคริปต์อื่นเช็คว่าถือปืนอยู่ไหม

---

## 3. ระบบ Hitbox ฟันดาบ — `CombatAnimationReceiver.cs` + `MeleeHitbox.cs` [เดิม]

- แอนิเมชันยิง **Animation Event** `OnAttackActive()` → เปิด hitbox + ส่งค่าดาเมจ/knockback/หนักเบา + เสก VFX รอยดาบ (ล็อก 8 ทิศตามเมาส์, billboard เข้ากล้อง, ดึงเข้าหากล้อง 0.3 กัน sorting จม)
- `OnAttackDeactive()` → ปิด hitbox · มี auto-disable 0.5 วิ กันบัค event ไม่ยิง
- `MeleeHitbox` = trigger รูปพัด เช็ค `arcAngle` (70°) ว่าศัตรูอยู่ในองศาไหม · จำรายชื่อที่ฟันโดนแล้วกันตีซ้ำในสวิงเดียว

---

## 4. ระบบปืน/แขน 2.5D — `ModularAimController` + `WeaponSpriteRig` + `DirectionalArm` [เรา]

| สคริปต์ | ทำอะไร |
|---|---|
| `ModularAimController` (บน GunPivot) | billboard เข้ากล้อง · คำนวณองศาเมาส์ → snap **6 ทิศ (60°)** · โยกลำกล้อง ±`maxBarrelTilt` · เลื่อนปืนไปฝั่งที่หัน (hand offset) |
| `WeaponSpriteRig` (บน GunSprite) | สลับรูปปืน 6 ทิศจาก **3 รูป** (NE/E/SE) ฝั่งซ้ายมิเรอร์ด้วย `localScale.x=-1` · `leftSidePush` กันปืนทับตัว · **ปุ่ม `G` = บังคับโชว์ปืนตอนเทส** |
| `DirectionalArm` (บนก้อน Arm) | สลับรูปแขน **6 รูป** (แยกซ้ายขวา ไม่มิเรอร์) · ฐานหมุนย้ายไปเกาะ `Elbow_<ทิศ>` (empty 6 จุดที่วางมือตรงข้อศอก body แต่ละท่า) · trim แยก 6 ทิศ |

> ไม่มีทิศบน/ล่าง — ตัดออกเพราะปืนตั้งชันจ่อตัวละครแล้วท่าแปลก · ทิศเฉียงขยายเป็นทิศละ 60° แทน

---

## 5. เอฟเฟกต์การยิง [เดิม]

- `BulletTracer.cs` — เส้น LineRenderer จากปากกระบอก→จุดชน เฟดหายใน 0.1 วิ แล้วลบตัวเอง
- `MuzzleFlashEffect.cs` — เปิด Light วาบ 0.05 วิ ที่ปลายปืน
- `VFXSelfDestroy.cs` — ลบ VFX ทิ้งตาม `lifetime` (ใช้กับควันดาบ/เลือด)

---

## 6. แอนิเมชัน — `PlayerAnimationController.cs` [เดิม + เราแก้]

เลือก state ตามลำดับ: `Shoot_Tree` → `Aim_Idle` → `Gun_Movement` → `Idle_Gun_Tree` (ตอนถือปืน) / `Movement` → `Idle` (ไม่ถือปืน)
**[เรา V7]** ยิงไม่ต้องเช็ค `isAiming` แล้ว · บล็อกหันหน้าตาม WASD ทำงานเฉพาะตอน **ไม่ถือปืน** (กันแย่งคุมทิศกับ PlayerMovement)

---

## 7. กล้อง

| สคริปต์ | ใช้ทำอะไร | สถานะ |
|---|---|---|
| `AimCameraController.cs` [เดิม] | แพนกล้องตามเมาส์ (lerp, จำกัดระยะ) + สลับ vCam ปกติ/เล็ง | **ต้อง unassign `Vcam Aiming` ใน Inspector** เพื่อปิดกล้องซูม FPS ตามดีไซน์ใหม่ |
| `FPSCameraController.cs` [เพื่อน] | **ไม่เกี่ยวกับปืน!** เป็นกล้องส่องหาของในมินิเกม — หมุนเมาส์จำกัดองศา + ยิง ray หา `MiniGameInteractable` แล้วคลิก | ใช้ในซีนมินิเกมเท่านั้น |

---

## 8. ปฏิสัมพันธ์กับของในฉาก

| สคริปต์ | ปุ่ม | ทำอะไร |
|---|---|---|
| `PlayerInteraction.cs` [เดิม] | `E` | เล็งเมาส์ใส่ไอเทม (raycast `itemLayer`, ระยะ 3m) → เก็บของ + โชว์ prompt |
| `DoorController.cs` [เดิม] | `E` | เปิดประตู เช็ค `KeyItemData.targetDoorID` ในกระเป๋า |
| `StorageInteract.cs` [เพื่อน] | `E` / `ESC` | เดินเข้าใกล้กล่อง → เปิด/ปิด UI กล่องเก็บของ + กระเป๋าผู้เล่นพร้อมกัน |
| `MiniGameInteractable.cs` [เพื่อน] | คลิกซ้าย (ในโหมดมินิเกม) | วัตถุที่ส่องแล้วเรืองแสง คลิกแล้วยิง `UnityEvent` + โชว์ dialogue hint |
| `StressTriggerZone.cs` [เพื่อน] | เดินชน | เดินเข้าโซน → สั่ง `PTSDManager.SetPTSDState(true)` เข้าโลก PTSD (มี cooldown / ตั้งให้ทำงานครั้งเดียวได้) |
| `PTSDMinigameCharger.cs` [เพื่อน] | `Space` ค้าง | เฉพาะตอนอยู่ในโลก PTSD — กดค้างชาร์จหลอด 2 วิ เต็มแล้วยิง event เข้ามินิเกม |

---

## 9. UI ที่ผูกกับผู้เล่น

- `CrosshairController.cs` [เดิม] — โชว์เป้าเฉพาะตอนถือปืน + ไม่ได้เปิดกระเป๋า · **ขนาดเป้าบานตาม `currentSpreadAngle`** (เห็นกรวยกระสุนจริง) · ซ่อนเมาส์ Windows ตอนถือปืน
- `AmmoUI.cs`, Health/Stamina Slider, `InventoryUI` / `HotbarController` (เลข 1-4 + scroll)
- `JournalManager` — `J` เปิดสมุดบันทึก

---

## 10. สรุปปุ่มทั้งหมด (ปัจจุบัน)

| ปุ่ม | ทำอะไร |
|---|---|
| `WASD` | เดิน (อิงทิศกล้อง) |
| `Ctrl` / `C` | toggle ย่องเบา |
| `Space` | แดช · **(ในโลก PTSD)** กดค้างชาร์จมินิเกม |
| คลิกซ้าย | ฟันดาบ (ค้าง = ฟันหนัก) · **ยิงปืนได้ทันที** |
| คลิกขวา (ค้าง) | **นิ่งขึ้น** — กรวยแคบ + เดินช้า (ไม่ซูมกล้องแล้ว) |
| `R` | รีโหลด |
| `E` | เก็บของ / เปิดประตู / เปิดกล่อง / กดรัวหนี jumpscare |
| `1-4` / scroll | เลือกช่อง hotbar |
| `J` | สมุดบันทึก |
| `ESC` | pause / ปิด UI กล่อง |
| `G` | **(เทส)** บังคับโชว์ปืน |
| `O` / `P` | **(เทส)** เข้า / ออก โลก PTSD (เมื่อก่อนเป็น `L`) |
| `L` | ❌ ใช้ไม่ได้แล้ว (เพื่อนเปลี่ยนเป็น O/P) |

---

## 11. ⚠️ ของค้าง / ปัญหาที่ต้องจัดการ

| # | เรื่อง | ความร้ายแรง |
|---|---|---|
| 1 | ~~`AimPivotController.cs` ล็อกเคอร์เซอร์ตอนคลิกขวา~~ | ✅ **แก้แล้ว 2026-09-16** — ถอดการทำงานออกหมด เหลือคลาสเปล่ากัน Missing Script · **เจ้าของลบ component ออกจากซีนได้เลย** |
| 2 | ~~`CrosshairController` ล็อกเป้ากลางจอตอนคลิกขวา~~ | ✅ **แก้แล้ว 2026-09-16** — เป้าตามเมาส์ตลอดเวลาแล้ว |
| 2.5 | ~~กดคลิกขวาแล้วสไปรท์หันหน้าขึ้นค้าง~~ | ✅ **แก้แล้ว 2026-09-16** — ต้นเหตุ: state **`Aim_Idle`** (ท่าเล็ง FPS เก่า หันหลังให้กล้อง) ใน `Player_Anim.controller` ถูกเรียก 2 ทาง: (1) `PlayerCombat` ส่ง `SetBool("IsAiming", true)` → มี transition 4 เส้นในตัว controller ลากเข้า state นั้น (2) `PlayerAnimationController` เรียก `Play("Aim_Idle")` ตรงๆ · ตอนนี้บังคับส่ง `IsAiming = false` เสมอ + ตัดสาขา `Aim_Idle` ออกแล้ว |
| 3 | `AnimationEventHandler.cs` เรียก `PerformStrikeDamage()` ที่ถูกยกเลิกไปแล้ว (ขึ้น warning เปล่าๆ) | 🟡 ของตายค้าง |
| 8 | state `Aim_Idle` + transition `IsAiming` 4 เส้น ยังค้างใน `Player_Anim.controller` (ตอนนี้ไม่มีอะไรไปกระตุ้นแล้ว ไม่พังอะไร) | 🟢 ลบทิ้งใน Animator window ได้ถ้าอยากสะอาด |
| 4 | `AimX`/`AimZ`/`Speed` ถูกเขียนทับจาก 3 สคริปต์ (PlayerMovement / PlayerAnimationController / PlayerCombat) | 🟡 บรรเทาแล้วเฉพาะตอนถือปืน เคสอื่นยังเสี่ยงกระตุก |
| 5 | `Debug.Log` ทุกเฟรมใน `PlayerMovement.HandleFacingDirection()` | 🟡 กิน perf + สแปม Console |
| 6 | Player ไม่ implement `IDamageable` · `Die()` ไม่ทำอะไร (ยังไม่มี Game Over) | 🟡 ต้องทำก่อนส่งจริง |
| 7 | `PTSDFocusController.SetFocusState()` [เพื่อน] มีฟังก์ชันเบลอฉากหลังไว้แล้ว **แต่ยังไม่มีใครเรียก** — เอามาใช้ตอนคลิกขวา "นิ่งขึ้น" ได้เลย ของฟรี | 🟢 โอกาสต่อยอด |
