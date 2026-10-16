using System.Collections.Generic;
using Fika.Core.Networking.Http;
using Fika.Core.Networking.Models.Presence;
using Fika.Core.UI.Models;
using NekoParty.Core.Networking;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.State;

namespace NekoParty.GroupEX.Networking;

public static class NekoGroupRequestHandler
{
	public static NekoPartyOperationResponse CreateParty()
	{
		return NekoPartyHttpClient.PostJson<NekoPartyCreateRequest, NekoPartyOperationResponse>("/nekoparty/create", default(NekoPartyCreateRequest));
	}

	public static NekoPartyOperationResponse InvitePartyMember(string targetProfileId)
	{
		return NekoPartyHttpClient.PostJson<NekoPartyInviteMemberRequest, NekoPartyOperationResponse>("/nekoparty/invite-member", new NekoPartyInviteMemberRequest(targetProfileId));
	}

	public static NekoPartyOperationResponse RemovePartyMember(string targetProfileId)
	{
		return NekoPartyHttpClient.PostJson<NekoPartyRemoveMemberRequest, NekoPartyOperationResponse>("/nekoparty/remove-member", new NekoPartyRemoveMemberRequest(targetProfileId));
	}

	public static NekoPartyOperationResponse GetCurrentParty()
	{
		return NekoPartyHttpClient.GetJson<NekoPartyOperationResponse>("/nekoparty/current");
	}

	public static NekoPartyOperationResponse SyncPreferredSide(int preferredSide)
	{
		return NekoPartyHttpClient.PostJson<NekoPartySyncSideRequest, NekoPartyOperationResponse>("/nekoparty/member-side/sync", new NekoPartySyncSideRequest(preferredSide));
	}

	public static NekoPartyOperationResponse SyncPartyRaidSettings(string location, int side, int time)
	{
		return NekoPartyHttpClient.PostJson<NekoPartySyncRaidSettingsRequest, NekoPartyOperationResponse>("/nekoparty/raidsettings/sync", new NekoPartySyncRaidSettingsRequest(location ?? string.Empty, side, time));
	}

	public static NekoPendingRaidInvitesResponse GetPendingRaidInvites()
	{
		return NekoPartyHttpClient.GetJson<NekoPendingRaidInvitesResponse>("/nekoparty/invite/pending");
	}

	public static NekoPendingPartyInvitesResponse GetPendingPartyInvites()
	{
		return NekoPartyHttpClient.GetJson<NekoPendingPartyInvitesResponse>("/nekoparty/party-invite/pending");
	}

	public static NekoPartyOperationResponse AcceptPartyInvite(string inviteId)
	{
		return NekoPartyHttpClient.PostJson<NekoPartyInviteAcceptRequest, NekoPartyOperationResponse>("/nekoparty/party-invite/accept", new NekoPartyInviteAcceptRequest(inviteId));
	}

	public static NekoPartyOperationResponse RejectPartyInvite(string inviteId)
	{
		return NekoPartyHttpClient.PostJson<NekoPartyInviteRejectRequest, NekoPartyOperationResponse>("/nekoparty/party-invite/reject", new NekoPartyInviteRejectRequest(inviteId));
	}

	public static NekoRaidInviteDecisionResponse AcceptRaidInvite(string inviteId)
	{
		return NekoPartyHttpClient.PostJson<NekoRaidInviteAcceptRequest, NekoRaidInviteDecisionResponse>("/nekoparty/raid-invite/accept", new NekoRaidInviteAcceptRequest(inviteId, (NekoGroupState.PendingAcceptedRaidInvitePreferredSide >= 0) ? NekoGroupState.PendingAcceptedRaidInvitePreferredSide : NekoGroupState.PreferredLocalSide));
	}

	public static NekoRaidInviteDecisionResponse RejectRaidInvite(string inviteId)
	{
		return NekoPartyHttpClient.PostJson<NekoRaidInviteRejectRequest, NekoRaidInviteDecisionResponse>("/nekoparty/raid-invite/reject", new NekoRaidInviteRejectRequest(inviteId));
	}

	public static NekoTransitConfigResponse GetTransitConfig()
	{
		return NekoPartyHttpClient.GetJson<NekoTransitConfigResponse>("/nekoparty/transit/config");
	}

	public static NekoTransitRelayResponse AllocateTransitRelay(string serverGuid)
	{
		return NekoPartyHttpClient.PostJson<NekoTransitRelayRequest, NekoTransitRelayResponse>("/nekoparty/transit/relay", new NekoTransitRelayRequest
		{
			ServerGuid = serverGuid
		});
	}

	public static FikaPlayerPresence[] GetPlayerPresences()
	{
		return FikaRequestHandler.GetPlayerPresences();
	}

	public static Dictionary<string, string> AvailableReceivers(string ownProfileId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return FikaRequestHandler.AvailableReceivers(new AvailableReceiversRequest(ownProfileId));
	}

	public static bool UploadPlayerVisual(string accountId, string visualJson)
	{
		try
		{
			return NekoPartyHttpClient.PostJson<NekoPlayerVisualUploadBody, NekoPlayerVisualResponseBody>("/nekoparty/player-visual/upload", new NekoPlayerVisualUploadBody
			{
				AccountId = accountId,
				VisualJson = visualJson
			})?.Success ?? false;
		}
		catch
		{
			return false;
		}
	}

	public static string GetPlayerVisual(string accountId)
	{
		try
		{
			NekoPlayerVisualResponseBody nekoPlayerVisualResponseBody = NekoPartyHttpClient.PostJson<NekoPlayerVisualGetBody, NekoPlayerVisualResponseBody>("/nekoparty/player-visual/get", new NekoPlayerVisualGetBody
			{
				AccountId = accountId
			});
			if (nekoPlayerVisualResponseBody == null || !nekoPlayerVisualResponseBody.Success)
			{
				return null;
			}
			return nekoPlayerVisualResponseBody.VisualJson;
		}
		catch
		{
			return null;
		}
	}
}
