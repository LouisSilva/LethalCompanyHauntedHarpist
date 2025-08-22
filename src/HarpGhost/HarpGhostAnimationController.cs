using BepInEx.Logging;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

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
    
    internal static readonly int IsRunning = Animator.StringToHash("Running");
    internal static readonly int IsStunned = Animator.StringToHash("Stunned");
    internal static readonly int IsDead = Animator.StringToHash("Dead");
    internal static readonly int Attack = Animator.StringToHash("Attack");

    private int _attackDamage = 35;

    private void Start()
    {
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Animation Controller {_ghostId}");
        
        animator = GetComponent<Animator>();
        if (!animator) _mls.LogError("Animator is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (!netcodeController) _mls.LogError("netcodeController is null");

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (!audioManager) _mls.LogError("audioManager is null");
    }

    private void OnEnable()
    {
        if (!netcodeController) return;
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDisable()
    {
        if (!netcodeController) return;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void HandleUpdateGhostIdentifier(string receivedGhostId)
    {
        _ghostId = receivedGhostId;
    }

    private void HandleOnEnterDeathState(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        SetBool(_ghostId, IsDead, true);
        SetBool(_ghostId, IsRunning, false);
        SetBool(_ghostId, IsStunned, false);
        Destroy(this);
    }

    private void HandleInitializeConfigValues(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        _attackDamage = HarpGhostConfig.Instance.HarpGhostAttackDamage.Value;
    }

    private void SetBool(string receivedGhostId, int parameter, bool value)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetBool(parameter, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(string receivedGhostId, int parameter)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetTrigger(parameter);
    }
    
    public void OnAnimationEventFixAgentSpeedAfterAttack()
    {
        if (NetworkManager.Singleton.IsClient && netcodeController.IsOwner)
        {
            netcodeController.FixAgentSpeedAfterAttackServerRpc(_ghostId);
        }
    }
    
    public void OnAnimationEventAttackShiftComplete()
    {
        if (!NetworkManager.Singleton.IsClient || !netcodeController.IsOwner) return;
        
        netcodeController.ChangeAgentMaxSpeedServerRpc(_ghostId, 0f, 0f); // Ghost is frozen while doing the second attack anim
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Laugh, audioManager.laughSfx.Length);
        StartCoroutine(DamageTargetPlayerAfterDelay(0.05f, _attackDamage, CauseOfDeath.Strangulation));
    }
    
    private IEnumerator DamageTargetPlayerAfterDelay(float delay, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        netcodeController.DamageTargetPlayerServerRpc(_ghostId, damage, causeOfDeath);
    }
}