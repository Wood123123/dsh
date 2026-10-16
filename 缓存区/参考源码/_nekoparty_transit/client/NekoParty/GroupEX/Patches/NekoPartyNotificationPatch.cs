using System;
using System.Reflection;
using Comfort.Common;
using Diz.Utils;
using EFT.Communications;
using EFT.UI;
using Fika.Core.Main.Utils;
using HarmonyLib;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.State;
using NekoParty.GroupEX.UI;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyNotificationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.TypeByName("Fika.Core.Networking.Websocket.FikaNotificationManager")?.GetMethod("WebSocket_OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	private static bool Prefix(MessageEventArgs e)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Expected O, but got Unknown
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected O, but got Unknown
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Expected O, but got Unknown
		if (e == null || string.IsNullOrEmpty(e.Data))
		{
			return true;
		}
		JObject val;
		try
		{
			val = JObject.Parse(e.Data);
		}
		catch
		{
			return true;
		}
		string text = ((JToken)val).Value<string>((object)"nekoType");
		if (string.IsNullOrEmpty(text))
		{
			return true;
		}
		switch (text)
		{
		case "nekoparty.partyInviteReceived":
		{
			if (NekoGroupExConfig.UseNativePartyInvite)
			{
				return true;
			}
			NekoPartyInviteReceivedNotification nekoPartyInviteReceivedNotification = ((JToken)val).ToObject<NekoPartyInviteReceivedNotification>();
			NekoGroupState.PendingPartyInvites[nekoPartyInviteReceivedNotification.InviteId] = nekoPartyInviteReceivedNotification;
			ShowNotification((Notification)nekoPartyInviteReceivedNotification);
			break;
		}
		case "nekoparty.raidInviteReceived":
		{
			NekoRaidInviteReceivedNotification nekoRaidInviteReceivedNotification = ((JToken)val).ToObject<NekoRaidInviteReceivedNotification>();
			FikaGlobals.LogWarning("[NekoP2P][Diag] WS收到战局邀请 leader=" + nekoRaidInviteReceivedNotification.LeaderNickname + "/" + nekoRaidInviteReceivedNotification.LeaderProfileId + " serverId=" + nekoRaidInviteReceivedNotification.ServerId + " loc=" + nekoRaidInviteReceivedNotification.Location + " inv=" + nekoRaidInviteReceivedNotification.InviteId, "RaidInviteDiag");
			NekoGroupState.PendingRaidInvites[nekoRaidInviteReceivedNotification.InviteId] = nekoRaidInviteReceivedNotification;
			ShowNotification((Notification)nekoRaidInviteReceivedNotification);
			break;
		}
		case "nekoparty.partyKicked":
		{
			NekoPartyKickedNotification nekoPartyKickedNotification = ((JToken)val).ToObject<NekoPartyKickedNotification>();
			NekoGroupState.ClearPartyState();
			RefreshUi();
			ShowNotification((Notification)nekoPartyKickedNotification);
			break;
		}
		case "nekoparty.partyDisbanded":
		{
			NekoPartyDisbandedNotification nekoPartyDisbandedNotification = ((JToken)val).ToObject<NekoPartyDisbandedNotification>();
			NekoGroupState.ClearPartyState();
			RefreshUi();
			ShowNotification((Notification)nekoPartyDisbandedNotification);
			break;
		}
		case "nekoparty.partyMemberLeft":
		{
			NekoPartyMemberLeftNotification nekoPartyMemberLeftNotification = ((JToken)val).ToObject<NekoPartyMemberLeftNotification>();
			RefreshUi();
			ShowNotification((Notification)nekoPartyMemberLeftNotification);
			break;
		}
		case "nekoparty.modMismatch":
		{
			NekoModMismatchNotification notification = ((JToken)val).ToObject<NekoModMismatchNotification>();
			AsyncWorker.RunInMainTread((Action)delegate
			{
				NekoModMismatchWindow.Show(notification);
			});
			break;
		}
		default:
			return true;
		}
		return false;
	}

	private static void ShowNotification(Notification notification)
	{
		AsyncWorker.RunInMainTread((Action)delegate
		{
			if (Singleton<PreloaderUI>.Instantiated)
			{
				Singleton<PreloaderUI>.Instance.NotifierView.ShowNotification(notification);
			}
		});
	}

	private static void RefreshUi()
	{
		AsyncWorker.RunInMainTread((Action)delegate
		{
			if (NekoPartyMainMenuController.Exists)
			{
				NekoPartyMainMenuController.Instance.RefreshPartyStateFromExternalChange();
			}
			if (NekoPartyMatchMakerController.Exists)
			{
				NekoPartyMatchMakerController.Instance.RefreshPartyStateFromExternalChange();
			}
		});
	}
}
