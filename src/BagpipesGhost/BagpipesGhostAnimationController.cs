using BepInEx.Logging;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAnimationController : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    #pragma warning disable 0649
    [SerializeField] private Animator animator;
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    [SerializeField] private BagpipesGhostAudioManager audioManager;
    
    public VisualEffect teleportVfx;
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
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipe Ghost Animation Controller {_ghostId}");
        
        animator = GetComponent<Animator>();
        if (animator == null) _mls.LogError("Animator is null");

        netcodeController = GetComponent<BagpipesGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("netcodeController is null");

        audioManager = GetComponent<BagpipesGhostAudioManager>();
        if (audioManager == null) _mls.LogError("audioManager is null");
    }

    private void OnEnable()
    {
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
        netcodeController.OnPlayTeleportVfx += HandlePlayTeleportVfx;
    }

    private void OnDestroy()
    {
        if (netcodeController == null) return;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnPlayTeleportVfx -= HandlePlayTeleportVfx;
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
        _attackDamage = HarpGhostConfig.Instance.HarpGhostAttackDamage.Value;
    }
    
    private void HandlePlayTeleportVfx(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        teleportVfx.SendEvent("OnPlayTeleport");
    }

    private void SetBool(string recievedGhostId, int parameter, bool value)
    {
        if (_ghostId != recievedGhostId) return;
        animator.SetBool(parameter, value);
    }

    public bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(string recievedGhostId, int parameter)
    {
        if (_ghostId != recievedGhostId) return;
        animator.SetTrigger(parameter);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}