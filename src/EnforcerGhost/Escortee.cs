using GameNetcodeStuff;

namespace LethalCompanyHarpGhost.EnforcerGhost;

internal interface IEscortee
{
    // This is used by an escort to tell the escortee that they have been hurt/stunned
    internal void EscorteeBreakoff(PlayerControllerB targetPlayer = null);
}