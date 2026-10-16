using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using HarmonyLib;
using JsonType;
using NekoParty.GroupEX.Compat;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.State;
using TMPro;
using UnityEngine;

namespace NekoParty.GroupEX.MixedSide;

internal static class NekoPartyMixedSideRuntime
{
	public static HashSet<string> GetLocalProfileIds()
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		try
		{
			Profile profile = FikaBackendUtils.Profile;
			string text = ((profile != null) ? profile.ProfileId : null);
			if (!string.IsNullOrEmpty(text))
			{
				hashSet.Add(text);
			}
		}
		catch
		{
		}
		try
		{
			TarkovApplication val = null;
			if (TarkovApplication.Exist(ref val) && ((ClientApplication<IEftSession>)(object)val).Session != null)
			{
				IEftSession session = ((ClientApplication<IEftSession>)(object)val).Session;
				ESideType[] array = (ESideType[])(object)new ESideType[2]
				{
					default(ESideType),
					(ESideType)1
				};
				foreach (ESideType side in array)
				{
					try
					{
						Profile profileBySide = NekoSessionCompat.GetProfileBySide(session, side);
						string text2 = ((profileBySide != null) ? profileBySide.ProfileId : null);
						if (!string.IsNullOrEmpty(text2))
						{
							hashSet.Add(text2);
						}
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	public static bool IsLocalProfileId(string profileId)
	{
		if (!string.IsNullOrEmpty(profileId))
		{
			return GetLocalProfileIds().Contains(profileId);
		}
		return false;
	}

	public static int GetCurrentPreferredSide()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected I4, but got Unknown
		try
		{
			TarkovApplication val = null;
			if (TarkovApplication.Exist(ref val))
			{
				RaidSettings val2 = Traverse.Create((object)val).Field("_menuOperation").GetValue<MainMenuShowOperation>()?.raidSettings_0;
				if (val2 != null)
				{
					return (int)val2.Side;
				}
			}
		}
		catch
		{
		}
		return NormalizeSide(NekoGroupState.PreferredLocalSide);
	}

	public static void SyncCurrentPreferredSideBestEffort()
	{
		int preferredSide = (NekoGroupState.PreferredLocalSide = GetCurrentPreferredSide());
		try
		{
			NekoGroupRequestHandler.SyncPreferredSide(preferredSide);
		}
		catch
		{
		}
	}

	public static bool OpenInviteSideSelection(NekoRaidInviteReceivedNotification invite)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		TarkovApplication val = null;
		if (!TarkovApplication.Exist(ref val) || !MonoBehaviourSingleton<MenuUI>.Instantiated)
		{
			return false;
		}
		MainMenuShowOperation value = Traverse.Create((object)val).Field("_menuOperation").GetValue<MainMenuShowOperation>();
		if (value == null)
		{
			return false;
		}
		RaidSettings raidSettings_ = value.raidSettings_0;
		RaidSettings raidSettings_2 = value.raidSettings_1;
		if (raidSettings_ == null || raidSettings_2 == null)
		{
			return false;
		}
		ESideType inviteDefaultSide = GetInviteDefaultSide(invite);
		ConfigureInviteRaidSettings(raidSettings_, invite, inviteDefaultSide);
		ConfigureInviteRaidSettings(raidSettings_2, invite, inviteDefaultSide);
		NekoGroupState.PendingAcceptedRaidInvite = invite;
		NekoGroupState.PendingAcceptedRaidInvitePreferredSide = -1;
		Traverse.Create(typeof(FikaBackendUtils)).Property("InviteInProgressId", (object[])null).SetValue((object)invite.InviteId);
		SetFikaPendingAcceptedRaidInvite(invite, inviteDefaultSide);
		value.method_44();
		return true;
	}

	public static ESideType GetInvitePreviewSide(NekoRaidInviteReceivedNotification invite)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return GetInviteDefaultSide(invite);
	}

	public static bool ShouldAutoAdvanceInviteFlow(RaidSettings raidSettings)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Invalid comparison between Unknown and I4
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected I4, but got Unknown
		NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
		if (pendingAcceptedRaidInvite == null || raidSettings == null)
		{
			return false;
		}
		int num = NormalizeSide(NekoGroupState.PendingAcceptedRaidInvitePreferredSide);
		if (string.Equals(raidSettings.LocationId, pendingAcceptedRaidInvite.Location, StringComparison.OrdinalIgnoreCase) && (int)raidSettings.SelectedDateTime == ((pendingAcceptedRaidInvite.Time == 1) ? 1 : 0))
		{
			return NormalizeSide((int)raidSettings.Side) == num;
		}
		return false;
	}

