using System.Collections.Generic;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Notifications;

namespace NekoParty.GroupEX.State;

public static class NekoGroupState
{
	public static NekoPartyData? CurrentParty { get; set; }

	public static Dictionary<string, NekoRaidInviteReceivedNotification> PendingRaidInvites { get; } = new Dictionary<string, NekoRaidInviteReceivedNotification>();

	public static Dictionary<string, NekoPartyInviteReceivedNotification> PendingPartyInvites { get; } = new Dictionary<string, NekoPartyInviteReceivedNotification>();

	public static HashSet<string> SuppressedRaidInviteIds { get; } = new HashSet<string>();

	public static string AutoJoinRaidInviteId { get; set; } = string.Empty;

	public static string RaidInviteInProgressId { get; set; } = string.Empty;

	public static string PartyInviteInProgressId { get; set; } = string.Empty;

	public static NekoRaidInviteReceivedNotification PendingAcceptedRaidInvite { get; set; }

	public static int PendingAcceptedRaidInvitePreferredSide { get; set; } = -1;

	public static int PreferredLocalSide { get; set; }

	public static HashSet<string> ConnectedRaidKeys { get; } = new HashSet<string>();

	public static void ClearPartyState()
	{
		CurrentParty = null;
		PendingPartyInvites.Clear();
		PartyInviteInProgressId = string.Empty;
		PreferredLocalSide = 0;
		ResetRaidInviteFlow();
	}

	public static void ResetRaidInviteFlow()
	{
		PendingRaidInvites.Clear();
		SuppressedRaidInviteIds.Clear();
		AutoJoinRaidInviteId = string.Empty;
		RaidInviteInProgressId = string.Empty;
		PendingAcceptedRaidInvite = null;
		PendingAcceptedRaidInvitePreferredSide = -1;
	}

	public static void PurgeExpiredRaidInvites(long nowUnix)
	{
		if (PendingRaidInvites.Count == 0)
		{
			return;
		}
		List<string> list = null;
		foreach (KeyValuePair<string, NekoRaidInviteReceivedNotification> pendingRaidInvite in PendingRaidInvites)
		{
			if (pendingRaidInvite.Value.ExpiresAtUnix > 0 && pendingRaidInvite.Value.ExpiresAtUnix <= nowUnix)
			{
				(list ?? (list = new List<string>())).Add(pendingRaidInvite.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			PendingRaidInvites.Remove(item);
		}
	}
}
