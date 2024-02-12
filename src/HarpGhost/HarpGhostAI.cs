using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static LethalCompanyHarpGhost.HarpGhostPlugin;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource _mls;

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.3f;
    [SerializeField] private float annoyanceLevel = 0f;
    [SerializeField] private float annoyanceDecayRate = 0.3f;
    [SerializeField] private float annoyanceThreshold = 8f;
    [SerializeField] private float maxSearchRadius = 100f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;
    private float _hearNoiseCooldown = 0f;
    
    private int _harpScrapValue = 300;
    
    private bool _hasBegunInvestigating = false;
    
    private Vector3 _targetPlayerLastSeenPos = default;
    private Vector3 _agentLastPosition = default;

    [Header("Transforms")]
    [Space(3f)]
    public Transform grabTarget;
    public BoxCollider attackArea;
    
    private NetworkObjectReference _harpObjectRef;

    [SerializeField] private bool harpGhostDebug = true;

    private HarpBehaviour _heldHarp;

    [Header("Controllers and Managers")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private HarpGhostAnimationController animationController;
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    #pragma warning restore 0649
    
    public void Awake()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
    }

    public override void Start()
    {
        base.Start();
        
        if (!IsServer) return;
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);

        animationController = GetComponent<HarpGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        animationController.Init();
        SubscribeToEvents();
        
        netcodeController.SpawnHarpServerRpc();
        netcodeController.GrabHarpServerRpc();
        if (IsServer) StartCoroutine(DelayedHarpMusicActivate()); // Needed otherwise the music won't play, maybe because multiple things are fighting for the heldHarp object
        _mls.LogInfo("Harp Ghost Spawned");
    }

    private void SubscribeToEvents()
    {
        netcodeController.OnTargetPlayerLastSeenPosUpdated += HandleTargetPlayerLastSeenPosUpdated;
        netcodeController.OnSwitchBehaviourState += SwitchBehaviourStateLocally;
        netcodeController.OnBeginChasingPlayer += BeginChasingPlayer;
        netcodeController.OnDropHarp += DropHarp;
        netcodeController.OnSpawnHarp += HandleSpawnHarp;
        netcodeController.OnGrapHarp += GrabHarpIfNotHolding;
        netcodeController.OnChangeAgentMaxAcceleration += HandleChangeAgentMaxAcceleration;
        netcodeController.OnChangeAgentMaxSpeed += HandleChangeAgentMaxSpeed;
        netcodeController.OnEnterDeathState += EnterDeathState;
    }

    public void FixedUpdate()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed,
            (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
        {
            animationController.ChangeAnimationParameterBool(animationController.IsRunning, false);
            return;
        }

        _timeSinceHittingLocalPlayer += Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;

        animationController.ChangeAnimationParameterBool(animationController.IsStunned, stunNormalizedTimer > 0);

        CalculateAgentSpeed();

        switch (currentBehaviourStateIndex)
        {
            case 0: // harp ghost playing music and chilling
            {
                if (annoyanceLevel > 0)
                {
                    annoyanceLevel -= annoyanceDecayRate * Time.deltaTime;
                    annoyanceLevel = Mathf.Clamp(annoyanceLevel, 0, Mathf.Infinity);
                }

                if (annoyanceLevel >= annoyanceThreshold)
                {
                    if (stunNormalizedTimer > 0) break;
                    netcodeController.SwitchBehaviourStateServerRpc(1);
                }

                break;
            }

            case 1: // harp ghost is angry and trying to find players to attack
            {
                if (stunNormalizedTimer > 0) break;
                animationController.ChangeAnimationParameterBool(animationController.IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 2: // ghost is investigating last seen player pos
            {
                if (stunNormalizedTimer > 0) break;
                animationController.ChangeAnimationParameterBool(animationController.IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 3: // ghost is chasing player
            {
                if (stunNormalizedTimer > 0) break;
                animationController.ChangeAnimationParameterBool(animationController.IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 4: // ghost is dead
            {
                break;
            }
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (StartOfRound.Instance.allPlayersDead)
        {
            if (roamMap.inProgress) StopSearch(roamMap);
            if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
            return;
        }

        if (stunNormalizedTimer > 0) return;

        switch (currentBehaviourStateIndex)
        {
            case 0: // playing music state
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }

            case 1: // searching for player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                LogDebug($"Temp player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    netcodeController.SwitchBehaviourStateServerRpc(3);
                    break;
                }
                
                if (!searchForPlayers.inProgress)
                {
                    searchForPlayers.searchWidth = 30f;
                    StartSearch(transform.position, searchForPlayers);
                    break;
                }
                
                break;
            }

            case 2: // investigating last seen player position state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!IsOwner) break;

                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                LogDebug($"Temp target player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    netcodeController.SwitchBehaviourStateServerRpc(3);
                    break;
                }
                
                if (!_hasBegunInvestigating)
                {
                    if (_targetPlayerLastSeenPos == default) netcodeController.SwitchBehaviourStateServerRpc(1);
                    else
                    {
                        if (!SetDestinationToPosition(_targetPlayerLastSeenPos, true))
                        {
                            netcodeController.SwitchBehaviourStateServerRpc(1);
                            break;
                        }
                    }
                    _hasBegunInvestigating = true;
                }

                if (Vector3.Distance(transform.position, _targetPlayerLastSeenPos) <= 1)
                {
                    netcodeController.SwitchBehaviourStateServerRpc(1);
                    break;
                }
                
                break;
            }

            case 3: // chasing player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!IsOwner) return;
                
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(100f, 80, eye, 2f,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                bool ourTargetFound = false;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    LogDebug($"playersinlos: {playersInLineOfSight.Length}");
                    ourTargetFound = targetPlayer != null && playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                }
                
                else
                {
                    LogDebug("No players found, switching to state 2");
                    netcodeController.SwitchBehaviourStateServerRpc(2);
                    break;
                }

                LogDebug($"Our target found: {ourTargetFound}");
                if (!ourTargetFound)
                {
                    PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                    if (playerControllerB == null)
                    {
                        LogDebug("No players found for second time, switching to state 2");
                        netcodeController.SwitchBehaviourStateServerRpc(2);
                        break;
                    }
                    
                    LogDebug("New target found, beginning the chase");
                    netcodeController.BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
                }
                
                if (_targetPlayerLastSeenPos != targetPlayer.transform.position) netcodeController.UpdateTargetPlayerLastSeenPosServerRpc(targetPlayer.transform.position);
                PlayerChasingFearIncrease();
                
                // Check if a player is in attack area and attack
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 7) AttackPlayerIfClose();
                break;
            }

            case 4: // dead state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                break;
            }
        }
    }

    private void LogDebug(string logMessage)
    {
        if (harpGhostDebug) _mls.LogInfo(logMessage);
    }

    private void BeginChasingPlayer(int targetPlayerObjectId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        SetMovingTowardsTargetPlayer(player);
        ChangeOwnershipOfEnemy(player.actualClientId);
        LogDebug($"Now chasing {targetPlayer.name}");
    }

    public void SwitchBehaviourStateLocally(int state)
    {
        switch (state)
        {
            case 0: // playing music state
            {
                LogDebug("Switched to behaviour state 0!");
                
                targetPlayer = null;
                _targetPlayerLastSeenPos = default;
                agentMaxSpeed = 0.3f;
                agentMaxAcceleration = 200f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 4;
                
                break; 
            }

            case 1: // searching for player state
            {
                LogDebug("Switched to behaviour state 1!");
                
                agentMaxSpeed = 3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 2;
                
                netcodeController.DropHarpServerRpc(transform.position);
                
                break;
            }

            case 2:
            {
                LogDebug("Switched to behaviour state 2!");
                
                agentMaxSpeed = 6f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 1;
                
                if (_heldHarp != null) netcodeController.DropHarpServerRpc(transform.position);
                
                break;
            }

            case 3:
            {
                LogDebug("Switched to behaviour state 3!");
                
                agentMaxSpeed = 9f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = true;
                _hasBegunInvestigating = false;
                _targetPlayerLastSeenPos = default;
                openDoorSpeedMultiplier = 0.6f;
                
                if (_heldHarp != null) netcodeController.DropHarpServerRpc(transform.position);
                
                break;
            }

            case 4:
            {
                LogDebug("Switched to behaviour state 4!");
                
                agentMaxSpeed = 0;
                agentMaxAcceleration = 0;
                movingTowardsTargetPlayer = false;
                targetPlayer = null;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;
                _hasBegunInvestigating = false;
                
                netcodeController.DropHarpServerRpc(transform.position);
                animationController.ChangeAnimationParameterBool(animationController.IsDead, true);
                
                break;
            }
               
        }
        
        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
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

    private void GrabHarpIfNotHolding()
    {
        if (_heldHarp != null) return;
        if (!_harpObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldHarp = networkObject.gameObject.GetComponent<HarpBehaviour>();
        GrabHarp(_heldHarp.gameObject);
    }

    private void GrabHarp(GameObject harpObject)
    {
        _heldHarp = harpObject.GetComponent<HarpBehaviour>();
        if (_heldHarp == null)
        {
            _mls.LogError("Harp in GrabHarp function did not contain harpItem component");
            return;
        }
        
        _heldHarp.parentObject = grabTarget;
        _heldHarp.isHeldByEnemy = true;
        _heldHarp.grabbableToEnemies = false;
        _heldHarp.grabbable = false;
        _heldHarp.GrabItemFromEnemy(this);
    }

    private void DropHarp(Vector3 dropPosition)
    {
        _heldHarp.parentObject = null;
        _heldHarp.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldHarp.EnablePhysics(true);
        _heldHarp.fallTime = 0.0f;
        Transform parent;
        _heldHarp.startFallingPosition = (parent = _heldHarp.transform.parent).InverseTransformPoint(_heldHarp.transform.position);
        _heldHarp.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldHarp.floorYRot = -1;
        _heldHarp.grabbable = true;
        _heldHarp.grabbableToEnemies = true;
        _heldHarp.isHeld = false;
        _heldHarp.isHeldByEnemy = false;
        _heldHarp.ItemActivate(false);
        _heldHarp.DiscardItemFromEnemy();
        _heldHarp = null;
    }

    private IEnumerator DelayedHarpMusicActivate()
    {
        yield return new WaitForSeconds(0.5f);
        _heldHarp.ItemActivate(true);
    }

    private void PlayerChasingFearIncrease()
    {
        if (GameNetworkManager.Instance.localPlayerController.isPlayerDead ||
            !GameNetworkManager.Instance.localPlayerController.isInsideFactory ||
            currentBehaviourStateIndex != 3 ||
            targetPlayer != GameNetworkManager.Instance.localPlayerController ||
            !IsOwner) return;
        
        LogDebug($"Increasing fear level for {GameNetworkManager.Instance.localPlayerController.name}, IsOwner?: {IsOwner}");
        if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(eye.position, 100f, 50, 3f))
        {
            LogDebug("Player to add fear to is looking at the ghost");
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1);
            GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 7)
        {
            LogDebug("Player to add fear to is not looking at the ghost, but is near");
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.6f);
            GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.4f);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (isEnemyDead) return;
        enemyHP -= force;
        
        netcodeController.DropHarpServerRpc(transform.position);
        if (enemyHP > 0)
        {
            audioManager.PlayCreatureVoice((int)HarpGhostAudioManager.AudioClipTypes.Damage, audioManager.damageSfx.Length);
            if (playerWhoHit != null)
            {
                LogDebug($"Player {playerWhoHit.name} hit ghost");
                netcodeController.BeginChasingPlayerServerRpc((int)playerWhoHit.playerClientId);
                netcodeController.SwitchBehaviourStateServerRpc(3);
            }

            else
            {
                LogDebug("Unknown player hit ghost");
                netcodeController.SwitchBehaviourStateServerRpc(1);
            }
            
            return;
        }
        
        // Ghost is dead
        LogDebug("Ghost is dead!");
        if (!IsOwner) return;
        netcodeController.EnterDeathStateServerRpc();
        KillEnemyOnOwnerClient();
    }

    private void EnterDeathState()
    {
        LogDebug("EnterDeathState() called");
        creatureVoice.Stop();
        creatureSFX.Stop();
        audioManager.PlayCreatureVoice((int)HarpGhostAudioManager.AudioClipTypes.Death, 1);

        animationController.EnterDeathState();
        
        SwitchBehaviourStateLocally(4);
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        LogDebug("SetEnemyStunned called");
        
        audioManager.PlayCreatureVoice((int)HarpGhostAudioManager.AudioClipTypes.Stun, audioManager.stunSfx.Length);
        netcodeController.DropHarpServerRpc(transform.position);
        animationController.ChangeAnimationParameterBool(animationController.IsStunned, true);
        animationController.DoAnimation(animationController.Stunned);

        if (setStunnedByPlayer != null)
        {
            LogDebug($"Player {setStunnedByPlayer.name} stunned ghost");
            netcodeController.BeginChasingPlayerServerRpc((int)setStunnedByPlayer.playerClientId);
            netcodeController.SwitchBehaviourStateServerRpc(3);
        }

        else
        {
            LogDebug("Unknown player stunned ghost");
            netcodeController.SwitchBehaviourStateServerRpc(1);
        }
    }

    private void AttackPlayerIfClose() // Checks if the player is in the ghost's attack area and if so, attacks
    {
        if (currentBehaviourStateIndex != 3 || 
            _timeSinceHittingLocalPlayer < 2f || 
            animationController.GetBool(animationController.IsStunned)) return;
        
        LogDebug("AttackPlayerIfClose() called");
        Collider[] hitColliders = Physics.OverlapBox(attackArea.transform.position, attackArea.size * 0.5f, Quaternion.identity, 1 << 3);

        if (hitColliders.Length <= 0) return;
        foreach (Collider player in hitColliders)
        {
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(player);
            if (playerControllerB == null) continue;
            
            LogDebug($"Attacking player {playerControllerB.name}");
            if (playerControllerB != targetPlayer) netcodeController.BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
            _timeSinceHittingLocalPlayer = 0f;
            animationController.DoAnimation(animationController.Attack);
            break;
        }
    }
    
    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        /*
        if (currentBehaviourStateIndex != 3 || timeSinceHittingLocalPlayer < 1.2f) return;
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        agentMaxSpeed = 0f;
        DoAnimationServerRpc(Attack);
        */
    }

    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (searchForPlayers.inProgress)
            searchForPlayers.searchWidth = Mathf.Clamp(searchForPlayers.searchWidth + 10f, 1f, maxSearchRadius);
    }

    public void DamageTargetPlayer(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        LogDebug("Damaging player!");
        targetPlayer.DamagePlayer(35, causeOfDeath: causeOfDeath);
    }
    
    public override void DetectNoise(
        Vector3 noisePosition, 
        float noiseLoudness, 
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown >= 0.0 || currentBehaviourStateIndex != 0 || Enum.IsDefined(typeof(HarpGhostAudioManager.NoiseIDToIgnore), noiseID)) return;
        _hearNoiseCooldown = 0.02f;

        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
        float noiseThreshold = 8f * noiseLoudness;
        LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

        if (Physics.Linecast(transform.position, noisePosition, 256))
        {
            noiseLoudness /= 2f;
            noiseThreshold /= 2f;
        }

        if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
        if (noiseID is (int)HarpGhostAudioManager.NoiseIds.Boombox or (int)HarpGhostAudioManager.NoiseIds.PlayersTalking or (int)HarpGhostAudioManager.NoiseIds.RadarBoosterPing)
            noiseLoudness *= 2;
        annoyanceLevel += noiseLoudness;

        if (annoyanceLevel > annoyanceThreshold)
        {
            LogDebug("In detectnoise(), ghost is sufficciently annoyed and now going to state 1");
            netcodeController.SwitchBehaviourStateServerRpc(1);
        }
        LogDebug($"Harp Ghost annoyance level: {annoyanceLevel}");
    }

    private void HandleTargetPlayerLastSeenPosUpdated(Vector3 pos)
    {
        _targetPlayerLastSeenPos = pos;
    }

    private void HandleChangeAgentMaxAcceleration(float newAcceleration)
    {
        agentMaxAcceleration = newAcceleration;
    }

    private void HandleChangeAgentMaxSpeed(float newMaxSpeed, float newMaxSpeed2)
    {
        agentMaxSpeed = newMaxSpeed;
        agent.speed = newMaxSpeed2;
    }

    private void HandleSpawnHarp(NetworkObjectReference harpObject, int harpScrapValue)
    {
        _harpObjectRef = harpObject;
        _harpScrapValue = harpScrapValue;
    }

    // [ServerRpc(RequireOwnership = false)]
    // public void SpawnHarpServerRpc()
    // { 
    //     GameObject harpObject = Instantiate(
    //         HarpItem.spawnPrefab,
    //         transform.position,
    //         Quaternion.identity,
    //         RoundManager.Instance.spawnedScrapContainer);
    //
    //     AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
    //     if (harpGhostDebug)
    //     {
    //         if (harpAudioSource == null)
    //         {
    //             harpAudioSource = harpObject.AddComponent<AudioSource>();
    //             harpAudioSource.playOnAwake = false;
    //             harpAudioSource.loop = true;
    //             harpAudioSource.spatialBlend = 1;
    //             _mls.LogError("Harp audio source is null");
    //         }
    //     }
    //     
    //     HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
    //     harpBehaviour.harpAudioSource = harpAudioSource;
    //     harpBehaviour.harpAudioClips = harpAudioClips;
    //     
    //     _harpScrapValue = UnityEngine.Random.Range(150, 301);
    //     harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
    //     harpObject.GetComponent<GrabbableObject>().SetScrapValue(_harpScrapValue);
    //     RoundManager.Instance.totalScrapValueInLevel += _harpScrapValue;
    //     
    //     harpObject.GetComponent<NetworkObject>().Spawn();
    //     SpawnHarpClientRpc(harpObject, _harpScrapValue);
    // }

    // [ClientRpc]
    // public void SpawnHarpClientRpc(NetworkObjectReference harpObject, int recievedHarpScrapValue)
    // {
    //     _harpScrapValue = recievedHarpScrapValue;
    //     _harpObjectRef = harpObject;
    // }

    // Using getters for encapsulation
    public float StunNormalizedTimer => stunNormalizedTimer;
    public float AgentMaxAcceleration => agentMaxAcceleration;
    public int CurrentBehaviourStateIndex => currentBehaviourStateIndex;
    public PlayerControllerB TargetPlayer => targetPlayer;
    public Vector3 TransformPosition => transform.position;
    public RoundManager RoundManagerInstance => RoundManager.Instance;
    public HarpBehaviour HeldHarp => _heldHarp;
}

/*
 TODO:

1). Increase navagent size to include harp when in behaviour state 0
2). Client who attacks the ghost makes the ghost not do anything until the client leaves its fov
3). Fix issues with multiple harps on the same map

 */

