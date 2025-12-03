using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.Types;

[Serializable]
public class SyncedInstance<T>
{
    internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
    internal static bool IsClient => NetworkManager.Singleton.IsClient;
    internal static bool IsHost => NetworkManager.Singleton.IsHost;

    [NonSerialized] protected static int IntSize = 4;

    public static T Default { get; private set; }
    public static T Instance { get; private set; }

    public static bool Synced { get; internal set; }

    protected void InitInstance(T instance)
    {
        Default = instance;
        Instance = instance;

        IntSize = sizeof(int);
    }

    private static void RequestSync()
    {
        if (!IsClient) return;

        using FastBufferWriter stream = new(IntSize, Allocator.Temp);
        MessageManager.SendNamedMessage($"{MyPluginInfo.PLUGIN_GUID}_OnRequestConfigSync", 0uL, stream);
    }

    private static void OnRequestSync(ulong clientId, FastBufferReader _)
    {
        if (!IsHost) return;

        Debug.Log($"Config sync request received from client: {clientId}");

        byte[] array = SerializeToBytes(Instance);
        int value = array.Length;

        using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

        try
        {
            stream.WriteValueSafe(in value);
            stream.WriteBytesSafe(array);

            MessageManager.SendNamedMessage($"{MyPluginInfo.PLUGIN_GUID}_OnReceiveConfigSync", clientId, stream);
        }
        catch (Exception e)
        {
            Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
        }
    }

    private static void OnReceiveSync(ulong _, FastBufferReader reader)
    {
        if (!reader.TryBeginRead(IntSize))
        {
            Debug.LogError("Config sync error: Could not begin reading buffer.");
            return;
        }

        reader.ReadValueSafe(out int val);
        if (!reader.TryBeginRead(val))
        {
            Debug.LogError("Config sync error: Host could not sync.");
            return;
        }

        byte[] data = new byte[val];
        reader.ReadBytesSafe(ref data, val);

        SyncInstance(data);

        Debug.Log("Successfully synced config with host.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    public static void InitializeLocalPlayer()
    {
        if (IsHost)
        {
            MessageManager.RegisterNamedMessageHandler($"{MyPluginInfo.PLUGIN_GUID}_OnRequestConfigSync", OnRequestSync);
            Synced = true;

            return;
        }

        Synced = false;
        MessageManager.RegisterNamedMessageHandler($"{MyPluginInfo.PLUGIN_GUID}_OnReceiveConfigSync", OnReceiveSync);
        RequestSync();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartDisconnect))]
    public static void PlayerLeave()
    {
        RevertSync();
    }

    internal static void SyncInstance(byte[] data)
    {
        Instance = DeserializeFromBytes(data);
        Synced = true;
    }

    internal static void RevertSync()
    {
        Instance = Default;
        Synced = false;
    }

    public static byte[] SerializeToBytes(T val)
    {
        BinaryFormatter bf = new();
        using MemoryStream stream = new();

        try
        {
            bf.Serialize(stream, val);
            return stream.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error serializing instance: {e}");
            return null;
        }
    }

    public static T DeserializeFromBytes(byte[] data)
    {
        BinaryFormatter bf = new();
        using MemoryStream stream = new(data);

        try
        {
            return (T)bf.Deserialize(stream);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deserializing instance: {e}");
            return default;
        }
    }
}