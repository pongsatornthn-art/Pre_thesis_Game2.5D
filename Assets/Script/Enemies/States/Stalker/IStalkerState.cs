using UnityEngine;

public interface IStalkerState
{
    void EnterState(StalkerAI ai);
    void UpdateState(StalkerAI ai);
    void ExitState(StalkerAI ai);
}
