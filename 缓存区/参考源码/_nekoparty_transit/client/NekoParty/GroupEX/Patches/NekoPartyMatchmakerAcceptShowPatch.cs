using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using HarmonyLib;
using JsonType;
using NekoParty.GroupEX.Compat;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.State;
using NekoParty.GroupEX.UI;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyMatchmakerAcceptShowPatch
{
	private static MethodBase TargetMethod()
	{
		return typeof(MatchMakerAcceptScreen).GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo x) => x.Name == "Show" && x.GetParameters()[0].Name == "session");
	}

	private static void Prefix(MatchMakerAcceptScreen __instance, ref RaidSettings raidSettings, DefaultUIButton ____acceptButton, DefaultUIButton ____backButton)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected I4, but got Unknown
		TryLogPreviewMembers(__instance);
		Traverse.Create(typeof(FikaBackendUtils)).Property("IsScav", (object[])null).SetValue((object)raidSettings.IsScav);
		NekoGroupState.PreferredLocalSide = (int)raidSettings.Side;
		try
		{
			NekoGroupRequestHandler.SyncPreferredSide(NekoGroupState.PreferredLocalSide);
		}
		catch
		{
		}
		NekoPartyMatchMakerController obj2 = ((Component)__instance).gameObject.GetComponent<NekoPartyMatchMakerController>() ?? ((Component)__instance).gameObject.AddComponent<NekoPartyMatchMakerController>();
		obj2.RaidSettings = raidSettings;
		obj2.AcceptButton = ____acceptButton;
		obj2.BackButton = ____backButton;
		obj2.AcceptScreen = __instance;
		if (NekoGroupExConfig.UseNativePartyInvite)
		{
			NekoPartyPreparePreview.EnsurePartyRaidLocation(raidSettings);
		}
	}

	private static void Postfix(MatchMakerAcceptScreen __instance, ref IEftSession session, ref RaidSettings raidSettings)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		IEftSession val = session;
		object obj = ((val != null) ? NekoSessionCompat.GetProfileBySide(val, raidSettings.Side) : null);
		if (obj == null)
		{
			IEftSession val2 = session;
			obj = ((val2 != null) ? ((IProfileSession)val2).Profile : null);
		}
		Profile val3 = (Profile)obj;
		if (val3 != null)
		{
			Traverse.Create(typeof(FikaBackendUtils)).Property("Profile", (object[])null).SetValue((object)val3);
			FikaGlobals.LogWarning("[NekoP2P][Diag] Show.Postfix 本地身份 aid=" + val3.AccountId + " nick=" + val3.Nickname + " side=" + ((object)val3.Side/*cast due to constrained. prefix*/).ToString() + " raidSide=" + ((raidSettings != null) ? ((object)raidSettings.Side/*cast due to constrained. prefix*/).ToString() : "null"), "AcceptIdentityDiag");
			NekoPartyMatchMakerController component = ((Component)__instance).GetComponent<NekoPartyMatchMakerController>();
			component?.EnsurePartyPanelAttached();
			component?.ScheduleVisualEnsure(__instance);
		}
	}

	private static void TryLogPreviewMembers(MatchMakerAcceptScreen screen)
	{
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			MatchmakerPlayersController controller = NekoPartyPreparePreview.GetController(screen);
			if (controller == null)
			{
				return;
			}
			int num = 0;
			foreach (RaidPlayer groupPlayer in ((BaseMatchmakerController<RaidSettings>)(object)controller).GroupPlayers)
			{
				num++;
				if (groupPlayer == null)
				{
					FikaGlobals.LogWarning($"[NekoP2P] PreviewMember#{num} = NULL", "AcceptPreviewDiag");
					continue;
				}
				GroupPlayer val = (GroupPlayer)(object)groupPlayer;
				PlayerInfo info = val.Info;
				PlayerVisualRepresentation playerVisualRepresentation = val.PlayerVisualRepresentation;
				PlayerVisualRepresentationDescriptor playerVisualRepresentation2 = val._playerVisualRepresentation;
				int num2 = ((playerVisualRepresentation2?.Equipment?._items != null) ? playerVisualRepresentation2.Equipment._items.Length : (-1));
				int num3 = ((playerVisualRepresentation2?.Customization != null) ? playerVisualRepresentation2.Customization.Count : (-1));
				FikaGlobals.LogWarning($"[NekoP2P] PreviewMember#{num} id={val.Id} accountId={val.AccountId} nick={info?.Nickname} savage={info?.SavageNickname} side={info?.Side} pvNull={playerVisualRepresentation == null} descNull={playerVisualRepresentation2 == null} equipItems={num2} customParts={num3} equipId={playerVisualRepresentation2?.Equipment?._id}", "AcceptPreviewDiag");
			}
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P] PreviewMember diag fail: " + ex.Message, "AcceptPreviewDiag");
		}
	}

	private static Exception Finalizer(MatchMakerAcceptScreen __instance, Exception __exception, IEftSession session, RaidSettings raidSettings)
	{
		return __exception;
	}
}