	public static void AutoAdvanceReadyButton(MonoBehaviour owner, DefaultUIButton readyButton)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		if (!((Object)owner == (Object)null) && !((Object)readyButton == (Object)null) && !((Object)NekoGroupExPlugin.Instance == (Object)null))
		{
			((MonoBehaviour)NekoGroupExPlugin.Instance).StartCoroutine(InvokeReadyButtonNextFrame(readyButton));
		}
	}

	public static int GetPartyMemberPreferredSide(string profileId, string nickname = null)
	{
		NekoPartyData? currentParty = NekoGroupState.CurrentParty;
		if (!currentParty.HasValue || currentParty.Value.Members == null)
		{
			return 0;
		}
		NekoPartyMemberData[] members = currentParty.Value.Members;
		for (int i = 0; i < members.Length; i++)
		{
			NekoPartyMemberData nekoPartyMemberData = members[i];
			if ((!string.IsNullOrEmpty(profileId) && string.Equals(nekoPartyMemberData.ProfileId, profileId, StringComparison.Ordinal)) || (!string.IsNullOrEmpty(nickname) && string.Equals(nekoPartyMemberData.Nickname, nickname, StringComparison.Ordinal)))
			{
				return NormalizeSide(nekoPartyMemberData.PreferredSide);
			}
		}
		return 0;
	}

	public static string FormatSideTag(int preferredSide)
	{
		if (NormalizeSide(preferredSide) != 1)
		{
			return LocalizationExtensions.Localized("NKP_SideTagPmc", (string)null);
		}
		return LocalizationExtensions.Localized("NKP_SideTagScav", (string)null);
	}

	public static string FormatDisplayName(string nickname, int preferredSide)
	{
		if (string.IsNullOrWhiteSpace(nickname))
		{
			return "[" + FormatSideTag(preferredSide) + "]";
		}
		return "[" + FormatSideTag(preferredSide) + "] " + nickname;
	}

	public static string FormatPartyMemberDisplayName(NekoPartyMemberData member)
	{
		return FormatDisplayName(member.Nickname, member.PreferredSide);
	}

	public static void ClearPendingInviteSelection()
	{
		NekoGroupState.PendingAcceptedRaidInvitePreferredSide = -1;
	}

	public static bool TryConsumeAutoJoinReannounce(NekoRaidInviteReceivedNotification candidate)
	{
		try
		{
			NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
			if (pendingAcceptedRaidInvite == null || candidate == null || string.IsNullOrEmpty(candidate.InviteId) || string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId) || string.IsNullOrEmpty(candidate.ServerId))
			{
				return false;
			}
			if (!string.Equals(pendingAcceptedRaidInvite.LeaderProfileId, candidate.LeaderProfileId, StringComparison.Ordinal))
			{
				return false;
			}
			if (!string.Equals(pendingAcceptedRaidInvite.Location ?? string.Empty, candidate.Location ?? string.Empty, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (pendingAcceptedRaidInvite.Time != candidate.Time)
			{
				return false;
			}
			NekoGroupState.PendingAcceptedRaidInvite = candidate;
			NekoGroupState.RaidInviteInProgressId = candidate.InviteId;
			NekoGroupState.AutoJoinRaidInviteId = string.Empty;
			NekoGroupState.PendingRaidInvites.Remove(candidate.InviteId);
			FikaGlobals.LogWarning("[NekoP2P] 第一封流程中，静默接续真实战局 serverId=" + candidate.ServerId + " loc=" + candidate.Location, "AutoJoinReannounce");
			return true;
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P] 静默接续异常: " + ex.Message, "AutoJoinReannounce");
			return false;
		}
	}

	public static NekoRaidInviteReceivedNotification PeekAutoJoinReannounceCandidate()
	{
		try
		{
			NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
			if (pendingAcceptedRaidInvite == null || string.IsNullOrEmpty(pendingAcceptedRaidInvite.InviteId) || string.IsNullOrEmpty(pendingAcceptedRaidInvite.LeaderProfileId))
			{
				return null;
			}
			foreach (NekoRaidInviteReceivedNotification value in NekoGroupState.PendingRaidInvites.Values)
			{
				if (value != null && !string.IsNullOrEmpty(value.InviteId) && !string.IsNullOrEmpty(value.ServerId) && !NekoGroupState.SuppressedRaidInviteIds.Contains(value.InviteId) && string.Equals(pendingAcceptedRaidInvite.LeaderProfileId, value.LeaderProfileId, StringComparison.Ordinal) && string.Equals(pendingAcceptedRaidInvite.Location ?? string.Empty, value.Location ?? string.Empty, StringComparison.OrdinalIgnoreCase) && pendingAcceptedRaidInvite.Time == value.Time)
				{
					return value;
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	public static void PrepareInviteRaidSettings(RaidSettings raidSettings, NekoRaidInviteReceivedNotification invite)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (raidSettings != null && invite != null)
		{
			ConfigureInviteRaidSettings(raidSettings, invite, GetInviteDefaultSide(invite));
		}
	}

	public static void PatchNativeInviteWindowText(NekoRaidInviteReceivedNotification invite)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		if (invite != null && !((Object)NekoGroupExPlugin.Instance == (Object)null))
		{
			((MonoBehaviour)NekoGroupExPlugin.Instance).StartCoroutine(PatchNativeInviteWindowTextCoroutine(invite));
		}
	}

	public static void ApplyInviteSelectedSide(ESideType selectedSide)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Invalid comparison between Unknown and I4
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected I4, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected I4, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		TarkovApplication val = null;
		if (NekoGroupState.PendingAcceptedRaidInvite != null && !string.IsNullOrEmpty(NekoGroupState.PendingAcceptedRaidInvite.InviteId) && TarkovApplication.Exist(ref val))
		{
			MainMenuShowOperation value = Traverse.Create((object)val).Field("_menuOperation").GetValue<MainMenuShowOperation>();
			object obj;
			if (value == null)
			{
				obj = null;
			}
			else
			{
				IEftSession menuSession = NekoSessionCompat.GetMenuSession(value);
				obj = ((menuSession != null) ? NekoSessionCompat.GetProfileBySide(menuSession, selectedSide) : null);
			}
			Profile val2 = (Profile)obj;
			if (val2 != null)
			{
				Traverse.Create(typeof(FikaBackendUtils)).Property("Profile", (object[])null).SetValue((object)val2);
			}
			Traverse.Create(typeof(FikaBackendUtils)).Property("IsScav", (object[])null).SetValue((object)((int)selectedSide == 1));
			NekoGroupState.PendingAcceptedRaidInvitePreferredSide = (int)selectedSide;
			NekoGroupState.PreferredLocalSide = (int)selectedSide;
			value.raidSettings_0.Side = selectedSide;
			value.raidSettings_1.Side = selectedSide;
			SetFikaPendingAcceptedRaidInvite(NekoGroupState.PendingAcceptedRaidInvite, selectedSide);
			SyncCurrentPreferredSideBestEffort();
		}
	}

	public static bool RouteInviteSideSelectionNext(MatchMakerSideSelectionScreen screen)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
		if ((Object)screen == (Object)null || pendingAcceptedRaidInvite == null || string.IsNullOrEmpty(pendingAcceptedRaidInvite.InviteId))
		{
			return false;
		}
		TarkovApplication val = null;
		if (!TarkovApplication.Exist(ref val))
		{
			return false;
		}
		MainMenuShowOperation value = Traverse.Create((object)val).Field("_menuOperation").GetValue<MainMenuShowOperation>();
		if (value == null || (Object)NekoGroupExPlugin.Instance == (Object)null)
		{
			return false;
		}
		ESideType value2 = Traverse.Create((object)screen).Field("_sideType").GetValue<ESideType>();
		Traverse.Create(Traverse.Create((object)screen).Property("ScreenController", (object[])null).GetValue<object>()).Method("UpdateSideSelection", new object[1] { value2 }).GetValue();
		ApplyInviteSelectedSide(value2);
		ConfigureInviteRaidSettings(value.raidSettings_0, pendingAcceptedRaidInvite, value2);
		ConfigureInviteRaidSettings(value.raidSettings_1, pendingAcceptedRaidInvite, value2);
		((MonoBehaviour)NekoGroupExPlugin.Instance).StartCoroutine(RouteInviteSideSelectionNextFrame(value, value2));
		return true;
	}

	public static void ClearFikaPendingAcceptedRaidInvite(string inviteId = null)
	{
		object value = Traverse.Create(typeof(FikaBackendUtils)).Property("PendingAcceptedRaidInvite", (object[])null).GetValue<object>();
		if (value != null && (string.IsNullOrEmpty(inviteId) || string.Equals(Traverse.Create(value).Field("InviteId").GetValue<string>(), inviteId, StringComparison.Ordinal)))
		{
			Traverse.Create(typeof(FikaBackendUtils)).Property("PendingAcceptedRaidInvite", (object[])null).SetValue((object)null);
		}
	}

	public static void ClearFikaInviteInProgress(string inviteId = null)
	{
		string value = Traverse.Create(typeof(FikaBackendUtils)).Property("InviteInProgressId", (object[])null).GetValue<string>();
		if (string.IsNullOrEmpty(inviteId) || string.Equals(value, inviteId, StringComparison.Ordinal))
		{
			Traverse.Create(typeof(FikaBackendUtils)).Property("InviteInProgressId", (object[])null).SetValue((object)string.Empty);
		}
	}

	public static bool HasPendingInviteSelection()
	{
		if (NekoGroupState.PendingAcceptedRaidInvite != null)
		{
			return !string.IsNullOrEmpty(NekoGroupState.PendingAcceptedRaidInvite.InviteId);
		}
		return false;
	}

	private static ESideType GetInviteDefaultSide(NekoRaidInviteReceivedNotification invite)
	{
		if (invite != null)
		{
			if (invite.TargetSide != 1)
			{
				return (ESideType)0;
			}
			return (ESideType)1;
		}
		if (GetCurrentPreferredSide() != 1)
		{
			return (ESideType)0;
		}
		return (ESideType)1;
	}

	private static void ConfigureInviteRaidSettings(RaidSettings raidSettings, NekoRaidInviteReceivedNotification invite, ESideType selectedSide)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		raidSettings.RaidMode = (ERaidMode)2;
		raidSettings.Side = selectedSide;
		raidSettings.SelectedDateTime = (EDateTime)(invite.Time == 1);
		raidSettings.LocationId = invite.Location;
		LocationSettings locationSettings = NekoSessionCompat.GetLocationSettings();
		if (locationSettings?.locations != null)
		{
			raidSettings.SelectedLocation = locationSettings.locations.Values.FirstOrDefault((Location x) => string.Equals(x.Id, invite.Location, StringComparison.OrdinalIgnoreCase));
		}
	}

	private static IEnumerator InvokeReadyButtonNextFrame(DefaultUIButton readyButton)
	{
		yield return null;
		if ((Object)readyButton != (Object)null)
		{
			readyButton.OnClick.Invoke();
		}
	}

	private static IEnumerator RouteInviteSideSelectionNextFrame(MainMenuShowOperation mainMenuController, ESideType selectedSide)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		yield return null;
		if (mainMenuController != null)
		{
			if ((int)selectedSide == 1)
			{
				mainMenuController.method_52();
			}
			else
			{
				mainMenuController.method_51();
			}
		}
	}

	private static IEnumerator PatchNativeInviteWindowTextCoroutine(NekoRaidInviteReceivedNotification invite)
	{
		string replacement = string.Format(LocalizationExtensions.Localized("NKP_RaidInviteNativePopup", (string)null), invite.LeaderNickname, LocalizationExtensions.Localized(invite.Location, (string)null));
		for (int attempt = 0; attempt < 12; attempt++)
		{
			yield return null;
			if ((Object)ItemUiContext.Instance == (Object)null)
			{
				continue;
			}
			bool flag = false;
			TextMeshProUGUI[] componentsInChildren = ((Component)ItemUiContext.Instance).GetComponentsInChildren<TextMeshProUGUI>(true);
			foreach (TextMeshProUGUI val in componentsInChildren)
			{
				if (!((Object)val == (Object)null) && !string.IsNullOrWhiteSpace(((TMP_Text)val).text) && ((TMP_Text)val).text.Contains(invite.LeaderNickname, StringComparison.Ordinal) && (((TMP_Text)val).text.Contains(LocalizationExtensions.Localized(invite.Location, (string)null), StringComparison.Ordinal) || ((TMP_Text)val).text.Contains("PMC", StringComparison.OrdinalIgnoreCase) || ((TMP_Text)val).text.Contains("SCAV", StringComparison.OrdinalIgnoreCase) || ((TMP_Text)val).text.Contains("加入", StringComparison.Ordinal) || ((TMP_Text)val).text.Contains("join", StringComparison.OrdinalIgnoreCase)))
				{
					((TMP_Text)val).text = replacement;
					flag = true;
				}
			}
			if (flag)
			{
				break;
			}
		}
	}

	private static int NormalizeSide(int preferredSide)
	{
		return (preferredSide == 1) ? 1 : 0;
	}

	private static void SetFikaPendingAcceptedRaidInvite(NekoRaidInviteReceivedNotification invite, ESideType selectedSide)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		object obj = CreateFikaRaidInviteObject(invite, selectedSide);
		if (obj != null)
		{
			Traverse.Create(typeof(FikaBackendUtils)).Property("PendingAcceptedRaidInvite", (object[])null).SetValue(obj);
		}
	}

	private static object CreateFikaRaidInviteObject(NekoRaidInviteReceivedNotification invite, ESideType selectedSide)
	{
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Invalid comparison between Unknown and I4
		if (invite == null)
		{
			return null;
		}
		Type type = typeof(FikaBackendUtils).Assembly.GetType("Fika.Core.Networking.Websocket.Notifications.RaidInviteReceivedNotification", throwOnError: false);
		if (type == null)
		{
			return null;
		}
		object obj = Activator.CreateInstance(type);
		if (obj == null)
		{
			return null;
		}
		Traverse obj2 = Traverse.Create(obj);
		obj2.Field("InviteId").SetValue((object)invite.InviteId);
		obj2.Field("PartyId").SetValue((object)invite.PartyId);
		obj2.Field("LeaderProfileId").SetValue((object)invite.LeaderProfileId);
		obj2.Field("LeaderNickname").SetValue((object)invite.LeaderNickname);
		obj2.Field("TargetProfileId").SetValue((object)invite.TargetProfileId);
		obj2.Field("ServerId").SetValue((object)invite.ServerId);
		obj2.Field("RaidCode").SetValue((object)invite.RaidCode);
		obj2.Field("Location").SetValue((object)invite.Location);
		obj2.Field("Side").SetValue((object)(((int)selectedSide == 1) ? 1 : 0));
		obj2.Field("Time").SetValue((object)invite.Time);
		obj2.Field("ExpiresAtUnix").SetValue((object)invite.ExpiresAtUnix);
		return obj;
	}
}
