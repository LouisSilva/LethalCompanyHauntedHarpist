using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.Items;

public class PlushieBehaviour : PhysicsProp
{
    private ManualLogSource _mls;
    private string _plushieId;
    
    #pragma warning disable 0649
    [SerializeField] private Material[] plushieMaterialVariants;
    #pragma warning restore 0649

    private int _materialVariantIndex;

    private bool _loadedVariantFromSave;
    private bool _calledRpc;

    public override void Start()
    {
        base.Start();

        _plushieId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Plushie {_plushieId}");
        Random.InitState(FindObjectOfType<StartOfRound>().randomMapSeed + _plushieId.GetHashCode());
        if (!_calledRpc) ApplyRandomMaterialServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyRandomMaterialServerRpc()
    {
        ApplyRandomMaterialClientRpc();
    }

    [ClientRpc]
    private void ApplyRandomMaterialClientRpc()
    {
        ApplyRandomMaterial();
        _calledRpc = true;
    }

    private void ApplyRandomMaterial()
    {
        if (_loadedVariantFromSave) return;

        if (plushieMaterialVariants.Length > 0)
        {
            _materialVariantIndex = Random.Range(0, plushieMaterialVariants.Length);
            mainObjectRenderer.material = plushieMaterialVariants[_materialVariantIndex];
            LogDebug($"New random material applied: {plushieMaterialVariants[_materialVariantIndex].name}");
        }
        else
        {
            LogDebug("No material variants available.");
        }
    }

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();
        return _materialVariantIndex;
    }

    public override void LoadItemSaveData(int saveData)
    {
        base.LoadItemSaveData(saveData);
        _materialVariantIndex = saveData;
        mainObjectRenderer.material = plushieMaterialVariants[_materialVariantIndex];
        _loadedVariantFromSave = true;
        LogDebug($"Material from save data applied: {plushieMaterialVariants[_materialVariantIndex].name}");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsOwner) return;
        _mls.LogInfo(msg);
        #endif
    }
}