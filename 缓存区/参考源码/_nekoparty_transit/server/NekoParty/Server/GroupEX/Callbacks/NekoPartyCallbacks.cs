using System.Threading.Tasks;
using NekoParty.Server.Core.Models;
using NekoParty.Server.GroupEX.Controllers;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Callbacks;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoPartyCallbacks(HttpResponseUtil httpResponseUtil, NekoPartyController partyController)
{
	public ValueTask<string> HandlePartyCreate(string url, NekoPartyCreateRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyCreate(info, sessionId)));
	}

	public ValueTask<string> HandlePartyInviteMember(string url, NekoPartyInviteMemberRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyInviteMember(info, sessionId)));
	}

	public ValueTask<string> HandlePartyRemoveMember(string url, NekoPartyRemoveMemberRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyRemoveMember(info, sessionId)));
	}

	public ValueTask<string> HandlePartyCurrent(string url, IRequestData info, MongoId sessionId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyCurrent(sessionId)));
	}

	public ValueTask<string> HandleSyncPreferredSide(string url, NekoPartySyncSideRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandleSyncPreferredSide(info, sessionId)));
	}

	public ValueTask<string> HandleSyncRaidSettings(string url, NekoPartySyncRaidSettingsRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandleSyncRaidSettings(info, sessionId)));
	}

	public ValueTask<string> HandleRaidInvitePending(string url, IRequestData info, MongoId sessionId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPendingRaidInvitesResponse>(partyController.HandleRaidInvitePending(sessionId)));
	}

	public ValueTask<string> HandlePartyInvitePending(string url, IRequestData info, MongoId sessionId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPendingPartyInvitesResponse>(partyController.HandlePartyInvitePending(sessionId)));
	}

	public ValueTask<string> HandlePartyInviteAccept(string url, NekoPartyInviteAcceptRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyInviteAccept(info, sessionId)));
	}

	public ValueTask<string> HandlePartyInviteReject(string url, NekoPartyInviteRejectRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPartyOperationResponse>(partyController.HandlePartyInviteReject(info, sessionId)));
	}

	public ValueTask<string> HandleRaidInviteAccept(string url, NekoRaidInviteAcceptRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoRaidInviteDecisionResponse>(partyController.HandleRaidInviteAccept(info, sessionId)));
	}

	public ValueTask<string> HandleRaidInviteReject(string url, NekoRaidInviteRejectRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoRaidInviteDecisionResponse>(partyController.HandleRaidInviteReject(info, sessionId)));
	}

	public ValueTask<string> HandlePlayerVisualUpload(string url, NekoPlayerVisualUploadRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPlayerVisualResponse>(partyController.HandlePlayerVisualUpload(info, sessionId)));
	}

	public ValueTask<string> HandlePlayerVisualGet(string url, NekoPlayerVisualGetRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoPlayerVisualResponse>(partyController.HandlePlayerVisualGet(info, sessionId)));
	}

	public ValueTask<string> HandleTransitConfig(string url, IRequestData info, MongoId sessionId)
	{
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoTransitConfigResponse>(partyController.HandleTransitConfig()));
	}

	public ValueTask<string> HandleTransitRelay(string url, NekoTransitRelayRequest info, MongoId sessionId)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new ValueTask<string>(httpResponseUtil.NoBody<NekoTransitRelayResponse>(partyController.HandleTransitRelay(info, sessionId)));
	}
}
