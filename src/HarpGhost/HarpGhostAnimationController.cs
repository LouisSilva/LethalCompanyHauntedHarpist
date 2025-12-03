using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAnimationController : MonoBehaviour
{
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
        animator = GetComponent<Animator>();
        if (!animator) HarpGhostPlugin.Logger.LogError("Animator is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (!netcodeController) HarpGhostPlugin.Logger.LogError("netcodeController is null");

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (!audioManager) HarpGhostPlugin.Logger.LogError("audioManager is null");
    }

    private void OnEnable()
    {
        if (!netcodeController) return;
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
    }

    private void OnDisable()
    {
        if (!netcodeController) return;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
    }

    private void HandleOnEnterDeathState()
    {
        SetBool(IsDead, true);
        SetBool(IsRunning, false);
        SetBool(IsStunned, false);
        Destroy(this);
    }

    private void HandleInitializeConfigValues()
    {
        _attackDamage = HarpGhostConfig.Instance.HarpGhostAttackDamage.Value;
    }

    private void SetBool(int parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }

    public void OnAnimationEventFixAgentSpeedAfterAttack()
    {
        if (NetworkManager.Singleton.IsClient && netcodeController.IsOwner)
        {
            netcodeController.FixAgentSpeedAfterAttackServerRpc();
        }
    }

    public void OnAnimationEventAttackShiftComplete()
    {
        if (!NetworkManager.Singleton.IsClient || !netcodeController.IsOwner) return;

        netcodeController.ChangeAgentMaxSpeedServerRpc(0f, 0f); // Ghost is frozen while doing the second attack anim
        netcodeController.PlayCreatureVoiceClientRpc((int)HarpGhostAudioManager.AudioClipTypes.Laugh, audioManager.laughSfx.Length);
        StartCoroutine(DamageTargetPlayerAfterDelay(0.05f, _attackDamage, CauseOfDeath.Strangulation));
    }

    private IEnumerator DamageTargetPlayerAfterDelay(float delay, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        netcodeController.DamageTargetPlayerServerRpc(damage, causeOfDeath);
    }
}