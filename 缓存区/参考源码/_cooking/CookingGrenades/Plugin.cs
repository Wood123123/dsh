using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using CookingGrenades.Config;
using CookingGrenades.Patches;
using CookingGrenades.Utils;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades;

[BepInPlugin("com.Tangh.CookingGrenades", "CookingGrenades", "1.4.2")]
[BepInDependency("com.SPT.core", "3.11.0")]
public class Plugin : BaseUnityPlugin
{
	internal static ManualLogSource log;

	internal static volatile float NextCookTime;

	private static Harmony _fikaHarmony;

	private void Awake()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		log = ((BaseUnityPlugin)this).Logger;
		ConfigManager.Init(((BaseUnityPlugin)this).Config);
		ConfigEventHandler.Init();
		FuseTimeTester.Init();
		GameObject val = new GameObject("GrenadeWheel");
		Object.DontDestroyOnLoad((Object)val);
		val.AddComponent<GrenadeWheel>();
		GameObject val2 = new GameObject("MedicineWheel");
		Object.DontDestroyOnLoad((Object)val2);
		val2.AddComponent<MedicineWheel>();
		TryEnablePatch<GrenadeInitPatch>();
		TryEnablePatch<PlayerGrenadeHandsControllerHandleFireInputPatch>();
		TryEnablePatch<PlayerGrenadeHandsControllerHandleAltFireInputPatch>();
		TryEnablePatch<BaseSoundPlayerOnSoundAtPointPatch>();
		TryEnablePatch<EftGamePlayerOwnerTranslateCommandPatch>();
		TryEnablePatch<ThrowWeapGetExplDelayPatch>();
		TryEnablePatch<GrenadeWheelInputPatch>();
		TryEnablePatch<GrenadeSelectorPatch>();
		TryEnablePatch<SetNewTopPriorityGrenadePatch>();
		TryEnablePatch<MedicineWheelInputPatch>();
		TryEnablePatch<GamePlayerOwnerTranslateAxesPatch>();
		TryEnablePatch<TrajectoryRendererGameWorldStartedPatch>();
		TryEnablePatch<TrajectoryRendererPlayerDisposePatch>();
		if (!ConfigManager.UserWarningConfirmed.Value)
		{
			TryEnablePatch<MenuScreenPatch>();
		}
		TryInitFikaPatches();
	}

	private static void TryEnablePatch<T>() where T : ModulePatch, new()
	{
		try
		{
			((ModulePatch)new T()).Enable();
			log.LogInfo((object)("[Patch] " + typeof(T).Name + " 已启用"));
		}
		catch (Exception ex)
		{
			log.LogError((object)("[Patch] " + typeof(T).Name + " 启用失败: " + ex.Message));
		}
	}

	private void TryInitFikaPatches()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		try
		{
			_fikaHarmony = new Harmony("com.Tangh.CookingGrenades.fika");
			bool flag = false;
			flag |= PatchClientGrenadeThrow("Fika.Core.Main.ClientClasses.HandsControllers.FikaClientGrenadeController");
			flag |= PatchClientGrenadeThrow("Fika.Core.Main.ClientClasses.HandsControllers.FikaClientQuickGrenadeController");
			flag |= PatchGrenadePacket();
			flag |= PatchObservedSpawn("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedGrenadeController");
			flag |= PatchObservedSpawn("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedQuickGrenadeController");
			log.LogInfo((object)(flag ? "[Fika] 联机同步补丁已启用（检测到 Fika）" : "[Fika] 未检测到 Fika，联机同步已跳过"));
		}
		catch (Exception ex)
		{
			log.LogError((object)("[Fika] 初始化失败: " + ex.Message));
		}
	}

	private bool PatchClientGrenadeThrow(string typeName)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		Type type = AccessTools.TypeByName(typeName);
		if (type == null)
		{
			return false;
		}
		MethodInfo methodInfo = AccessTools.Method(type, "ThrowGrenade", (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			return false;
		}
		_fikaHarmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(FikaPatches_ClientGrenadeThrow), "Prefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		log.LogInfo((object)("[Fika] 已 patch " + typeName + ".ThrowGrenade"));
		return true;
	}

	private bool PatchGrenadePacket()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Expected O, but got Unknown
		//IL_00dd: Expected O, but got Unknown
		Type type = AccessTools.TypeByName("Fika.Core.Networking.Packets.FirearmController.SubPackets.GrenadePacket");
		if (type == null)
		{
			return false;
		}
		MethodInfo methodInfo = AccessTools.Method(type, "Serialize", (Type[])null, (Type[])null);
		MethodInfo methodInfo2 = AccessTools.Method(type, "Deserialize", (Type[])null, (Type[])null);
		MethodInfo methodInfo3 = AccessTools.Method(type, "Execute", (Type[])null, (Type[])null);
		if (methodInfo != null)
		{
			_fikaHarmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(FikaPatches_GrenadePacketSerialize), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		if (methodInfo2 != null)
		{
			_fikaHarmony.Patch((MethodBase)methodInfo2, (HarmonyMethod)null, new HarmonyMethod(typeof(FikaPatches_GrenadePacketDeserialize), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		if (methodInfo3 != null)
		{
			_fikaHarmony.Patch((MethodBase)methodInfo3, new HarmonyMethod(typeof(FikaPatches_GrenadePacketExecute), "Prefix", (Type[])null), new HarmonyMethod(typeof(FikaPatches_GrenadePacketExecute), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		log.LogInfo((object)"[Fika] 已 patch GrenadePacket Serialize/Deserialize/Execute");
		return true;
	}

	private bool PatchObservedSpawn(string typeName)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		Type type = AccessTools.TypeByName(typeName);
		if (type == null)
		{
			return false;
		}
		MethodInfo methodInfo = AccessTools.Method(type, "SpawnGrenade", (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			return false;
		}
		_fikaHarmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(FikaPatches_ObservedGrenadeSpawn), "Prefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		log.LogInfo((object)("[Fika] 已 patch " + typeName + ".SpawnGrenade"));
		return true;
	}
}
