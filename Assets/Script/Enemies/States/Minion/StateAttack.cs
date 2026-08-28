using UnityEngine;

public class StateAttack : IMonsterState
{
    private enum AttackPhase { Windup, Lunge, Recovery, CallBoss }
    private AttackPhase currentPhase;
    private float timer;
    private Vector3 lungeDirection;
    
    // Configurable values (ปรับความเร็ว/เวลาได้ตรงนี้)
    private float windupDuration = 0.8f; // 🌟 เวลาง้าง (เร็วขึ้นนิดหน่อย)
    private float lungeDuration = 0.2f; // 🌟 เวลาพุ่ง (สั้นๆ กระชับๆ เหมือนก้าวเท้าฟัน)
    private float recoveryDuration = 2.5f; // 🌟 เวลาเหนื่อยหอบ (เพิ่มเวลาให้ผู้เล่นกะตีหนักทัน)
    private float lungeSpeed = 12f; 
    private bool hasHitPlayer = false;
    private bool hasLunged = false; // บัครอบก่อนมันไม่ยอมพุ่ง เพราะไปเช็ค timer == 0f

    public void EnterState(PTSDMonsterAI ai)
    {
        if (ai.Agent.isOnNavMesh) 
        {
            ai.Agent.ResetPath(); // ล็อกขาให้นิ่งตอนตี
            ai.Agent.velocity = Vector3.zero;
        }
        timer = 0f; 
        hasHitPlayer = false;

        // 🌟 สุ่มกาชาการโจมตีตาม GDD (90% พุ่งตี / 10% เรียก Stalker)
        int rand = Random.Range(1, 101);
        if (rand <= 10)
        {
            currentPhase = AttackPhase.CallBoss;
            ai.hasSuperArmor = true; // ได้รับ Super Armor ทันทีตอนง้างตะโกน
            ai.ShowDebugText("CHARGING SCREAM!", Color.magenta);
            Debug.Log($"<color=magenta>{ai.gameObject.name} กำลังชาร์จเสียงกรีดร้องเรียกบอส! (2.5 วิ)</color>");
        }
        else
        {
            currentPhase = AttackPhase.Windup;
            ai.hasSuperArmor = true; // 🌟 ป้องกันการโดนขัดจังหวะขณะง้างฟัน
            ai.ShowDebugText("WINDUP!", new Color(1f, 0.5f, 0f)); // สีส้มเตือนว่ากำลังจะพุ่ง
            Debug.Log($"<color=orange>[Debug AI] เข้าโหมดง้างตี! (หยุดนิ่ง {windupDuration} วิ)</color>");
            
            // เล็งทิศทางไปหาผู้เล่นเก็บไว้ (ดักยิง/ดักพุ่ง) จะพุ่งไปตามทิศนี้เท่านั้น
            lungeDirection = (ai.PlayerTransform.position - ai.transform.position).normalized;
            lungeDirection.y = 0; // ล็อกแกน Y ไม่ให้พุ่งเหินฟ้า
        }
    }

