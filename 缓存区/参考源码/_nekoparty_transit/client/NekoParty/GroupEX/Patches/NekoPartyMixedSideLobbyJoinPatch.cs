using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI;
using Fika.Core.Main.Utils;
using Fika.Core.UI.Custom;
using Fika.Core.UI.Models;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.State;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyMixedSideLobbyJoinPatch
{
	private static MethodBase TargetMethod()
	{
		return typeof(MatchMakerUIScript).GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	private static void Postfix(MatchMakerUIScript __instance)
	{
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Expected O, but got Unknown
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_044c: Unknown result type (might be due to invalid IL or missing references)
		//IL_045e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_0482: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_048b: Expected I4, but got Unknown
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Expected O, but got Unknown
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Expected O, but got Unknown
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Invalid comparison between Unknown and I4
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Invalid comparison between I4 and Unknown
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Expected O, but got Unknown
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Expected I4, but got Unknown
		NekoPartyData? party = NekoGroupState.CurrentParty;
		if (!party.HasValue || party.Value.Members == null || party.Value.Members.Length == 0)
		{
			return;
		}
		Profile profile = FikaBackendUtils.Profile;
		string text = ((profile != null) ? profile.ProfileId : null);
		if (string.IsNullOrEmpty(text) || NekoPartyMixedSideRuntime.IsLocalProfileId(party.Value.LeaderProfileId))
		{
			return;
		}
		RaidSettings value = Traverse.Create((object)__instance).Field<RaidSettings>("RaidSettings").Value;
		if (value == null)
		{
			return;
		}
		LobbyEntry[] value2 = Traverse.Create((object)__instance).Field("_matches").GetValue<LobbyEntry[]>();
		List<GameObject> value3 = Traverse.Create((object)__instance).Field("_matchesListObjects").GetValue<List<GameObject>>();
		if (value2 == null || value3 == null || value3.Count == 0)
		{
			return;
		}
		string nickname = party.Value.Members.FirstOrDefault((NekoPartyMemberData x) => string.Equals(x.ProfileId, party.Value.LeaderProfileId, StringComparison.Ordinal)).Nickname;
		NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
		bool flag = pendingAcceptedRaidInvite != null && !string.IsNullOrEmpty(pendingAcceptedRaidInvite.InviteId) && string.Equals(NekoGroupState.RaidInviteInProgressId, pendingAcceptedRaidInvite.InviteId, StringComparison.Ordinal) && !string.Equals(NekoGroupState.AutoJoinRaidInviteId, pendingAcceptedRaidInvite.InviteId, StringComparison.Ordinal);
		bool flag2 = false;
		int num = 0;
		LobbyEntry[] array = value2;
		foreach (LobbyEntry val in array)
		{
			if (string.Equals(val.ServerId, text, StringComparison.Ordinal))
			{
				continue;
			}
			if (num >= value3.Count)
			{
				break;
			}
			GameObject val2 = value3[num++];
			if ((Object)val2 == (Object)null || ((pendingAcceptedRaidInvite == null || string.IsNullOrEmpty(pendingAcceptedRaidInvite.ServerId) || !string.Equals(val.ServerId, pendingAcceptedRaidInvite.ServerId, StringComparison.Ordinal)) && (string.IsNullOrEmpty(nickname) || !string.Equals(val.HostUsername, nickname, StringComparison.Ordinal)) && (val.Players == null || !val.Players.ContainsKey(party.Value.LeaderProfileId))) || (!(value.LocationId == val.Location) && (!value.LocationId.StartsWith("sandbox", StringComparison.OrdinalIgnoreCase) || !val.Location.StartsWith("sandbox", StringComparison.OrdinalIgnoreCase))) || value.SelectedDateTime != val.Time)
			{
				continue;
			}
			Button componentInChildren = val2.GetComponentInChildren<Button>(true);
			if (!((Object)componentInChildren == (Object)null))
			{
				((Behaviour)componentInChildren).enabled = true;
				((Selectable)componentInChildren).interactable = true;
				Image component = val2.GetComponent<Image>();
				if ((Object)component != (Object)null)
				{
					((Graphic)component).color = new Color(0.17f, 0.19f, 0.22f, 0.95f);
				}
				HoverTooltipArea orAddComponent = GameObjectExtensions.GetOrAddComponent<HoverTooltipArea>(((Component)componentInChildren).gameObject);
				((Behaviour)orAddComponent).enabled = true;
				orAddComponent.SetMessageText(((int)val.Status == 1) ? LocalizationExtensions.Localized("F_UI_ReJoinRaid", (string)null) : LocalizationExtensions.Localized("F_UI_JoinRaid", (string)null), false);
				if (flag && string.Equals(pendingAcceptedRaidInvite.LeaderProfileId, party.Value.LeaderProfileId, StringComparison.Ordinal) && string.Equals(pendingAcceptedRaidInvite.Location, val.Location, StringComparison.OrdinalIgnoreCase) && pendingAcceptedRaidInvite.Time == (int)val.Time && !((Object)NekoGroupExPlugin.Instance == (Object)null))
				{
					NekoGroupState.AutoJoinRaidInviteId = pendingAcceptedRaidInvite.InviteId;
					flag2 = true;
					((MonoBehaviour)NekoGroupExPlugin.Instance).StartCoroutine(AutoInvokeJoinButtonNextFrame(componentInChildren));
					FikaGlobals.LogWarning("[NekoP2P] 自动加入命中并点击 serverId=" + val.ServerId + " host=" + val.HostUsername + " loc=" + val.Location + " time=" + (int)val.Time, "AutoJoinHit");
				}
			}
		}
		if (!flag || flag2)
		{
			return;
		}
		array = value2;
		foreach (LobbyEntry val3 in array)
		{
			if (!string.Equals(val3.ServerId, text, StringComparison.Ordinal))
			{
				FikaGlobals.LogWarning("[NekoP2P] 自动加入未命中行 serverId=" + val3.ServerId + " host=" + val3.HostUsername + " loc=" + val3.Location + " time=" + (int)val3.Time + " players=" + ((val3.Players == null) ? "null" : string.Join(",", val3.Players.Keys)), "AutoJoinMiss");
			}
		}
	}

	private static IEnumerator AutoInvokeJoinButtonNextFrame(Button joinButton)
	{
		yield return null;
		if (!((Object)joinButton == (Object)null) && ((Behaviour)joinButton).isActiveAndEnabled && ((Selectable)joinButton).interactable)
		{
			((UnityEvent)joinButton.onClick).Invoke();
		}
	}
}
