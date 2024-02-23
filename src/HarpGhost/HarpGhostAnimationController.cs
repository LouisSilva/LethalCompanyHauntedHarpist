using System.Collections;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAnimationController : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    #pragma warning disable 0649
    [SerializeField] private Animator animator;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAudioManager audioManager;
    #pragma warning restore 0649
    
    public static readonly int IsRunning = Animator.StringToHash("isRunning");
    public static readonly int IsStunned = Animator.StringToHash("isStunned");
    public static readonly int IsDead = Animator.StringToHash("isDead");
    public static readonly int Death = Animator.StringToHash("death");
    public static readonly int Stunned = Animator.StringToHash("stunned");
    public static readonly int Recover = Animator.StringToHash("recover");
    public static readonly int Attack = Animator.StringToHash("attack");

    private int _attackDamage = 35;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Animation Controller");
        
        animator = GetComponent<Animator>();
        if (animator == null) _mls.LogError("Animator is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("netcodeController is null");

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("audioManager is null");
    }

    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }

    private void OnEnable()
    {
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDestroy()
    {
        if (netcodeController == null) return;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
    }

    private void HandleOnEnterDeathState(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        SetTrigger(_ghostId, Death);
        SetBool(_ghostId, IsDead, true);
        SetBool(_ghostId, IsRunning, false);
        SetBool(_ghostId, IsStunned, false);
        Destroy(this);
    }

    private void HandleInitializeConfigValues(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        _attackDamage = HarpGhostConfig.Instance.GhostAttackDamage.Value;
    }

    private void SetBool(string recievedGhostId, int parameter, bool value)
    {
        if (_ghostId != recievedGhostId) return;
        LogDebug($"SetBoolCalled: {parameter}, {value}");
        animator.SetBool(parameter, value);
    }

    public bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(string recievedGhostId, int parameter)
    {
        if (_ghostId != recievedGhostId) return;
        LogDebug($"SetTriggerCalled: {parameter}");
        animator.SetTrigger(parameter);
    }
    
    public void OnAnimationEventFixAgentSpeedAfterAttack() // Is called by an animation event
    {
        if (NetworkManager.Singleton.IsClient && netcodeController.IsOwner)
        {
            netcodeController.FixAgentSpeedAfterAttackServerRpc(_ghostId);
        }
    }
    
    public void OnAnimationEventAttackShiftComplete() // Is called by an animation event
    {
        if (!NetworkManager.Singleton.IsClient || !netcodeController.IsOwner) return;
        
        netcodeController.ChangeAgentMaxSpeedServerRpc(_ghostId, 0f, 0f); // Ghost is frozen while doing the second attack anim
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Laugh, audioManager.laughSfx.Length);
        LogDebug("OnAnimationEventAttackShiftComplete() called");
        StartCoroutine(DamageTargetPlayerAfterDelay(0.05f, _attackDamage, CauseOfDeath.Strangulation));
    }
    
    private IEnumerator DamageTargetPlayerAfterDelay(float delay, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        LogDebug("Damaging player in anim controller");
        netcodeController.DamageTargetPlayerServerRpc(_ghostId, damage, causeOfDeath);
    }
}