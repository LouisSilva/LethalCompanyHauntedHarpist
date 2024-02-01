using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static LethalCompanyHarpGhost.HarpGhostPlugin;

namespace LethalCompanyHarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource mls;

    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;

    public Transform turnCompass;
    public Transform grabTarget;

    private NetworkObjectReference harpObjectRef;
    
    private float timeSinceHittingLocalPlayer;
    private float hearNoiseCooldown;
    [SerializeField] private float agentMaxAcceleration;
    [SerializeField] private float agentMaxSpeed;

    private int harpScrapValue;

    [SerializeField] private bool harpGhostDebug = true;

    private System.Random enemyRandom;

    private HarpBehaviour heldHarp;

    public override void Start()
    {
        base.Start();
        mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
        mls.LogInfo("Harp Ghost Spawned");

        if (!IsServer) return;
        timeSinceHittingLocalPlayer = 0;
        enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        agentMaxAcceleration = 200f;
        agentMaxSpeed = 3f;
        SpawnHarpServerRpc();
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) mls.LogError("NavMeshAgent component not found on " + name);
    }
    
    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        timeSinceHittingLocalPlayer += Time.deltaTime;
        hearNoiseCooldown -= Time.deltaTime;

        switch (currentBehaviourStateIndex)
        {
            case 0: // harp ghost playing music and chilling
            {
                if (previousBehaviourStateIndex != 0)
                {
                    agentMaxSpeed = 1f;
                    openDoorSpeedMultiplier = 0.6f;
                    previousBehaviourStateIndex = 0;
                    movingTowardsTargetPlayer = false;
                    LogDebug($"Harp Ghost '{gameObject.name}': Switched to behaviour state 0");
                }
                
                break;
            }

            case 1: // harp ghost is angry and trying to find player/s to attack
            {
                if (previousBehaviourStateIndex != 1)
                {   
                    agentMaxSpeed = 5f;
                    openDoorSpeedMultiplier = 1f;
                    previousBehaviourStateIndex = 1;
                    movingTowardsTargetPlayer = false;
                    DropHarpServerRpc(transform.position);
                    LogDebug($"Harp Ghost '{gameObject.name}': Switched to behaviour state 1");
                }
                
                break;
            }
        }
        
        CalculateAgentSpeed();
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }

            case 1:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                bool targetPlayerBool = TargetClosestPlayer(1.5f, true, 165F);
                
                if (targetPlayerBool)
                {
                    if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                    movingTowardsTargetPlayer = true;
                }
                else
                {
                    movingTowardsTargetPlayer = false;
                    if (!searchForPlayers.inProgress) StartSearch(transform.position, searchForPlayers);
                }

                break;
            }
        }
    }

    private void LogDebug(string logMessage)
    {
        if (harpGhostDebug) mls.LogInfo(logMessage);
    }

    private void CalculateAgentSpeed()
    {
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex >= 0)
        {
            MoveWithAcceleration();
        }
    }

    private void MoveWithAcceleration() {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }

    private void GrabHarp(GameObject harpObject)
    {
        heldHarp = harpObject.GetComponent<HarpBehaviour>();
        if (heldHarp == null) mls.LogError("Harp in GrabHarp function did not contain harpItem component");
        else
        {
            RoundManager.Instance.totalScrapValueInLevel += heldHarp.scrapValue;
            heldHarp.parentObject = grabTarget;
            heldHarp.isHeldByEnemy = true;
            heldHarp.grabbableToEnemies = false;
            heldHarp.grabbable = false;
            heldHarp.GrabItemFromEnemy(this);
            heldHarp.ItemActivate(true);
        }
    }

    private void DropHarp(Vector3 dropPosition)
    {
        if (heldHarp == null) return;
        heldHarp.parentObject = null;
        heldHarp.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        heldHarp.EnablePhysics(true);
        heldHarp.fallTime = 0.0f;
        heldHarp.startFallingPosition = heldHarp.transform.parent.InverseTransformPoint(heldHarp.transform.position);
        heldHarp.targetFloorPosition = heldHarp.transform.parent.InverseTransformPoint(dropPosition);
        heldHarp.floorYRot = -1;
        heldHarp.grabbable = true;
        heldHarp.grabbableToEnemies = true;
        heldHarp.isHeld = false;
        heldHarp.isHeldByEnemy = false;
        heldHarp.DiscardItemFromEnemy();
        heldHarp = null;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        enemyHP -= force;

        if (IsOwner)
        {
            if (enemyHP <= 0)
            {
                KillEnemyOnOwnerClient();
            }
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (timeSinceHittingLocalPlayer < 1f) {
            return;
        }
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        playerControllerB.DamagePlayer(10);
    }
    
    public override void DetectNoise(
        Vector3 noisePosition, 
        float noiseLoudness, 
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if ((double)stunNormalizedTimer > 0 || (double)hearNoiseCooldown > 0 || currentBehaviourStateIndex == 1) return;
        hearNoiseCooldown = 0.03f;

        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
        float noiseThreshold = 5f * noiseLoudness;
        LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise threshold: {noiseThreshold}");

        if (Physics.Linecast(transform.position, noisePosition, 256))
        {
            noiseLoudness /= 2f;
            noiseThreshold /= 2f;
        }

        if (noiseLoudness < 0.25) return;
        if (currentBehaviourStateIndex != 1 && distanceToNoise < noiseThreshold)
        {
            SwitchToBehaviourState(1);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropHarpServerRpc(Vector3 dropPosition)
    {
        DropHarpClientRpc(dropPosition);
    }

    [ClientRpc]
    public void DropHarpClientRpc(Vector3 dropPosition)
    {
        DropHarp(dropPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnHarpServerRpc()
    {
        harpScrapValue = 150;
        GameObject harpObject = Instantiate(
            harpItem.spawnPrefab, 
            transform.position, 
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer
            );
        
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.AddComponent<ScanNodeProperties>().scrapValue = harpScrapValue;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, harpScrapValue);
        GrabHarp(harpObject.gameObject);
    }

    [ClientRpc]
    public void SpawnHarpClientRpc(NetworkObjectReference harpObject, int harpValue)
    {
        harpScrapValue = harpValue;
        harpObjectRef = harpObject;
    }
}

/*
 TODO:

1). Add animations
2). Make harp play music
3). Make ghost able to open doors
4). Make ghost able to change elevation
5). Add tooltip for harp
 
 */
