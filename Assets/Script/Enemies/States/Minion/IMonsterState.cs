using UnityEngine;

public interface IMonsterState
{
    void EnterState(PTSDMonsterAI ai);
    void UpdateState(PTSDMonsterAI ai);
    void ExitState(PTSDMonsterAI ai);
}
