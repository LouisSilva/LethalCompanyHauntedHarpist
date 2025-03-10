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
            // SyncPoopIdClientRpc(_poopId);

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
        return _variantIndex.Value + 1;
    }

    public override void LoadItemSaveData(int saveData)
    {
        _loadedVariantFromSave = true;
        StartCoroutine(ApplyItemSaveData(saveData - 1));
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