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
using EFT.InventoryLogic;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.ObservedClasses.HandsControllers;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Communication;
using Fika.Core.Networking.Packets.Generic;
using HarmonyLib;
using UnityEngine;

namespace FikaInventoryResync;

[BepInPlugin("com.local.fikainventoryresync", "Fika Inventory Resync", "1.3.1")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class FikaInventoryResyncPlugin : BaseUnityPlugin
{
	public const string Guid = "com.local.fikainventoryresync";

	internal static ManualLogSource Log;

	internal static ConfigEntry<bool> Enabled;

	internal static ConfigEntry<float> CooldownSeconds;

	internal static ConfigEntry<bool> Verbose;

	private static FieldInfo _weaponManagerField;

	private static FieldInfo _objectInHandsAnimatorField;

	private static FieldInfo _weaponPrefabField;

	private static readonly HashSet<int> _nullManagerReported = new HashSet<int>();

	private static int _shellEjectSkipped;

	private static FieldInfo _splitIdNextField;

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
		Log.LogInfo((object)("FikaInventoryResync v1.3.1 loaded. Enabled=" + Enabled.Value + " Cooldown=" + CooldownSeconds.Value.ToString("0.##") + "s"));
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
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Expected O, but got Unknown
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_0332: Expected O, but got Unknown
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		//IL_044c: Expected O, but got Unknown
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
		}
		else
		{
			val.Patch((MethodBase)methodInfo5, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "OnNetworkReceive_Transpiler", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null);
			Log.LogInfo((object)"Patch3 OK: host inventory-operation de-duplication transpiler on FikaServer::OnNetworkReceive");
		}
		Type type2 = AccessTools.TypeByName("EFT.SplitOperationDescriptor");
		Type type3 = ((type2 == null) ? null : type2.GetNestedType("SplitIdGenerator", BindingFlags.Public | BindingFlags.NonPublic));
		if (type3 == null)
		{
			Log.LogError((object)"Patch4 SKIPPED: EFT.SplitOperationDescriptor/SplitIdGenerator not found.");
		}
		else
		{
			_splitIdNextField = AccessTools.Field(type3, "_nextId");
			MethodInfo methodInfo6 = AccessTools.Method(type3, "get_NextId", (Type[])null, (Type[])null);
			if (methodInfo6 == null || _splitIdNextField == null)
			{
				Log.LogError((object)("Patch4 SKIPPED: get_NextId/_nextId not found on " + type3.FullName));
			}
			else
			{
				val.Patch((MethodBase)methodInfo6, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "SplitIdGenerator_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				Log.LogInfo((object)("Patch4 OK: split replay id generator now advances (was returning a constant) on " + type3.FullName + "::get_NextId"));
			}
		}
		Type type4 = AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedFirearmController");
		if (type4 == null)
		{
			Log.LogError((object)"Patch5/6 SKIPPED: ObservedFirearmController not found.");
			return;
		}
		_weaponManagerField = AccessTools.Field(type4, "_weaponManager");
		_objectInHandsAnimatorField = AccessTools.Field(type4, "_objectInHandsAnimator") ?? AccessTools.Field(type4.BaseType, "_objectInHandsAnimator");
		_weaponPrefabField = AccessTools.Field(type4, "_weaponPrefab") ?? AccessTools.Field(type4.BaseType, "_weaponPrefab");
		if (AccessTools.Method(type4, "Start", (Type[])null, (Type[])null) == null)
		{
			Log.LogError((object)"Patch5 SKIPPED: ObservedFirearmController.Start not found.");
		}
		MethodInfo methodInfo7 = AccessTools.Method(type4, "IEventsConsumerOnShellEject", (Type[])null, (Type[])null);
		if (methodInfo7 == null)
		{
			Log.LogError((object)"Patch6 SKIPPED: IEventsConsumerOnShellEject not found.");
			return;
		}
		val.Patch((MethodBase)methodInfo7, new HarmonyMethod(AccessTools.Method(typeof(FikaInventoryResyncPlugin), "ShellEject_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Log.LogInfo((object)"Patch6 OK: shell-eject is skipped when _weaponManager is null (stops the NullReferenceException spam and the aborted animation-event chain).");
	}

	private static Exception FirearmStart_Finalizer(Exception __exception)
	{
		if (__exception != null)
		{
			Log.LogError((object)("[diag] ObservedFirearmController.Start() THREW -- _weaponManager will stay null and every shell-eject event will NullReferenceException from now on. Cause: " + __exception));
		}
		return null;
	}

	private static void FirearmStart_Postfix(ObservedFirearmController __instance)
	{
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled.Value || _weaponManagerField == null)
		{
			return;
		}
		try
		{
			if (_weaponManagerField.GetValue(__instance) != null)
			{
				return;
			}
			int instanceID = ((Object)__instance).GetInstanceID();
			if (_nullManagerReported.Count > 500 || !_nullManagerReported.Add(instanceID))
			{
				return;
			}
			object obj = _objectInHandsAnimatorField?.GetValue(__instance);
			object obj2 = _weaponPrefabField?.GetValue(__instance);
			string text = "<n/a>";
			if (obj2 != null)
			{
				object obj3 = obj2.GetType().GetField("ObjectInHands", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj2);
				text = ((obj3 == null) ? "NULL" : obj3.GetType().Name);
			}
			string text2 = "?";
			try
			{
				Weapon weapon = ((FirearmController)__instance).Weapon;
				if (weapon != null)
				{
					text2 = ((object)((Item)weapon).TemplateId/*cast due to constrained. prefix*/).ToString();
				}
			}
			catch
			{
			}
			Log.LogError((object)("[diag] _weaponManager is NULL after Start() -- this is the root of the IEventsConsumerOnShellEject NullReferenceException. weaponTemplate=" + text2 + "  _objectInHandsAnimator=" + ((obj == null) ? "NULL" : "ok") + "  _weaponPrefab=" + ((obj2 == null) ? "NULL" : "ok") + "  ObjectInHands=" + text));
		}
		catch (Exception ex)
		{
			Log.LogError((object)("[diag] FirearmStart_Postfix error: " + ex.Message));
		}
	}

	private static bool ShellEject_Prefix(ObservedFirearmController __instance)
	{
		if (!Enabled.Value || _weaponManagerField == null)
		{
			return true;
		}
		try
		{
			if (_weaponManagerField.GetValue(__instance) != null)
			{
				return true;
			}
		}
		catch
		{
			return true;
		}
		_shellEjectSkipped++;
		if (Verbose.Value && (_shellEjectSkipped == 1 || _shellEjectSkipped % 500 == 0))
		{
			Log.LogWarning((object)("[diag] skipped " + _shellEjectSkipped + " shell-eject events whose _weaponManager was null (these were the repeating NullReferenceException entries in the log)."));
		}
		return false;
	}

	private static bool SplitIdGenerator_Prefix(object __instance, ref MongoID __result)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled.Value || _splitIdNextField == null)
		{
			return true;
		}
		try
		{
			MongoID val = (MongoID)_splitIdNextField.GetValue(__instance);
			MongoID val2 = val;
			val2 = MongoID.op_Increment(val2);
			_splitIdNextField.SetValue(__instance, val2);
			__result = val;
			return false;
		}
		catch (Exception ex)
		{
			Log.LogError((object)("Patch4 prefix error: " + ex.Message));
			return true;
		}
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
