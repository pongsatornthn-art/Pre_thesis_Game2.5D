using UnityEngine;

public class StalkerStateScream : IStalkerState
{
    private float screamTimer;
    private float screamDuration = 3f;

    public void EnterState(StalkerAI ai)
    {
        screamDuration = ai.screamSeconds;   // [2026-11-24] เดิม 3 วิ (+ คำรามอีก 3 วิในโหมดไล่) → ผู้เล่นหนีไปไกลแล้ว
        ai.lastContactTime = Time.time;
        if (ai.PlayerTransform != null) ai.LastKnownPlayerPos = ai.PlayerTransform.position;
        if (ai.Agent.isOnNavMesh) ai.Agent.isStopped = true;
        
        // Show UI Text
        ai.ShowDebugText("STALKER_SCREAM", Color.red);
        
        // Play Audio
        var audioService = ServiceLocator.Get<IAudioService>();
        if (audioService != null && ai.screamClip != null)
        {
            audioService.PlaySFX(ai.screamClip, ai.transform.position, ai.screamVolume, true);
        }
        else if (audioService == null)
        {
            Debug.LogWarning("StalkerStateScream: IAudioService not found!");
        }
        else if (ai.screamClip == null)
        {
            Debug.LogWarning("StalkerStateScream: Scream AudioClip is missing on StalkerAI!");
        }

        Debug.Log($"<color=red>🦇 STALKER: *SCREAMMMM* ({screamDuration} วิ ก่อนไล่)</color>");
        
        // TODO: Play scream animation here if available
        // if (ai.animator != null) ai.animator.SetTrigger("Scream");

        screamTimer = 0f;
    }

    public void UpdateState(StalkerAI ai)
    {
        screamTimer += Time.deltaTime;
        
        // Look at player while screaming
        if (ai.PlayerTransform != null)
        {
            Vector3 dir = (ai.PlayerTransform.position - ai.transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                ai.transform.rotation = Quaternion.Slerp(ai.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            }
        }

        if (screamTimer >= screamDuration)
        {
            ai.ChangeState(new StalkerStateChase());
        }
    }

    public void ExitState(StalkerAI ai)
    {
        ai.ShowDebugText("", Color.white);
    }
}
