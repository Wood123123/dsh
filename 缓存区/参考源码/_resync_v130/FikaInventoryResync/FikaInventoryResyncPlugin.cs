using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Communication;
using Fika.Core.Networking.Packets.Generic;
using HarmonyLib;
using UnityEngine;

namespace FikaInventoryResync;

[BepInPlugin("com.local.fikainventoryresync", "Fika Inventory Resync", "1.1.0")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class FikaInventoryResyncPlugin : BaseUnityPlugin
{
	public const string Guid = "com.local.fikainventoryresync";

	internal static ManualLogSource Log;

	internal static ConfigEntry<bool> Enabled;

	internal static ConfigEntry<float> CooldownSeconds;

	internal static ConfigEntry<bool> Verbose;

	private static FieldRef<ObservedInventoryController, FikaPlayer> _fikaPlayerRef;

	private static FieldInfo _fikaPlayerField;

	private static readonly Dictionary<int, float> _lastRequestAt = new Dictionary<int, float>();

	private static readonly Dictionary<int, int> _suppressed = new Dictionary<int, int>();

	private void Awake()
	{
		Log = ((BaseUnityPlugin)this).Logger;
		Enabled = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Enabled", true, "Master switch for all three patches.");
		CooldownSeconds = ((BaseUnityPlugin)this).Config.Bind<float>("General", "RequestCooldownSeconds", 1f, "Minimum seconds between two resync requests for the same netId. Prevents flooding when a mirror is badly drifted.");
		Verbose = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Verbose", true, "Log every resync request that is actually sent (cooldown-suppressed ones are counted, not logged).");
		Log.LogInfo((object)("FikaInventoryResync v1.1.0 loaded. Enabled=" + Enabled.Value + " Cooldown=" + CooldownSeconds.Value.ToString("0.##") + "s"));
		try
		{
			ApplyPatches();
		}
		catch (Exception ex)
		{
			Log.LogError((object)("ApplyPatches failed: " + ex));
		}
	}

	private static void ApplyPatches()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Expected O, but got Unknown
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Expected O, but got Unknown
		Harmony val = new Harmony("com.local.fikainventoryresync");
		Type type = AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.ObservedInventoryController");
		if (type == null)
		{
			Log.LogError((object)"Patch1 SKIPPED: ObservedInventoryController type not found.");
		}
		else
		{
			MethodInfo methodInfo = null;
			MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo2 in methods)
			{
				if (methodInfo2.Name.EndsWith("CreateOperationFromDescriptor", StringComparison.Ordinal))
				{
					methodInfo = methodInfo2;
					break;
				}
			}
			if (methodInfo == null)
			{
				Log.LogError((object)("Patch1 SKIPPED: CreateOperationFromDescriptor not found on " + type.FullName));
			}
			else
			{
				try
				{
					_fikaPlayerRef = AccessTools.FieldRefAccess<ObservedInventoryController, FikaPlayer>("_fikaPlayer");
				}
				catch (Exception ex)
				{
					Log.LogWarning((object)("Patch1: _fikaPlayer FieldRef unavailable (" + ex.Message + "), falling back to reflection."));
					_fikaPlayerField = AccessTools.Field(type, "_fikaPlayer");
				}
				val.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "CreateOperationFromDescriptor_Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				Log.LogInfo((object)("Patch1 OK: client resync request hooked on " + methodInfo.DeclaringType.FullName + "::" + methodInfo.Name));
			}
		}
		MethodInfo methodInfo3 = AccessTools.Method(typeof(FikaServer), "OnResyncInventoryIdPacketReceived", (Type[])null, (Type[])null);
		if (methodInfo3 == null)
		{
			Log.LogError((object)"Patch2a SKIPPED: FikaServer.OnResyncInventoryIdPacketReceived not found.");
		}
		else
		{
			val.Patch((MethodBase)methodInfo3, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "ServerResyncGuard_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Log.LogInfo((object)"Patch2a OK: host resync guard on FikaServer::OnResyncInventoryIdPacketReceived");
		}
		MethodInfo methodInfo4 = AccessTools.Method(typeof(FikaClient), "OnResyncInventoryIdPacketReceived", (Type[])null, (Type[])null);
		if (methodInfo4 == null)
		{
			Log.LogError((object)"Patch2b SKIPPED: FikaClient.OnResyncInventoryIdPacketReceived not found.");
		}
		else
		{
			val.Patch((MethodBase)methodInfo4, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "ClientResyncGuard_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Log.LogInfo((object)"Patch2b OK: client resync guard on FikaClient::OnResyncInventoryIdPacketReceived");
		}
		MethodInfo methodInfo5 = AccessTools.Method(typeof(FikaServer), "OnNetworkReceive", (Type[])null, (Type[])null);
		if (methodInfo5 == null)
		{
			Log.LogError((object)"Patch3 SKIPPED: FikaServer.OnNetworkReceive not found.");
			return;
		}
		val.Patch((MethodBase)methodInfo5, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "OnNetworkReceive_Transpiler", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null);
		Log.LogInfo((object)"Patch3 OK: host inventory-operation de-duplication transpiler on FikaServer::OnNetworkReceive");
	}

	internal static IEnumerable<CodeInstruction> OnNetworkReceive_Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo methodInfo = AccessTools.Method(typeof(NetDataReader), "GetByte", (Type[])null, (Type[])null);
		MethodInfo operand = AccessTools.Method(typeof(FikaInventoryResyncPlugin), "ReadBroadcastFlagForRelay", (Type[])null, (Type[])null);
		List<CodeInstruction> list = new List<CodeInstruction>(instructions);
		int num = 0;
		foreach (CodeInstruction item in list)
		{
			if (item.operand is MethodInfo methodInfo2 && methodInfo2 == methodInfo)
			{
				if (num == 0)
				{
					item.opcode = OpCodes.Call;
					item.operand = operand;
				}
				num++;
			}
		}
		if (num == 1)
		{
			Log.LogInfo((object)"Patch3: replaced the broadcast-flag read (1 call site, as expected).");
		}
		else
		{
			Log.LogError((object)("Patch3: expected exactly 1 NetDataReader.GetByte() call site in FikaServer.OnNetworkReceive but found " + num + " -- patch left inert."));
		}
		return list;
	}

	internal static byte ReadBroadcastFlagForRelay(NetDataReader reader)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Invalid comparison between Unknown and I4
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		byte b = reader.GetByte();
		if (b != 1 || !Enabled.Value)
		{
			return b;
		}
		int position = reader.Position;
		try
		{
			if ((int)reader.GetEnum<EPacketType>() == 2 && (int)reader.GetEnum<EGenericPacketType>() == 11)
			{
				return 0;
			}
		}
		catch
		{
		}
		finally
		{
			reader.SetPosition(position);
		}
		return b;
	}

	private static FikaPlayer GetFikaPlayer(ObservedInventoryController ctrl)
	{
		if (_fikaPlayerRef != null)
		{
			return _fikaPlayerRef.Invoke(ctrl);
		}
		if (_fikaPlayerField != null)
		{
			object? value = _fikaPlayerField.GetValue(ctrl);
			return (FikaPlayer)((value is FikaPlayer) ? value : null);
		}
		return null;
	}

	private static void CreateOperationFromDescriptor_Postfix(ObservedInventoryController __instance, OperationCreationResult __result)
	{
		try
		{
			if (!Enabled.Value || !((OperationCreationResult)(ref __result)).Failed || !Singleton<IFikaNetworkManager>.Instantiated)
			{
				return;
			}
			IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
			FikaClient val = (FikaClient)(object)((instance is FikaClient) ? instance : null);
			if ((Object)(object)val == (Object)null)
			{
				return;
			}
			FikaPlayer fikaPlayer = GetFikaPlayer(__instance);
			if ((Object)(object)fikaPlayer == (Object)null)
			{
				return;
			}
			int netId = fikaPlayer.NetId;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (_lastRequestAt.TryGetValue(netId, out var value) && realtimeSinceStartup - value < CooldownSeconds.Value)
			{
				_suppressed.TryGetValue(netId, out var value2);
				_suppressed[netId] = value2 + 1;
				return;
			}
			_lastRequestAt[netId] = realtimeSinceStartup;
			_suppressed.TryGetValue(netId, out var value3);
			_suppressed[netId] = 0;
			ResyncInventoryIdPacket val2 = default(ResyncInventoryIdPacket);
			((ResyncInventoryIdPacket)(ref val2))._002Ector(netId);
			val.SendData<ResyncInventoryIdPacket>(ref val2, (DeliveryMethod)2, true);
			if (Verbose.Value)
			{
				Log.LogInfo((object)("sent resync request netId=" + netId + ", error: " + ((object)((OperationCreationResult)(ref __result)).Error)?.ToString() + ((value3 > 0) ? (", suppressed " + value3 + " since last request") : "")));
			}
		}
		catch (Exception ex)
		{
			Log.LogError((object)("Patch1 postfix error: " + ex.Message));
		}
	}

	private static bool ServerResyncGuard_Prefix(ResyncInventoryIdPacket packet)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled.Value)
		{
			return true;
		}
		if (((MongoID)(ref packet.MongoId)).Equals(default(MongoID)))
		{
			if (Verbose.Value)
			{
				Log.LogInfo((object)("host dropped empty resync for netId=" + packet.NetId + " (request semantics, not an id assignment)"));
			}
			return false;
		}
		return true;
	}

	private static bool ClientResyncGuard_Prefix(FikaClient __instance, ResyncInventoryIdPacket packet)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled.Value)
		{
			return true;
		}
		if (!((MongoID)(ref packet.MongoId)).Equals(default(MongoID)))
		{
			return true;
		}
		CoopHandler coopHandler = __instance.CoopHandler;
		if ((Object)(object)coopHandler == (Object)null || coopHandler.Players == null)
		{
			return false;
		}
		if (!coopHandler.Players.TryGetValue(packet.NetId, out var value) || (Object)(object)value == (Object)null)
		{
			return false;
		}
		if (((Player)value).IsYourPlayer)
		{
			if (Verbose.Value)
			{
				Log.LogInfo((object)("answering resync request for our own netId=" + packet.NetId));
			}
			return true;
		}
		if (Verbose.Value)
		{
			Log.LogInfo((object)("client dropped empty resync for netId=" + packet.NetId + " (would have cleared someone's mirror)"));
		}
		return false;
	}
}
