using LethalCompanyHarpGhost.Types;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.Items;

public class PlushieBehaviour : PhysicsProp
{
    private string _plushieId;

    #pragma warning disable 0649
    [SerializeField] private Material[] plushieMaterialVariants;
    #pragma warning restore 0649

    private CachedValue<NetworkObject> _networkObject;

    private readonly NetworkVariable<int> _variantIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private bool _loadedVariantFromSave;
    private bool _networkEventsSubscribed;

    private void Awake()
    {
        _networkObject = new CachedValue<NetworkObject>(GetComponent<NetworkObject>, true);
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();

        if (IsServer)
        {
            _plushieId = Guid.NewGuid().ToString();
            Random.InitState(StartOfRound.Instance.randomMapSeed + _plushieId.GetHashCode());

            if (!_loadedVariantFromSave)
            {
                _variantIndex.Value = Random.Range(0, plushieMaterialVariants.Length);
            }
        }
    }

    private void ApplyVariant(int chosenVariantIndex)
    {
        if (plushieMaterialVariants.Length > 0)
            mainObjectRenderer.material = plushieMaterialVariants[chosenVariantIndex];
    }

    private void OnVariantIndexChanged(int oldValue, int newValue)
    {
        ApplyVariant(newValue);
    }

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();
        if (!IsOwner)
        {
            HarpGhostPlugin.Logger.LogWarning($"{nameof(GetItemDataToSave)} called on a client which doesn't own it.");
        }

        return _variantIndex.Value + 1;
    }

    // This function is called for server and clients
    public override void LoadItemSaveData(int saveData)
    {
        saveData -= 1;
        base.LoadItemSaveData(saveData);
        if (!IsOwner)
        {
            HarpGhostPlugin.Logger.LogWarning($"{nameof(PlushieBehaviour)}.{nameof(LoadItemSaveData)} called on a client which doesn't own it.");
            return;
        }

        _loadedVariantFromSave = true;
        StartCoroutine(ApplyItemSaveData(saveData));
    }

    private IEnumerator ApplyItemSaveData(int loadedVariantIndex)
    {
        while (!_networkObject.Value.IsSpawned)
        {
            yield return new WaitForSeconds(0.2f);
        }

        _variantIndex.Value = loadedVariantIndex;
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        _variantIndex.OnValueChanged += OnVariantIndexChanged;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        _variantIndex.OnValueChanged -= OnVariantIndexChanged;
        _networkEventsSubscribed = false;
    }
}