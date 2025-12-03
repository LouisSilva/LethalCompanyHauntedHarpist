using GameNetcodeStuff;
using LethalCompanyHarpGhost.EnforcerGhost;
using LethalCompanyHarpGhost.HarpGhost;
using LethalCompanyHarpGhost.Items;
using LethalCompanyHarpGhost.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIServer : MusicalGhost, IEscortee
{
    private List<EnforcerGhostAIServer> _escorts = [];

#pragma warning disable 0649
    [Header("Controllers and Managers")] [Space(5f)]
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    [SerializeField] private Animator animator;
#pragma warning restore 0649

    [Header("AI and Pathfinding")] [Space(5f)]
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.5f;
    [SerializeField] private float agentMaxAccelerationInEscapeMode = 30f;
    [SerializeField] private float agentMaxSpeedInEscapeMode = 10f;
    [SerializeField] private float openDoorSpeedMultiplierInEscapeMode = 6f;

    [SerializeField] private int numberOfEscorts = 3;

    private enum States
    {
        PlayingMusicWhileEscorted,
        RunningToEscapeDoor,
        RunningToEdgeOfOutsideMap,
        TeleportingOutOfMap,
        Dead
    }

    private CachedList<EntranceTeleport> _allExitsInLevel;

    private CachedValue<float> _escortRowSpacingSqr;

    private Vector3 _agentLastPosition;
    private readonly Queue<Vector3> _escortAgentPathPoints = new();

    private EntranceTeleport _escapeDoor;

    private InstrumentBehaviour _heldBagpipes;

    private NetworkObjectReference _instrumentObjectRef;

    private Coroutine _teleportCoroutine;

    private const int MaxPathPoints = 50;

    private float _agentCurrentSpeed;
    private float _openDoorTimer;
    private float _takeDamageCooldown;
    private const float EscortHorizontalBaseLength = 5f;
    private const float EscortRowSpacing = 3f;

    private bool _hasSubscribedToNetworkEvents;
    private bool _agentIsMoving;
    private bool _chosenEscapeDoor;
    private bool _isNarrowPath;
    private bool _inStunAnimation;
    private bool _openDoorTimerActivated;
    private bool _currentlyRetiringAllEscorts;
    private bool _agroSfxPlayed;
    private bool _tauntSfxPlayed;

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        SubscribeToNetworkEvents();

        netcodeController = GetComponent<BagpipesGhostNetcodeController>();
        if (!netcodeController) HarpGhostPlugin.Logger.LogError("Netcode Controller is null");

        agent = GetComponent<NavMeshAgent>();
        if (!agent) HarpGhostPlugin.Logger.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        InitializeConfigValues();
        EnableEnemyMesh(true);

        _allExitsInLevel = new CachedList<EntranceTeleport>(() => FindObjectsOfType<EntranceTeleport>().ToList());
        _escortRowSpacingSqr = new CachedValue<float>(() => EscortRowSpacing * EscortRowSpacing);

        netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsRunning, false);

        netcodeController.SpawnBagpipesServerRpc();
        netcodeController.GrabBagpipesClientRpc();

        if (!HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab)
        {
            HarpGhostPlugin.Logger.LogError("Enforcer ghost prefab is null, making bagpipe ghost run away from players because he is not escorted");
            SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
        }
        else StartCoroutine(SpawnEscorts(() => { netcodeController.PlayBagpipesMusicClientRpc(); }));

        // The ghost is already in this state, but calling the method will choose the ghost's first node to go to
        SwitchBehaviourStateLocally((int)States.PlayingMusicWhileEscorted);
        HarpGhostPlugin.LogVerbose("Bagpipe Ghost Spawned");
    }

    public void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    public void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || !netcodeController || _hasSubscribedToNetworkEvents) return;

        netcodeController.OnGrabBagpipes += HandleGrabBagpipes;
        netcodeController.OnSpawnBagpipes += HandleSpawnBagpipes;

        _hasSubscribedToNetworkEvents = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !netcodeController || !_hasSubscribedToNetworkEvents) return;

        netcodeController.OnGrabBagpipes -= HandleGrabBagpipes;
        netcodeController.OnSpawnBagpipes -= HandleSpawnBagpipes;

        _hasSubscribedToNetworkEvents = false;
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        enemyHP = BagpipeGhostConfig.Instance.BagpipeGhostInitialHealth.Value;
        agentMaxSpeedInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostMaxSpeedInEscapeMode.Value;
        agentMaxAccelerationInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostMaxAccelerationInEscapeMode.Value;
        openDoorSpeedMultiplierInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostDoorSpeedMultiplierInEscapeMode.Value;
        numberOfEscorts = (int)Mathf.Clamp(BagpipeGhostConfig.Instance.BagpipeGhostNumberOfEscortsToSpawn.Value, 0, Mathf.Infinity);

        netcodeController.InitializeConfigValuesClientRpc();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        _agentIsMoving = _agentCurrentSpeed > 0f;
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;

        _takeDamageCooldown -= Time.deltaTime;
        CalculateAgentSpeed();

        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            netcodeController.DoAnimationClientRpc(BagpipesGhostAIClient.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsStunned, false);
        }

        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsRunning, false);
            return;
        }

        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusicWhileEscorted:
            {
                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                RunAnimation();

                if (_openDoorTimerActivated)
                {
                    _openDoorTimer += Time.deltaTime;
                    if (_openDoorTimer > 1f && stunNormalizedTimer <= 0)
                    {
                        _openDoorTimerActivated = false;
                        GoThroughEscapeDoor();
                    }
                }

                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
               RunAnimation();

               break;
            }

            case (int)States.TeleportingOutOfMap:
            {
                break;
            }

            case (int)States.Dead:
            {
                break;
            }
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusicWhileEscorted:
            {
                // Check if the ghost has reached its destination
                if ((transform.position - targetNode.position).sqrMagnitude <= 25f) // 5f * 5f = 25f
                {
                    // Pick next node to go to
                    int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.3f));
                    Transform farAwayTransform = ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset));
                    targetNode = farAwayTransform;
                    if (!SetDestinationToPosition(farAwayTransform.position, true))
                    {
                        HarpGhostPlugin.Logger.LogWarning("Bagpipe ghost pathfinding has failed, as a fail-safe the ghost is now running away. This should never happen.");
                        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
                        break;
                    }
                }

                UpdateEscortAgentPathPoints();
                UpdateAgentFormation();

                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                if (isOutside) SwitchBehaviourStateLocally((int)States.RunningToEdgeOfOutsideMap);
                if (!_chosenEscapeDoor) ChooseEscapeDoor();

                if ((transform.position - _escapeDoor.exitPoint.position).sqrMagnitude <= 9f && // 3f * 3f = 9f
                    !_openDoorTimerActivated)
                {
                    if (!_tauntSfxPlayed)
                    {
                        _tauntSfxPlayed = true;
                        netcodeController.PlayCreatureVoiceClientRpc((int)BagpipesGhostAIClient.AudioClipTypes.Taunt, 3, false);
                    }

                    // Check if the ghost has reached the exit door
                    if (Vector3.Distance(transform.position, _escapeDoor.exitPoint.position) <= 1 && !_openDoorTimerActivated)
                    {
                        HarpGhostPlugin.LogVerbose("Reached escape door");
                        moveTowardsDestination = false;
                        _openDoorTimerActivated = true;
                        _openDoorTimer = 0f;
                    }
                }

                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
                if (Vector3.Distance(transform.position, destination) <= 1)
                {
                    moveTowardsDestination = false;
                    if (_teleportCoroutine == null)
                        StartCoroutine(ExitTheGameThroughOutsideNode());
                }

                break;
            }

            case (int)States.TeleportingOutOfMap:
            {
                break;
            }
        }
    }

    private void GoThroughEscapeDoor()
    {
        if (!IsServer) return;

        isOutside = true;
        serverPosition = _escapeDoor.entrancePoint.position;
        transform.position = serverPosition;

        HarpGhostPlugin.LogVerbose("Going through escape door!");
        agent.Warp(serverPosition);
        SyncPositionToClients();
        SwitchBehaviourStateLocally((int)States.RunningToEdgeOfOutsideMap);
    }

    private IEnumerator ExitTheGameThroughOutsideNode()
    {
        if(!IsServer) yield break;
        HarpGhostPlugin.LogVerbose("Reached outside escape node");

        SwitchBehaviourStateLocally((int)States.TeleportingOutOfMap);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, false);
        netcodeController.PlayTeleportVfxClientRpc();
        StartCoroutine(PlaySfxAfterTime(0.3f, (int)BagpipesGhostAIClient.AudioClipTypes.LongLaugh,
            2, false));
        yield return new WaitForSeconds(0.5f);
        netcodeController.DespawnHeldBagpipesClientRpc();
        EnableEnemyMesh(false);
        netcodeController.SetMeshEnabledClientRpc(false);

        yield return new WaitForSeconds(5);
        KillEnemyClientRpc(true);
        _teleportCoroutine = null;
        Destroy(this);
    }

    private void ChooseEscapeDoor()
    {
        if (!IsServer) return;

        HarpGhostPlugin.LogVerbose("Choosing escape door");
        _chosenEscapeDoor = true;

        EntranceTeleport closestExit = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < _allExitsInLevel.Value.Count; i++)
        {
            EntranceTeleport exit = _allExitsInLevel.Value[i];
            if (!exit || !exit.exitPoint) continue;

            float distanceSqr = (transform.position - exit.exitPoint.position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestExit = exit;
            }
        }

        if (!closestExit)
        {
            HarpGhostPlugin.Logger.LogError("No valid escape doors found!");
            return;
        }

        _escapeDoor = closestExit;

        // The "Avoid the escape door being the main door if possible" logic has been removed
        SetDestinationToPosition(_escapeDoor.exitPoint.position);

        // EntranceTeleport[] exits = FindObjectsOfType<EntranceTeleport>().Where(exit => exit != null && exit.exitPoint != null).ToArray();
        // Dictionary<EntranceTeleport, float> exitDistances = exits.ToDictionary(exit => exit, exit => Vector3.Distance(transform.position, exit.exitPoint.position));
        //
        // EntranceTeleport closestExit = exitDistances.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
        // _escapeDoor = closestExit;
        //
        // // Avoid the escape door being the main door if possible
        // if (closestExit.entranceId == 0)
        // {
        //     if (Vector3.Distance(transform.position, closestExit.exitPoint.position) > 30f)
        //     {
        //         exitDistances.Remove(closestExit);
        //         _escapeDoor = exitDistances.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
        //     }
        // }
    }

    // Makes the escorts enter rambo state and removes them from the escort list
    private void RetireAllEscorts(PlayerControllerB targetPlayerToSet = null)
    {
        if (!IsServer) return;
        if (_currentlyRetiringAllEscorts) return;

        _currentlyRetiringAllEscorts = true;
        for (int i = _escorts.Count - 1; i >= 0; i--)
        {
            HarpGhostPlugin.LogVerbose($"Retiring escort {_escorts[i].GhostId}");
            if (targetPlayerToSet)
            {
                _escorts[i].targetPlayer = targetPlayerToSet;
                _escorts[i].TargetPosition = targetPlayerToSet.transform.position;
                _escorts[i].SwitchToBehaviourStateOnLocalClient((int)EnforcerGhostAIServer.States.InvestigatingTargetPosition);
            }
            else
            {
                _escorts[i].SwitchToBehaviourStateOnLocalClient((int)EnforcerGhostAIServer.States.SearchingForPlayers);
            }

            RemoveEscort(i);
        }

        _currentlyRetiringAllEscorts = false;
    }

    private void UpdateAgentFormation()
    {
        Vector3[] escortAgentPathPointsArray = _escortAgentPathPoints.ToArray();
        _isNarrowPath = CheckForNarrowPassages();

        for (int i = _escorts.Count - 1; i >= 0; i--)
        {
            // Check if escort is null/dead and if so remove them from the escort list
            if (!_escorts[i]|| _escorts[i].isEnemyDead)
            {
                _escorts.RemoveAt(i);
                continue;
            }

            _escorts[i].escorteePingTimer = 5f;

            // Check if the escort has finished its spawn animation before giving it orders
            if (!_escorts[i].FullySpawned) continue;

            // Check if the bagpipe ghost is moving, if not then don't bother making the escorts do anything
            if (!_agentIsMoving && (transform.position - _escorts[i].transform.position).sqrMagnitude < _escortRowSpacingSqr.Value) continue;

            Vector3 targetPosition;
            if (!_isNarrowPath && _escorts.Count > 1) // triangle formation
            {
                float lateralOffset = (i % 2 * 2 - 1) * EscortHorizontalBaseLength / 2;
                int row = i / 2;
                Vector3 directionOffset = transform.right * lateralOffset - transform.forward * (EscortRowSpacing * row);
                targetPosition = transform.position + directionOffset;
            }
            else // single file line
            {
                const float closenessToEscorteeFactor = 0.5f;
                const float increasedGapBetweenEscortersFactor = 2f;

                int pathPointsLength = escortAgentPathPointsArray.Length;
                int targetIndex = Mathf.Max(0, Mathf.RoundToInt(pathPointsLength - 1 - i * increasedGapBetweenEscortersFactor));

                targetPosition = escortAgentPathPointsArray[Mathf.Clamp(targetIndex, 0, pathPointsLength - 1)];
                targetPosition = Vector3.Lerp(targetPosition, transform.position, closenessToEscorteeFactor);
            }

            if (_agentIsMoving || (_escorts[i].transform.position - targetPosition).magnitude > 1f)
            {
                _escorts[i].SetDestinationToPosition(targetPosition);
            }
        }
    }

    private void UpdateEscortAgentPathPoints()
    {
        if (_escorts.Count == 0) return;

        if (_escortAgentPathPoints.Count == 0 ||
            Vector3.Distance(_escortAgentPathPoints.Peek(), transform.position) > 0.25f)
        {
            _escortAgentPathPoints.Enqueue(transform.position);
        }

        // Dequeue when the queue is full
        if (_escortAgentPathPoints.Count > MaxPathPoints)
        {
            _escortAgentPathPoints.Dequeue();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckForNarrowPassages()
    {
        float sideLength = EscortHorizontalBaseLength * Mathf.Sqrt(2);
        bool leftBlocked = Physics.Raycast(transform.position, -transform.right, out RaycastHit _, sideLength / 2);
        bool rightBlocked = Physics.Raycast(transform.position, transform.right, out RaycastHit _, sideLength / 2);
        return leftBlocked || rightBlocked;
    }

    private void AddEscort(EnforcerGhostAIServer escort)
    {
        if (_escorts == null || _escorts.Contains(escort)) return;
        escort.Escortee = this;
        _escorts.Add(escort);
    }

    internal void RemoveEscort(int indexOfEscort)
    {
        if (!_escorts[indexOfEscort]) return;
        _escorts[indexOfEscort].Escortee = null;
        _escorts.RemoveAt(indexOfEscort);
    }

    private IEnumerator SpawnEscorts(Action callback = null)
    {
        yield return new WaitForSeconds(0.5f); // wait a little bit to allow the ghost to move a bit away from the vent where it spawned at (away from the wall)

        _escorts = new List<EnforcerGhostAIServer>(numberOfEscorts);
        for (int i = 0; i < numberOfEscorts; i++)
        {
            GameObject escort = Instantiate(HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab, transform.position, Quaternion.identity);
            escort.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            RoundManager.Instance.currentEnemyPower += HarpGhostPlugin.EnforcerGhostEnemyType.PowerLevel;
            SpawnableEnemyWithRarity first = null;

            for (int j = 0; j < RoundManager.Instance.currentLevel.Enemies.Count; j++)
            {
                SpawnableEnemyWithRarity enemy = RoundManager.Instance.currentLevel.Enemies[j];
                if (enemy.enemyType.name == "EnforcerGhost")
                {
                    first = enemy;
                    break;
                }
            }

            ++first!.enemyType.numberSpawned;

            EnforcerGhostAIServer escortScript = escort.GetComponent<EnforcerGhostAIServer>();
            escortScript.agent.avoidancePriority = Mathf.Min(i + 1, 99); // Give the escort agents a different, descending priority
            AddEscort(escortScript);
            yield return new WaitForSeconds(1f);
        }

        // This callback can technically be used for anything, but its only use is going to be for playing music after the escorts are spawned
        if (callback == null) yield break;
        yield return new WaitForSeconds(0.5f);
        callback.Invoke();
    }

    private void HandleGrabBagpipes()
    {
        if (_heldBagpipes) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldBagpipes = networkObject.gameObject.GetComponent<InstrumentBehaviour>();
    }

    private void HandleSpawnBagpipes(NetworkObjectReference instrumentObjectRef, int scrapValue)
    {
        _instrumentObjectRef = instrumentObjectRef;
    }

    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        if (isEnemyDead || currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead) return;

        netcodeController.PlayCreatureVoiceClientRpc((int)HarpGhostAudioManager.AudioClipTypes.Stun, 2);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(BagpipesGhostAIClient.IsStunned);
        _inStunAnimation = true;

        if (currentBehaviourStateIndex != (int)States.PlayingMusicWhileEscorted) return;
        if (!_agroSfxPlayed)
        {
            _agroSfxPlayed = true;
            StartCoroutine(PlaySfxAfterTime(setToStunTime + 1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
        }
        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer || isEnemyDead || _takeDamageCooldown > 0 || currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead)
            return;

        if (!BagpipeGhostConfig.Instance.BagpipeGhostFriendlyFire.Value && !playerWhoHit)
            return;

        enemyHP -= force;
        _takeDamageCooldown = 0.03f;

        if (enemyHP > 0)
        {
            netcodeController.PlayCreatureVoiceClientRpc((int)BagpipesGhostAIClient.AudioClipTypes.Damage, 2);
            if (!_agroSfxPlayed)
            {
                _agroSfxPlayed = true;
                StartCoroutine(PlaySfxAfterTime(1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
            }

            if (currentBehaviourStateIndex == (int)States.PlayingMusicWhileEscorted) SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor, playerWhoHit);
            return;
        }

        KillEnemyClientRpc(false);
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        if (!IsServer) return;
        SwitchBehaviourStateLocally((int)States.Dead);
    }

    private IEnumerator PlaySfxAfterTime(float time, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        yield return new WaitForSeconds(time);
        netcodeController.PlayCreatureVoiceClientRpc(typeIndex, clipArrayLength, interrupt);
    }

    private void SwitchBehaviourStateLocally(int state, PlayerControllerB targetPlayerToSet = null)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.PlayingMusicWhileEscorted:
            {
                HarpGhostPlugin.LogVerbose($"Switched to behaviour state {(int)States.PlayingMusicWhileEscorted}!");

                agentMaxSpeed = 0.5f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                openDoorSpeedMultiplier = 6;

                // Pick first node to go to
                int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.1f));
                Transform farAwayTransform = ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset));
                targetNode = farAwayTransform;
                if (!SetDestinationToPosition(farAwayTransform.position, true))
                {
                    HarpGhostPlugin.Logger.LogWarning("Bagpipe ghost pathfinding has failed, as a fail-safe the ghost is now running away. This should not happen, contact the mod developer if you see this");
                }

                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                HarpGhostPlugin.LogVerbose($"Switched to behaviour state {(int)States.RunningToEscapeDoor}!");

                agentMaxSpeed = agentMaxSpeedInEscapeMode;
                agentMaxAcceleration = agentMaxAccelerationInEscapeMode;
                openDoorSpeedMultiplier = openDoorSpeedMultiplierInEscapeMode;
                movingTowardsTargetPlayer = false;

                RetireAllEscorts(targetPlayerToSet);
                netcodeController.StopBagpipesMusicClientRpc();
                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
                HarpGhostPlugin.LogVerbose($"Switched to behaviour state {(int)States.RunningToEdgeOfOutsideMap}!");

                agentMaxSpeed = agentMaxSpeedInEscapeMode;
                agentMaxAcceleration = agentMaxAccelerationInEscapeMode;
                openDoorSpeedMultiplier = openDoorSpeedMultiplierInEscapeMode;
                movingTowardsTargetPlayer = false;

                RetireAllEscorts();
                allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
                SetDestinationToPosition(ChooseFarthestNodeFromPosition(StartOfRound.Instance.middleOfShipNode.position, true).position);
                netcodeController.StopBagpipesMusicClientRpc();

                break;
            }

            case (int)States.TeleportingOutOfMap:
            {
                HarpGhostPlugin.LogVerbose($"Switched to behaviour state {(int)States.TeleportingOutOfMap}!");

                agentMaxSpeed = 1f;
                agentMaxAcceleration = 0f;
                movingTowardsTargetPlayer = false;

                RetireAllEscorts();
                netcodeController.StopBagpipesMusicClientRpc();
                Destroy(GetComponentInChildren<ScanNodeProperties>().gameObject);

                break;
            }

            case (int)States.Dead:
            {
                HarpGhostPlugin.LogVerbose($"Switched to behaviour state {(int)States.Dead}!");

                agentMaxSpeed = 0f;
                agentMaxAcceleration = 0f;
                movingTowardsTargetPlayer = false;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;

                netcodeController.EnterDeathStateClientRpc();
                RetireAllEscorts();
                break;
            }
        }

        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
    }

    private void RunAnimation()
    {
        if (!IsServer) return;

        bool isRunning = _agentCurrentSpeed >= 3f;
        if (animator.GetBool(BagpipesGhostAIClient.IsRunning) != isRunning && !_inStunAnimation)
            netcodeController.ChangeAnimationParameterBoolClientRpc(BagpipesGhostAIClient.IsRunning, isRunning);
    }

    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex != (int)States.Dead)
        {
            MoveWithAcceleration();
        }
    }

    private void MoveWithAcceleration()
    {
        if (!IsServer) return;

        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);

        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }

    public void EscorteeBreakoff(PlayerControllerB targetPlayerReceived = null)
    {
        RetireAllEscorts(targetPlayerReceived);
        if (!_agroSfxPlayed)
        {
            _agroSfxPlayed = true;
            StartCoroutine(PlaySfxAfterTime(0.1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
        }
        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
    }
}