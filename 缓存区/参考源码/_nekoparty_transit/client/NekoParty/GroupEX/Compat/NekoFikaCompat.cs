using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace NekoParty.GroupEX.Compat;

internal static class NekoFikaCompat
{
	private readonly struct ApiProbe(string type, params string[] members)
	{
		public readonly string Type = type;

		public readonly string[] Members = members;
	}

	private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

	private static Assembly _fikaAssembly;

	private static readonly ApiProbe[] RequiredApis = new ApiProbe[13]
	{
		new ApiProbe("Fika.Core.Main.Utils.FikaGlobals", "LogInfo", "LogWarning", "LogError", "IsInRaid"),
		new ApiProbe("Fika.Core.Main.Utils.FikaBackendUtils", "JoinMatch", "ServerGuid", "GroupId", "RaidCode", "IsReconnect", "ClientType", "IsSpectator", "Profile", "RemoteEndPoint", "LocalPort", "IsHostNatPunch", "IsHeadlessGame"),
		new ApiProbe("Fika.Core.Main.Utils.LocaleUtils", "CONNECTING_TO_SESSION", "UI_ERROR_CONNECTING", "UI_UNABLE_TO_CONNECT", "UI_JOIN_RAID", "UI_REJOIN_RAID", "UI_MMUI_ONLINE_PLAYERS"),
		new ApiProbe("Fika.Core.Networking.Http.FikaRequestHandler", "GetHost", "UpdateAddPlayer", "GetPlayerPresences", "AvailableReceivers"),
		new ApiProbe("Fika.Core.Networking.NetManagerUtils", "CreatePingingClient"),
		new ApiProbe("Fika.Core.Networking.FikaPingingClient", "Init", "AttemptToPingHost", "PingEndPoint", "Received", "Rejected", "InProgress", "NetClient", "_endPoints"),
		new ApiProbe("Fika.Core.Networking.FikaServer", "Init", "StopNatIntroduceRoutine", "OnDestroy"),
		new ApiProbe("Fika.Core.Networking.FikaClient", "OnPeerConnected", "OnPeerDisconnected"),
		new ApiProbe("Fika.Core.UI.Custom.MatchMakerUIScript", "JoinMatch", "CreateMatchMakerUI", "BuildPartyPanel"),
		new ApiProbe("Fika.Core.UI.Custom.MainMenuUIScript", "SetupGroupActionButton"),
		new ApiProbe("Fika.Core.UI.FikaUIGlobals", "ColorizeText"),
		new ApiProbe("Fika.Core.Networking.Models.Presence.FikaPlayerPresence"),
		new ApiProbe("Fika.Core.FikaPlugin", "Instance")
	};

	internal static string DetectedFikaVersion { get; private set; }

	internal static Assembly FikaAssembly
	{
		get
		{
			if (_fikaAssembly != null)
			{
				return _fikaAssembly;
			}
			_fikaAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => string.Equals(a.GetName().Name, "Fika.Core", StringComparison.OrdinalIgnoreCase));
			return _fikaAssembly;
		}
	}

	internal static Type FindType(string fullName)
	{
		if (string.IsNullOrEmpty(fullName))
		{
			return null;
		}
		lock (TypeCache)
		{
			if (TypeCache.TryGetValue(fullName, out var value))
			{
				return value;
			}
		}
		Type type = null;
		Assembly fikaAssembly = FikaAssembly;
		if (fikaAssembly != null)
		{
			type = fikaAssembly.GetType(fullName, throwOnError: false);
			if (type == null)
			{
				string simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
				try
				{
					type = fikaAssembly.GetTypes().FirstOrDefault((Type t) => t.Name == simpleName);
				}
				catch (ReflectionTypeLoadException ex)
				{
					type = ex.Types?.FirstOrDefault((Type t) => t != null && t.Name == simpleName);
				}
			}
		}
		lock (TypeCache)
		{
			TypeCache[fullName] = type;
			return type;
		}
	}

	internal static bool RunStartupCheck(ManualLogSource log)
	{
		try
		{
			if (Chainloader.PluginInfos.TryGetValue("com.fika.core", out var value))
			{
				DetectedFikaVersion = value.Metadata.Version.ToString();
			}
		}
		catch
		{
		}
		List<string> list = new List<string>();
		ApiProbe[] requiredApis = RequiredApis;
		for (int i = 0; i < requiredApis.Length; i++)
		{
			ApiProbe apiProbe = requiredApis[i];
			Type type = FindType(apiProbe.Type);
			if (type == null)
			{
				list.Add("类型缺失: " + apiProbe.Type);
				continue;
			}
			string[] members = apiProbe.Members;
			foreach (string text in members)
			{
				if (type.GetMember(text, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0)
				{
					list.Add("成员缺失: " + type.FullName + "." + text);
				}
			}
		}
		string arg = DetectedFikaVersion ?? "未知";
		if (list.Count == 0)
		{
			log.LogInfo((object)$"[NekoFikaCompat] Fika {arg} API 自检通过（{RequiredApis.Length} 类型）。");
			return true;
		}
		log.LogWarning((object)$"[NekoFikaCompat] Fika {arg} 存在 {list.Count} 处 API 不兼容，相关功能将自动跳过：");
		foreach (string item in list)
		{
			log.LogWarning((object)("[NekoFikaCompat]   " + item));
		}
		return false;
	}
}
