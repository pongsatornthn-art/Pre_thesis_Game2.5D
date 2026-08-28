using UnityEngine;


public class StalkerStateKill : IStalkerState
{
    private float timer;
    private float damageAccumulator;
    private bool minigameActive;
    private PlayerMovement player;

    public void EnterState(StalkerAI ai)
    {
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = true;
        
        Debug.Log("<color=red>💀 JUMPSCARE! STALKER จับตัวผู้เล่นได้แล้ว!</color>");
        Debug.Log($"<color=yellow>[System] แจ้งเตือน: เข้าสู่ Mini-Game ให้กดรัวๆ [{ai.requiredInputButton}] เพื่อดิ้นหลุด!</color>");
        
        player = ai.PlayerTransform.GetComponent<PlayerMovement>();
        if (player != null) 
        {
            // หยุดเวลาโลกให้มอนสเตอร์ตัวอื่นขยับไม่ได้
            Time.timeScale = 0f;
            
            // เริ่มต้นมินิเกม
            timer = 0f;
            damageAccumulator = 0f;
            minigameActive = true;
            
            var minigameUI = ServiceLocator.Get<IMinigameService>();
            if (minigameUI != null)
            {
                minigameUI.ShowMinigameUI(ai.requiredInputButton);
            }
        }
        else
        {
            // ถ้าไม่เจอผู้เล่น ให้กลับไปเดินเล่น
            ai.ChangeState(new StalkerStateLurk());
        }
    }

    public void UpdateState(StalkerAI ai) 
    { 
        if (!minigameActive || player == null) return;
        
        // เราใช้ Time.unscaledDeltaTime เพราะ timeScale = 0
        float dt = Time.unscaledDeltaTime;
        timer += dt;
        
        // 1. คำนวณดาเมจพื้นฐานที่โดนสูบต่อเฟรม
        float currentDrain = ai.baseHpDrainRate * dt;
        
        // 2. เช็คว่าผู้เล่นกดปุ่มรัวๆ ไหม
        if (Input.GetKeyDown(ai.requiredInputButton))
        {
            // พอกด 1 ครั้ง ดาเมจที่กำลังจะโดนจะลดลง (หรือติดลบจนกลายเป็นฟื้นฟูเลือดถ้ากดเร็วพอ)
            currentDrain -= ai.drainReductionPerPress;
            Debug.Log($"<color=green>⚡ กดปุ่ม! ลดดาเมจไป {ai.drainReductionPerPress}</color>");
        }
        
        // 3. สะสมดาเมจไว้ในหลอด
        damageAccumulator += currentDrain;
        
        // 4. ถ้าดาเมจสะสมเกิน 1 หน่วย ให้แปลนเป็นดาเมจจริงไปหักเลือด
        if (damageAccumulator >= 1f)
        {
            int damageToApply = Mathf.FloorToInt(damageAccumulator);
            player.TakeRawDamage(damageToApply);
            damageAccumulator -= damageToApply;
        }
        // ถ้าผู้เล่นกดรัวมากจนค่าสะสมติดลบ (ฟื้นฟู)
        else if (damageAccumulator <= -1f)
        {
            // ระบบยังไม่ได้ทำฟื้นฟูเลือด เลยให้เคลียร์ทิ้งเฉยๆ
            damageAccumulator = 0f;
        }

        // 5. อัปเดต UI (ถ้ามี)
        var minigameUI = ServiceLocator.Get<IMinigameService>();
        if (minigameUI != null)
        {
            // ดึงเลือดปัจจุบันและเลือดสูงสุดมาแสดงที่หลอด UI
            float timeLeft = Mathf.Max(0, ai.minigameDuration - timer);
            minigameUI.UpdateMinigameUI(timeLeft, player.CurrentHealth, player.maxHealth); 
        }

        // 6. เช็คผลแพ้ชนะ (วินาทีชี้ชะตา)
        // กรณีที่ 1: แพ้ (ตายก่อนหมดเวลา)
        // เนื่องจาก PlayerMovement.TakeRawDamage() มีเช็ค Die() อยู่แล้ว 
        // เราแค่เช็คว่าเกมรัน Die ไปหรือยัง (สมมติให้ดูจาก player.currentHealth < 0 ไม่ได้เพราะเป็น private)
        // แต่ถ้าผู้เล่นตายไปแล้ว ตัว player มักจะโดน Destroy หรือปิด Script
        if (!player.gameObject.activeInHierarchy || player.maxHealth == 0) // สมมติว่าตายแล้วหาย
        {
            EndMinigame(ai, false);
            return;
        }
        
        // กรณีที่ 2: ชนะ (ทนได้ครบ 5 วินาที)
        if (timer >= ai.minigameDuration)
        {
            EndMinigame(ai, true);
        }
    }

    private void EndMinigame(StalkerAI ai, bool playerSurvived)
    {
        minigameActive = false;
        Time.timeScale = 1f; // คืนเวลาให้โลกปกติ
        
        var minigameUI = ServiceLocator.Get<IMinigameService>();
        if (minigameUI != null) minigameUI.HideMinigameUI();

        if (playerSurvived)
        {
            Debug.Log("<color=cyan>✨ รอดแล้ว! STALKER วาร์ปหนีหายไปในเงามืด!</color>");
            ai.Awareness.ForceZeroAwareness(); 
            
            // วาร์ปหนีไปไกลๆ (สุ่มหาจุดที่อยู่ในบ้าน/เดินไปถึงได้ 10 รอบ)
            bool warpedSuccessfully = false;
            for (int i = 0; i < 15; i++)
            {
                Vector2 randCircle = Random.insideUnitCircle.normalized * ai.postEscapeTeleportRadius;
                Vector3 tryWarpPos = ai.PlayerTransform.position + new Vector3(randCircle.x, 0, randCircle.y);
                
                if (UnityEngine.AI.NavMesh.SamplePosition(tryWarpPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // ลองจำลองเดินไปจุดนั้นดู ถ้าไปได้ (PathComplete) แปลว่าอยู่ในบ้าน/พื้นที่เชื่อมต่อกัน ไม่ใช่นอกโลก
                    UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                    ai.Agent.CalculatePath(hit.position, path);
                    
                    if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        ai.Agent.Warp(hit.position);
                        warpedSuccessfully = true;
                        break;
                    }
                }
            }

            // ถ้าหาจุดวาร์ปดีๆ ไม่ได้เลย (วงแคบไป หรือบัคกำแพง) ให้วาร์ปไปหลบหลังมุมใกล้ๆ แทนการโยนออกนอกโลก
            if (!warpedSuccessfully)
            {
                Vector3 backupPos = ai.PlayerTransform.position - (ai.PlayerTransform.forward * 5f);
                if (UnityEngine.AI.NavMesh.SamplePosition(backupPos, out UnityEngine.AI.NavMeshHit backupHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    ai.Agent.Warp(backupHit.position);
                }
            }
            
            // กลับสู่สถานะย่องตาม
            ai.ChangeState(new StalkerStateLurk());
        }
        else
        {
            Debug.Log("<color=black>☠️ Game Over (มินิเกมจบเพราะเลือดหมด)</color>");
        }
    }

    public void ExitState(StalkerAI ai) 
    { 
        Time.timeScale = 1f; // เซฟตี้ เผื่อหลุดออกจาก State กลางคัน
        var minigameUI = ServiceLocator.Get<IMinigameService>();
        if (minigameUI != null) minigameUI.HideMinigameUI();
    }
}
