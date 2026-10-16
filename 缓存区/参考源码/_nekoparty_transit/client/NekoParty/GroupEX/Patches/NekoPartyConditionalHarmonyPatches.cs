using System;
using System.Reflection;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using NekoParty.GroupEX.Compat;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyConditionalHarmonyPatches
{
	private static MethodBase FikaMethod(string typeFullName, string methodName)
	{
		Type type = NekoFikaCompat.FindType(typeFullName);
		if (type == null)
		{
			return null;
		}
		return AccessTools.Method(type, methodName, (Type[])null, (Type[])null);
	}

	private static void TryPatch(Harmony harmony, MethodBase target, string name, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
	{
		if (target == null)
		{
			return;
		}
		try
		{
			harmony.Patch(target, prefix, postfix, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[NekoParty] 补丁 " + name + " 应用失败（当前 Fika/EFT 版本不兼容此功能点，已跳过）: " + ex.Message));
		}
	}

	private static void SafeLocalPatch(Harmony harmony, MethodBase target, string name, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
	{
		if (target == null)
		{
			return;
		}
		try
		{
			harmony.Patch(target, prefix, postfix, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[NekoParty] 补丁 " + name + " 应用失败（已跳过，避免阻断加载）: " + ex.Message));
		}
	}

	internal static void Apply(Harmony harmony)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected O, but got Unknown
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Expected O, but got Unknown
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Expected O, but got Unknown
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Expected O, but got Unknown
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Expected O, but got Unknown
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Expected O, but got Unknown
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Expected O, but got Unknown
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Expected O, but got Unknown
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Expected O, but got Unknown
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Expected O, but got Unknown
		//IL_0464: Unknown result type (might be due to invalid IL or missing references)
		//IL_046f: Expected O, but got Unknown
		//IL_0494: Unknown result type (might be due to invalid IL or missing references)
		//IL_049f: Expected O, but got Unknown
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cf: Expected O, but got Unknown
		//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ff: Expected O, but got Unknown
		//IL_0525: Unknown result type (might be due to invalid IL or missing references)
		//IL_052f: Expected O, but got Unknown
		//IL_0555: Unknown result type (might be due to invalid IL or missing references)
		//IL_055f: Expected O, but got Unknown
		//IL_0584: Unknown result type (might be due to invalid IL or missing references)
		//IL_058f: Expected O, but got Unknown
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bb: Expected O, but got Unknown
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Expected O, but got Unknown
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0435: Expected O, but got Unknown
		if (NekoGroupExConfig.DisableMatchmakerCoopPatches)
		{
			return;
		}
		if (!NekoGroupExConfig.DisableFikaUiCrossRaidPatches)
		{
			TryPatch(harmony, FikaMethod("Fika.Core.UI.Custom.MainMenuUIScript", "SetupGroupActionButton"), "SuppressFikaPartyRowButton", new HarmonyMethod(typeof(NekoPartySuppressFikaPartyRowButtonPatch), "Prefix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.UI.Custom.MatchMakerUIScript", "BuildPartyPanel"), "SuppressFikaMatchmakerPartyPanel", new HarmonyMethod(typeof(NekoPartySuppressFikaMatchmakerPartyPanelPatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableBExtraPatches && !NekoGroupExConfig.DisableNativeReadyStatusPatch)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "SetCurrentPlayerReadyStatus", (Type[])null, (Type[])null), "NativeReadyStatus", new HarmonyMethod(typeof(NekoPartyNativeReadyStatusPatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableNativeGroupStartPatch)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(TarkovApplication), "NetworkGameMatching", new Type[2]
			{
				typeof(string),
				typeof(EMatchingType)
			}, (Type[])null), "NativeGroupStart", new HarmonyMethod(typeof(NekoPartyNativeGroupStartPatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableBExtraPatches)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "TransferLeadership", (Type[])null, (Type[])null), "TransferLeadershipBlock", new HarmonyMethod(typeof(NekoPartyTransferLeadershipBlockPatch), "Prefix", (Type[])null));
		}
		SafeLocalPatch(harmony, AccessTools.PropertyGetter(typeof(MatchmakerPlayersController), "CanShowGroupPreview"), "CanShowGroupPreview", new HarmonyMethod(typeof(NekoPartyCanShowGroupPreviewPatch), "Prefix", (Type[])null));
		SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "AddPlayerToGroup", (Type[])null, (Type[])null), "GroupPlayerUpsert", new HarmonyMethod(typeof(NekoPartyGroupPlayerUpsertPatch), "Prefix", (Type[])null));
		NekoPartyVisualResiliencePatches.Apply(harmony);
		if (!NekoGroupExConfig.DisableBExtraPatches)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(BaseGroupPanel), "method_6", (Type[])null, (Type[])null), "GroupPanelRaidReadyGuard", new HarmonyMethod(typeof(NekoPartyGroupPanelRaidReadyGuardPatch), "Prefix", (Type[])null));
			SafeLocalPatch(harmony, AccessTools.PropertyGetter(typeof(MatchmakerPlayersController), "GroupIsFull"), "GroupNeverFull", new HarmonyMethod(typeof(NekoPartyGroupNeverFullPatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableMaxCountPatch)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "UpdateMaxGroupCount", (Type[])null, (Type[])null), "GroupMaxCount(Update)", null, new HarmonyMethod(typeof(NekoPartyGroupMaxCountPatch), "Postfix", (Type[])null));
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "ResetMaxGroupCount", (Type[])null, (Type[])null), "GroupMaxCount(Reset)", null, new HarmonyMethod(typeof(NekoPartyGroupMaxCountPatch), "Postfix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableShowAllMembersPatch)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(BaseGroupPanel), "AddVisibleGroupPlayers", (Type[])null, (Type[])null), "ShowAllMembers", new HarmonyMethod(typeof(NekoPartyGroupPanelShowAllMembersPatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableScavSideUnlockPatch)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchMakerSideSelectionScreen), "GetScavBlockReason", (Type[])null, (Type[])null), "ScavSideUnlock", null, new HarmonyMethod(typeof(NekoPartyScavSideUnlockPatch), "Postfix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableBExtraPatches)
		{
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MainMenuShowOperation), "method_57", (Type[])null, (Type[])null), "ScavRaidSize", new HarmonyMethod(typeof(NekoPartyScavRaidSizePatch), "Prefix", (Type[])null));
			if (typeof(MatchmakerOfflineRaidScreen).GetField("raidSettings_0", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
			{
				SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerOfflineRaidScreen), "Close", (Type[])null, (Type[])null), "RaidBotSettingsCopy", new HarmonyMethod(typeof(NekoPartyRaidBotSettingsCopyPatch), "Prefix", (Type[])null));
			}
			else
			{
				Debug.LogWarning((object)"[NekoParty] 4.1.3 字段 raidSettings_0 不存在，跳过 RaidBotSettingsCopy（不阻断加载）。");
			}
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MatchmakerPlayersController), "GetCoopBlockReason", (Type[])null, (Type[])null), "CoopBlockReason", new HarmonyMethod(typeof(NekoPartyCoopBlockReasonPatch), "Prefix", (Type[])null));
			SafeLocalPatch(harmony, AccessTools.Method(typeof(MainMenuShowOperation), "method_56", (Type[])null, (Type[])null), "GroupCoopAvailable", new HarmonyMethod(typeof(NekoPartyGroupCoopAvailablePatch), "Prefix", (Type[])null));
		}
		if (!NekoGroupExConfig.DisableFikaUiCrossRaidPatches)
		{
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaServer", "Init"), "CrossRaidBeacon(FikaServer.Init)", new HarmonyMethod(typeof(NekoPartyCrossRaidBeaconPatch), "Prefix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaPingingClient", "Init"), "CrossRaidBeacon(FikaPingingClient.Init)", new HarmonyMethod(typeof(NekoPartyCrossRaidBeaconPatch), "Prefix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.UI.Custom.MatchMakerUIScript", "JoinMatch"), "CrossRaidJoin", new HarmonyMethod(typeof(NekoPartyCrossRaidJoinPatch), "Prefix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaServer", "StopNatIntroduceRoutine"), "CrossRaidKeepPunch", new HarmonyMethod(typeof(NekoPartyCrossRaidKeepPunchPatch), "Prefix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaServer", "OnDestroy"), "CrossRaidServerDestroy", null, new HarmonyMethod(typeof(NekoPartyCrossRaidServerDestroyPatch), "Postfix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaClient", "OnPeerConnected"), "CrossRaidConnectionLedger", null, new HarmonyMethod(typeof(NekoPartyCrossRaidConnectionLedgerPatch), "Postfix", (Type[])null));
			TryPatch(harmony, FikaMethod("Fika.Core.Networking.FikaClient", "OnPeerDisconnected"), "CrossRaidRejectNotice", new HarmonyMethod(typeof(NekoPartyCrossRaidRejectNoticePatch), "Prefix", (Type[])null));
		}
	}
}
