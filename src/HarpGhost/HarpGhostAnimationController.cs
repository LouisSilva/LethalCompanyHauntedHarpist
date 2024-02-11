using BepInEx.Logging;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAnimationController : MonoBehaviour
{
    private ManualLogSource _mls;
    
    [SerializeField] private Animator animator;

    private const bool HarpGhostAnimConDebug = true;

    private void Awake()
    {
        _mls = new ManualLogSource("HarpGhostAnimationController");
        _mls.LogInfo("Animation Controller loaded");
    }

    private void LogDebug(string msg)
    {
        if (HarpGhostAnimConDebug) _mls.LogInfo(msg);
    }

    public void SetBool(string parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    public void SetBool(int parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    public void GetBool(string parameter)
    {
        animator.GetBool(parameter);
    }

    public void GetBool(int parameter)
    {
        animator.GetBool(parameter);
    }

    public void SetTrigger(string parameter)
    {
        animator.SetTrigger(parameter);
    }

    public void SetTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }
}