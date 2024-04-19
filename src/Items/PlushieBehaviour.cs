using System;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.Items;

public class PlushieBehaviour : PhysicsProp
{
    private ManualLogSource _mls;
    private string _plushieId;

    private int materialVariantIndex;

    #pragma warning disable 0649
    [SerializeField] private Material[] plushieMaterialVariants;
    [SerializeField] private Renderer renderer;
    #pragma warning restore 0649

    public override void Start()
    {
        materialVariantIndex = -1;
        base.Start();

        _plushieId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Plushie {_plushieId}");
        Random.InitState(FindObjectOfType<StartOfRound>().randomMapSeed - 10);
        ApplyRandomMaterial();
    }
    
    private void ApplyRandomMaterial()
    {
        if (materialVariantIndex != -1)
        {
            renderer.material = plushieMaterialVariants[materialVariantIndex];
        }
        else if (plushieMaterialVariants.Length > 0)
        {
            materialVariantIndex = Random.Range(0, plushieMaterialVariants.Length);
            renderer.material = plushieMaterialVariants[materialVariantIndex];
            LogDebug($"Material applied: {plushieMaterialVariants[materialVariantIndex].name}");
        }
        else
        {
            LogDebug("No material variants available.");
        }
    }

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();
        return materialVariantIndex;
    }

    public override void LoadItemSaveData(int saveData)
    {
        base.LoadItemSaveData(saveData);
        materialVariantIndex = saveData;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsOwner) return;
        _mls.LogInfo(msg);
        #endif
    }
}