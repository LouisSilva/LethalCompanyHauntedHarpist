using GameNetcodeStuff;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public interface IEscortee
{
    // This is used by an escort to tell the escortee that they have been hurt/stunned
    public void EscorteeBreakoff();
    public void EscorteeBreakoff(PlayerControllerB targetPlayer);
}