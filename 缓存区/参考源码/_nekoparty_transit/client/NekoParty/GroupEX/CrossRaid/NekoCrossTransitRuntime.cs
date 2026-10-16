using System;
using BepInEx.Configuration;
using Fika.Core;
using Fika.Core.Main.Utils;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.State;
using UnityEngine;

namespace NekoParty.GroupEX.CrossRaid;

internal static class NekoCrossTransitRuntime
{
	private static NekoTransitConfigResponse? _cachedConfig;

	private static bool _hasCachedConfig;

	private static DateTime _cacheTime = DateTime.MinValue;

	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2.0);

	internal static bool ShouldUseCrossTransit()
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return false;
		}
		NekoPartyData? currentParty = NekoGroupState.CurrentParty;
		if (!currentParty.HasValue || currentParty.Value.Members == null || currentParty.Value.Members.Length < 2)
		{
			return false;
		}
		return true;
	}

	internal static void ApplyBeaconIfNeeded()
	{
		if (!ShouldUseCrossTransit())
		{
			return;
		}
		NekoTransitConfigResponse config = GetConfig();
		if (config.Success && config.Enabled && !string.IsNullOrEmpty(config.PunchHost) && config.PunchPort > 0 && (Object)(object)FikaPlugin.Instance != (Object)null)
		{
			FikaPlugin.Instance.NatPunchServerIP = config.PunchHost;
			FikaPlugin.Instance.NatPunchServerPort = (ushort)config.PunchPort;
			ConfigEntry<bool> useNATPunching = FikaPlugin.Instance.Settings.UseNATPunching;
			if (useNATPunching != null)
			{
				useNATPunching.Value = true;
			}
			FikaGlobals.LogInfo($"[NekoCrossRaid] NAT 信标已指向 Hub {config.PunchHost}:{config.PunchPort}", "ApplyBeaconIfNeeded");
		}
	}

	internal static bool TryGetPunchEndpoint(out string host, out int port)
	{
		host = "";
		port = 0;
		NekoTransitConfigResponse config = GetConfig();
		if (!config.Success || !config.Enabled || string.IsNullOrEmpty(config.PunchHost) || config.PunchPort <= 0)
		{
			return false;
		}
		host = config.PunchHost;
		port = config.PunchPort;
		return true;
	}

	internal static bool TryGetNatProbeEndpoints(out string host, out int port1, out int port2)
	{
		host = "";
		port1 = 0;
		port2 = 0;
		NekoTransitConfigResponse config = GetConfig();
		if (!config.Success || !config.Enabled || string.IsNullOrEmpty(config.PunchHost) || config.NatProbePort1 <= 0 || config.NatProbePort2 <= 0)
		{
			return false;
		}
		host = config.PunchHost;
		port1 = config.NatProbePort1;
		port2 = config.NatProbePort2;
		return true;
	}

	internal static bool TryAllocateRelay(string serverGuid, out string host, out int port)
	{
		host = "";
		port = 0;
		if (!GetConfig().RelayEnabled)
		{
			return false;
		}
		try
		{
			NekoTransitRelayResponse nekoTransitRelayResponse = NekoGroupRequestHandler.AllocateTransitRelay(serverGuid);
			if (!nekoTransitRelayResponse.Success || string.IsNullOrEmpty(nekoTransitRelayResponse.Host) || nekoTransitRelayResponse.Port <= 0)
			{
				FikaGlobals.LogWarning("[NekoCrossRaid] 中继 allocate 失败: " + nekoTransitRelayResponse.Error, "TryAllocateRelay");
				return false;
			}
			host = nekoTransitRelayResponse.Host;
			port = nekoTransitRelayResponse.Port;
			FikaGlobals.LogInfo($"[NekoCrossRaid] 已分配中继 {host}:{port} session={nekoTransitRelayResponse.SessionId}", "TryAllocateRelay");
			return true;
		}
		catch (Exception ex)
		{
			FikaGlobals.LogError("[NekoCrossRaid] 中继 allocate 异常: " + ex.Message, "TryAllocateRelay");
			return false;
		}
	}

	private static NekoTransitConfigResponse GetConfig()
	{
		if (_hasCachedConfig && DateTime.UtcNow - _cacheTime < CacheTtl)
		{
			return _cachedConfig.Value;
		}
		try
		{
			_cachedConfig = NekoGroupRequestHandler.GetTransitConfig();
			_hasCachedConfig = true;
			_cacheTime = DateTime.UtcNow;
		}
		catch
		{
		}
		if (!_hasCachedConfig)
		{
			return default(NekoTransitConfigResponse);
		}
		return _cachedConfig.Value;
	}
}
