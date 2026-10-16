using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dsh.HeadlessPanel;

/// <summary>
/// Reads Fika's live state straight out of the running server via reflection, so the panel
/// needs no HTTP round-trip, no apiKey and never touches Fika's profile cache (which logs a
/// warning on every refresh when two profiles share a PMC nickname).
///
/// Everything is late-bound on purpose: the panel must still load when Fika is absent or has
/// been updated, in which case the affected sections simply report themselves as unavailable.
/// </summary>
public static class FikaBridge
{
    private const string PresenceServiceType = "FikaServer.Services.PresenceService, FikaServer";
    private const string HeadlessServiceType = "FikaServer.Services.Headless.HeadlessService, FikaServer";

    /// <summary>A connected headless client as the panel needs it.</summary>
    public sealed class HeadlessState
    {
        public string ProfileId { get; set; } = "";
        public int State { get; set; }
        public int Players { get; set; }
        public int Level { get; set; }
        public bool SocketOpen { get; set; }
    }

    /// <summary>An online player plus the raid they are in, if any.</summary>
    public sealed class PlayerState
    {
        public string Nickname { get; set; } = "";
        public int Level { get; set; }
        public int Activity { get; set; }
        public string Location { get; set; } = "";
        public bool RaidStarted { get; set; }
        public string MatchId { get; set; } = "";
    }

    public static bool FikaLoaded => Resolve(PresenceServiceType) != null || Resolve(HeadlessServiceType) != null;

    private static Type Resolve(string assemblyQualifiedName)
    {
        try
        {
            return Type.GetType(assemblyQualifiedName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }

    private static object ResolveService(Type serviceType, IServiceProvider services)
    {
        if (serviceType == null || services == null)
        {
            return null;
        }

        try
        {
            return services.GetService(serviceType);
        }
        catch
        {
            return null;
        }
    }

    private static object ReadProperty(object target, string name)
    {
        if (target == null)
        {
            return null;
        }

        try
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static int ReadInt(object target, string name)
    {
        var value = ReadProperty(target, name);
        return value == null ? 0 : Convert.ToInt32(value);
    }

    private static string ReadString(object target, string name)
    {
        return ReadProperty(target, name)?.ToString() ?? "";
    }

    /// <summary>Connected headless clients, keyed by profile id.</summary>
    public static List<HeadlessState> GetHeadlessStates(IServiceProvider services)
    {
        var result = new List<HeadlessState>();
        var service = ResolveService(Resolve(HeadlessServiceType), services);
        if (service == null)
        {
            return result;
        }

        var clients = ReadProperty(service, "HeadlessClients") as IEnumerable;
        if (clients == null)
        {
            return result;
        }

        foreach (var entry in clients)
        {
            // ConcurrentDictionary<K,V> enumerates as KeyValuePair<K,V>.
            var value = ReadProperty(entry, "Value") ?? entry;
            var key = ReadProperty(entry, "Key");

            var socket = ReadProperty(value, "WebSocket");
            var socketState = ReadProperty(socket, "State");

            result.Add(new HeadlessState
            {
                ProfileId = key?.ToString() ?? "",
                State = ReadInt(value, "State"),
                Players = (ReadProperty(value, "Players") as IEnumerable)?.Cast<object>().Count() ?? 0,
                Level = ReadInt(value, "Level"),
                SocketOpen = socketState != null && Convert.ToInt32(socketState) == 2 // WebSocketState.Open
            });
        }

        return result;
    }

    /// <summary>Online players and the map each one is currently in.</summary>
    public static List<PlayerState> GetPlayerStates(IServiceProvider services)
    {
        var result = new List<PlayerState>();
        var service = ResolveService(Resolve(PresenceServiceType), services);
        if (service == null)
        {
            return result;
        }

        var presence = ReadProperty(service, "AllPlayersPresence") as IEnumerable;
        if (presence == null)
        {
            return result;
        }

        foreach (var entry in presence)
        {
            if (entry == null)
            {
                continue;
            }

            var raid = ReadProperty(entry, "RaidInformation");
            var players = ReadProperty(raid, "Players");

            result.Add(new PlayerState
            {
                Nickname = ReadString(entry, "Nickname"),
                Level = ReadInt(entry, "Level"),
                Activity = ReadInt(entry, "Activity"),
                Location = raid == null ? "" : ReadString(raid, "Location"),
                RaidStarted = raid != null && ReadProperty(raid, "Started") is bool started && started,
                MatchId = raid == null ? "" : (ReadProperty(raid, "MatchId")?.ToString() ?? "")
            });
        }

        return result;
    }
}
