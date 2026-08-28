using UnityEngine;

public class StalkerStateScream : IStalkerState
{
    private float screamTimer;
    private float screamDuration = 3f;

    public void EnterState(StalkerAI ai)
    {
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

        Debug.Log("<color=red>🦇 STALKER: *SCREAMMMM* (3 seconds before chase)</color>");
        
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
