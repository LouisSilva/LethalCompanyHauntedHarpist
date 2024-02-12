using System.Collections;
using BepInEx.Logging;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAnimationController : MonoBehaviour
{
    private ManualLogSource _mls;
    
    #pragma warning disable 0649
    [SerializeField] private Animator animator;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private HarpGhostAI harpGhostAI;
    #pragma warning restore 0649
    
    public readonly int IsRunning = Animator.StringToHash("isRunning");
    public readonly int IsStunned = Animator.StringToHash("isStunned");
    public readonly int IsDead = Animator.StringToHash("isDead");
    public readonly int Death = Animator.StringToHash("death");
    public readonly int Stunned = Animator.StringToHash("stunned");
    public readonly int Recover = Animator.StringToHash("recover");
    public readonly int Attack = Animator.StringToHash("attack");

    [SerializeField] private bool harpGhostAnimationControllerDebug = true;

    private void Awake()
    {
        _mls = new ManualLogSource("HarpGhostAnimationController");
    }

    private void Start()
    {
        SubscribeToEvents();

        animator = GetComponent<Animator>();
        if (animator == null) _mls.LogError("Animator is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("netcodeController is null");

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("audioManager is null");

        harpGhostAI = GetComponent<HarpGhostAI>();
        if (harpGhostAI == null) _mls.LogError("harpGhostAi is null");
    }

    private void LogDebug(string msg)
    {
        if (harpGhostAnimationControllerDebug) _mls.LogInfo(msg);
    }

    private void SubscribeToEvents()
    {
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
    }

    public void Init()
    {
        ChangeAnimationParameterBool(IsDead, false);
        ChangeAnimationParameterBool(IsRunning, false);
        ChangeAnimationParameterBool(IsStunned, false);
    }

    public void EnterDeathState()
    {
        DoAnimation(Death);
        ChangeAnimationParameterBool(IsDead, true);
        ChangeAnimationParameterBool(IsRunning, false);
        ChangeAnimationParameterBool(IsStunned, false);
    }

    private void SetBool(int parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    public bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }
    
    public void IncreaseAccelerationGallop() // Is called by an animation event
    {
        netcodeController.ChangeAgentMaxAccelerationServerRpc(harpGhostAI.AgentMaxAcceleration*4);
    }

    public void DecreaseAccelerationGallop() // Is called by an animation event
    {
        netcodeController.ChangeAgentMaxAccelerationServerRpc(harpGhostAI.AgentMaxAcceleration/4);
    }
    
    public void FixAgentSpeedAfterAttack() // Is called by an animation event
    {
        LogDebug("FixAgentSpeedAfterAttack() called");
        float newMaxSpeed, newMaxSpeed2;
        switch (harpGhostAI.CurrentBehaviourStateIndex)
        {
            case 0:
                newMaxSpeed = 0.3f;
                newMaxSpeed2 = 0.3f;
                break;
            case 1:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
            case 2:
                newMaxSpeed = 6f;
                newMaxSpeed2 = 1f;
                break;
            case 3:
                newMaxSpeed = 9f;
                newMaxSpeed2 = 1f;
                break;
            default:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
        }
        
        netcodeController.ChangeAgentMaxSpeedServerRpc(newMaxSpeed, newMaxSpeed2);
    }
    
    public void AttackShiftComplete() // Is called by an animation event
    {
        LogDebug("AttackShiftComplete called");
        netcodeController.ChangeAgentMaxSpeedServerRpc(0f, 0f); // Ghost is frozen while doing the second attack anim
        audioManager.PlayCreatureVoice((int)HarpGhostAudioManager.AudioClipTypes.Laugh, audioManager.laughSfx.Length);
        if (netcodeController.IsServer) StartCoroutine(DamagePlayerAfterDelay(0.05f));
    }
    
    private IEnumerator DamagePlayerAfterDelay(float delay) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        if (harpGhostAI.TargetPlayer == null) yield break;
        harpGhostAI.DamageTargetPlayer(35, CauseOfDeath.Strangulation);
    }
    
    public void StunnedAnimationFreeze() // called by an animation event
    {
        LogDebug("StunnedAnimationFreeze() called");
        if (netcodeController.IsServer) StartCoroutine(WaitUntilStunComplete());
    }

    private IEnumerator WaitUntilStunComplete()
    {
        LogDebug("WaitUntilStunComplete() called");
        while (harpGhostAI.StunNormalizedTimer > 0) yield return new WaitForSeconds(0.02f);
        if (GetBool(IsDead)) yield break; // Cancels the stun recover animation if the ghost is dead
        DoAnimation(Recover);
    }

    public void DoAnimation(int animationId) // This is what classes use to do animation rpcs
    {
        netcodeController.DoAnimationServerRpc(animationId);
    }

    public void ChangeAnimationParameterBool(int animationId, bool value) // This is what classes use to change animation paramters rpcs
    {
        if (GetBool(animationId) == value) return;
        netcodeController.ChangeAnimationParameterBoolServerRpc(animationId, value);
    }
}