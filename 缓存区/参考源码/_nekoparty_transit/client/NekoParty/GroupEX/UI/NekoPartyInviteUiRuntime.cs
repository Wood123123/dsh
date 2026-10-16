using System;
using System.Linq;
using EFT;
using EFT.UI;
using JsonType;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Notifications;
using UnityEngine;

namespace NekoParty.GroupEX.UI;

internal static class NekoPartyInviteUiRuntime
{
	public static bool TryShowNativePartyInvitePopup(NekoPartyInviteReceivedNotification invite, Action acceptAction, Action declineAction)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		if (invite == null || (Object)ItemUiContext.Instance == (Object)null)
		{
			return false;
		}
		GroupInvite val = CreateNativeGroupInvite(invite);
		if (val == null)
		{
			return false;
		}
		bool handled = false;
		DialogWindowContext obj = ItemUiContext.Instance.ShowInviteInGroupWindow(val);
		((BaseWindowContext<bool>)(object)obj).OnAccept += delegate
		{
			if (!handled)
			{
				handled = true;
				acceptAction?.Invoke();
			}
		};
		((BaseWindowContext<bool>)(object)obj).OnDecline += delegate
		{
			if (!handled)
			{
				handled = true;
				declineAction?.Invoke();
			}
		};
		((BaseWindowContext<bool>)(object)obj).OnCloseSilent += delegate
		{
			if (!handled)
			{
				handled = true;
				declineAction?.Invoke();
			}
		};
		return true;
	}

	private static GroupInvite CreateNativeGroupInvite(NekoPartyInviteReceivedNotification invite)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Expected O, but got Unknown
		GroupPlayer val = CreateGroupPlayer(invite.FromProfile, invite.LeaderProfileId, invite.LeaderNickname, fallbackIsLeader: true);
		if (val == null)
		{
			return null;
		}
		GroupPlayer[] array = invite.Members?.Select((NekoPartyInvitePlayerData member) => CreateGroupPlayer(member, member.ProfileId, member.Nickname, member.IsLeader)).Where((GroupPlayer member) => member != null).ToArray();
		if (array == null || array.Length == 0)
		{
			array = (GroupPlayer[])(object)new GroupPlayer[1] { val };
		}
		return new GroupInvite
		{
			Id = (string.IsNullOrWhiteSpace(invite.PartyId) ? invite.InviteId : invite.PartyId),
			RequestId = invite.InviteId,
			From = invite.LeaderProfileId,
			To = invite.TargetProfileId,
			FromProfile = val,
			Members = array
		};
	}

	private static GroupPlayer CreateGroupPlayer(NekoPartyInvitePlayerData player, string fallbackProfileId, string fallbackNickname, bool fallbackIsLeader)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Expected O, but got Unknown
		//IL_010a: Expected O, but got Unknown
		string text = ((!string.IsNullOrWhiteSpace(player.ProfileId)) ? player.ProfileId : fallbackProfileId);
		string text2 = ((!string.IsNullOrWhiteSpace(player.Nickname)) ? player.Nickname : fallbackNickname);
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			return null;
		}
		return new GroupPlayer
		{
			AccountId = ((!string.IsNullOrWhiteSpace(player.AccountId)) ? player.AccountId : text),
			Id = text,
			IsLeader = ((player.ProfileId != null) ? player.IsLeader : fallbackIsLeader),
			IsReady = (player.ProfileId == null || player.IsReady),
			Info = new PlayerInfo
			{
				Nickname = text2,
				Side = (EPlayerSide)((player.Side != 1) ? 1 : 4),
				Level = ((player.Level <= 0) ? 1 : player.Level),
				PrestigeLevel = Math.Max(0, player.PrestigeLevel),
				MemberCategory = (EMemberCategory)Math.Max(0, player.MemberCategory),
				SelectedMemberCategory = (EMemberCategory)Math.Max(0, player.SelectedMemberCategory)
			}
		};
	}
}