    public void UpdateState(PTSDMonsterAI ai)
    {
        timer += Time.deltaTime;

        if (currentPhase == AttackPhase.CallBoss)
        {
            // 🌟 ไอเดียที่ 1+3: ยืนชาร์จ 2.5 วินาที พอครบจะทำดาเมจคนรอบๆ แล้วเรียกบอส
            if (timer >= 2.5f)
            {
                ai.hasSuperArmor = false;
                
                // ตรวจสอบระยะผู้เล่น ถ้าอยู่ใกล้จะจับขาทำดาเมจและสตัน (ตาม GDD)
                if (Vector3.Distance(ai.transform.position, ai.PlayerTransform.position) <= 2.5f)
                {
                    PlayerMovement p = ai.PlayerTransform.GetComponent<PlayerMovement>();
                    if (p != null) 
                    {
                        Debug.Log("<color=red>โดนจับขา! ผู้เล่นติดสตันจากเสียงกรีดร้อง!</color>");
                        p.TakeDamage(ai.attackDamage);
                        p.ApplyStun(2.0f); // สตัน 2 วิ
                    }
                }
                
                ai.ShoutForStalker(); 
                ai.ChangeState(new StateChase());
            }
        }
        else if (currentPhase == AttackPhase.Windup)
        {
            // หมดเวลาง้าง -> เข้าสู่ช่วงพุ่งทะลวง (Lunge)
            if (timer >= windupDuration)
            {
                currentPhase = AttackPhase.Lunge;
                timer = 0f;
                ai.hasSuperArmor = true; // 🌟 เปิด Super Armor ตอนพุ่งลงดาบตาม GDD
                ai.ShowDebugText("LUNGE!", Color.red);
                
                if (ai.Agent.isOnNavMesh)
                {
                    ai.Agent.ResetPath();
                    ai.Agent.velocity = Vector3.zero; // เบรกความเร็วเก่าทิ้งให้หมด
                }
            }
        }
        else if (currentPhase == AttackPhase.Lunge)
        {
            if (ai.Agent.isOnNavMesh && !hasLunged) 
            {
                hasLunged = true;
                ai.Agent.speed = lungeSpeed;
                ai.Agent.acceleration = 200f; // อัตราเร่งสูงปรี๊ดเพื่อพุ่ง
                ai.Agent.angularSpeed = 0f; // ล็อคคอ! ห้ามหันหน้าตามผู้เล่นเด็ดขาด (แก้บัคล็อคเป้า)
                
                // พุ่งสั้นๆ (Dash) แบบก้าวเท้าเข้ามาฟัน
                Vector3 targetLunge = ai.transform.position + (lungeDirection * 1.5f);
                ai.Agent.SetDestination(targetLunge);
                Debug.Log($"<color=red>[Debug AI] เริ่มพุ่งตัว! สปีด: {ai.Agent.speed} ไปยังพิกัดข้างหน้า!</color>");
            }
            
            // ระหว่างที่กำลังพุ่ง ให้สแกนเช็คว่าร่างกายชนผู้เล่นหรือยัง
            if (!hasHitPlayer)
            {
                // ใช้ OverlapSphere รัศมี 1.2 รอบตัวมอนสเตอร์ ถ้าระยะชนถึงผู้เล่น ให้ทำดาเมจ
                Collider[] hits = Physics.OverlapSphere(ai.transform.position, 1.2f, ai.playerMask);
                foreach (var hit in hits)
                {
                    if (hit.transform == ai.PlayerTransform || hit.transform.root == ai.PlayerTransform.root)
                    {
                        hasHitPlayer = true; // โดนแล้วจำไว้ จะได้ไม่โดนซ้ำ
                        Debug.Log($"<color=red>💥 พุ่งชนเต็มๆ! โดน {ai.attackDamage} ดาเมจ!</color>");
                        PlayerMovement p = ai.PlayerTransform.GetComponent<PlayerMovement>();
                        if (p != null) p.TakeDamage(ai.attackDamage);
                        break;
                    }
                }
            }

            // หมดเวลาพุ่ง -> เข้าสู่ช่วงพักฟื้น (Recovery)
            if (timer >= lungeDuration)
            {
                currentPhase = AttackPhase.Recovery;
                timer = 0f;
                ai.hasSuperArmor = false; // 🌟 ปิด Super Armor ตอนพักฟื้น
                ai.ShowDebugText("TIRED...", Color.gray);
                Debug.Log($"<color=grey>[Debug AI] พุ่งเสร็จแล้ว! เข้าโหมดพักเหนื่อย (เดินช้าๆ) เป็นเวลา {recoveryDuration} วิ</color>");
                
                if (ai.Agent.isOnNavMesh)
                {
                    ai.Agent.ResetPath();
                    ai.Agent.velocity = Vector3.zero; 
                    ai.Agent.isStopped = true; // หยุดนิ่งสนิท ห้ามเดินไปไถลชนผู้เล่น
                }

                // ผู้เล่นรีเควส: ขอให้เกจเต็ม 100 เพื่อเทสดูว่าถ้ายืนพักเฉยๆ แล้วจะหนีพ้นไหม
                ai.Awareness.ForceMaxAwareness(); 
            }
        }
        else if (currentPhase == AttackPhase.Recovery)
        {
            // ไม่ต้องทำอะไร ปล่อยให้มันยืนเหนื่อยหอบเฉยๆ ไม่ต้องพยายามเดินไปหาผู้เล่นแล้ว (แก้บัคพยายามสิงร่าง)
            
            // 🌟 หมดเวลาเหนื่อยหอบ ค่อยกลับไปวิ่งไล่กวดต่อ
            if (timer >= recoveryDuration)
            {
                Debug.Log($"<color=white>[Debug AI] หายเหนื่อยแล้ว! กลับสู่สถานะประเมินสถานการณ์</color>");
                ai.ChangeState(new StateChase());
            }
        }
    }

    public void ExitState(PTSDMonsterAI ai)
    {
        ai.hasSuperArmor = false; // เผื่อกรณีโดนขัดจังหวะ จะได้ลบ Super Armor ทิ้ง
        
        if (ai.Agent.isOnNavMesh)
        {
            ai.Agent.ResetPath(); 
            ai.Agent.velocity = Vector3.zero;
            ai.Agent.speed = ai.chaseSpeed; 
            ai.Agent.acceleration = 8f; 
            ai.Agent.angularSpeed = 120f; 
            ai.Agent.isStopped = false; // ปลดล็อคขาให้กลับมาเดินได้
        }
    }
}
