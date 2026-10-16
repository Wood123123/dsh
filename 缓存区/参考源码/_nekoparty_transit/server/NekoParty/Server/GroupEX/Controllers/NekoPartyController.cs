using FikaServer.Models.Fika;
using FikaServer.Services;
using NekoNet.Server.Core.Models;
using NekoParty.Server.Core.Models;
using NekoParty.Server.GroupEX.Models;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Controllers;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoPartyController(NekoGroupService groupService, NekoCrossPartyService cross, NekoCrossRaidService crossRaid, MatchService matchService)
{
	public NekoPartyOperationResponse HandlePartyCreate(NekoPartyCreateRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		NekoPartyData party = groupService.UpsertLeaderParty(sessionId);
		return new NekoPartyOperationResponse
		{
			Success = true,
			Party = party
		};
	}

	public NekoPartyOperationResponse HandlePartyInviteMember(NekoPartyInviteMemberRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.InviteMember(sessionId, request.TargetProfileId);
	}

	public NekoPartyOperationResponse HandlePartyRemoveMember(NekoPartyRemoveMemberRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.RemoveMember(sessionId, request.TargetProfileId);
	}

	public NekoPartyOperationResponse HandlePartyCurrent(MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		NekoPartyData currentParty = cross.GetCurrentParty(sessionId);
		if (currentParty == (NekoPartyData)null)
		{
			NekoPartyData partyByPlayer = groupService.GetPartyByPlayer(sessionId);
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = partyByPlayer
			};
		}
		return new NekoPartyOperationResponse
		{
			Success = true,
			Party = currentParty
		};
	}

	public NekoPlayerVisualResponse HandlePlayerVisualUpload(NekoPlayerVisualUploadRequest request, MongoId sessionId)
	{
		groupService.StorePlayerVisual(request.AccountId, request.VisualJson);
		return new NekoPlayerVisualResponse
		{
			Success = true
		};
	}

	public NekoPlayerVisualResponse HandlePlayerVisualGet(NekoPlayerVisualGetRequest request, MongoId sessionId)
	{
		string text = groupService.TryGetPlayerVisual(request.AccountId);
		return new NekoPlayerVisualResponse
		{
			Success = (text != null),
			VisualJson = text,
			Error = ((text == null) ? "missing" : null)
		};
	}

	public NekoPartyOperationResponse HandleSyncPreferredSide(NekoPartySyncSideRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		cross.SyncSide(sessionId, request.PreferredSide);
		return groupService.SyncPreferredSide(sessionId, request.PreferredSide);
	}

	public NekoPartyOperationResponse HandleSyncRaidSettings(NekoPartySyncRaidSettingsRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		if (cross.SyncRaidSettings(sessionId, request.Location, request.Side, request.Time, out NekoPartyData party) && party != (NekoPartyData)null)
		{
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = party
			};
		}
		return groupService.SetPartyRaidSettings(sessionId, request.Location, request.Side, request.Time);
	}

	public NekoPendingRaidInvitesResponse HandleRaidInvitePending(MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.GetPendingRaidInvites(sessionId);
	}

	public NekoPendingPartyInvitesResponse HandlePartyInvitePending(MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.GetPendingPartyInvites(sessionId);
	}

	public NekoPartyOperationResponse HandlePartyInviteAccept(NekoPartyInviteAcceptRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.AcceptPartyInvite(sessionId, request.InviteId);
	}

	public NekoPartyOperationResponse HandlePartyInviteReject(NekoPartyInviteRejectRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.RejectPartyInvite(sessionId, request.InviteId);
	}

	public NekoRaidInviteDecisionResponse HandleRaidInviteAccept(NekoRaidInviteAcceptRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		NekoRaidInviteDecisionResponse val = groupService.AcceptRaidInvite(sessionId, request.InviteId, request.PreferredSide);
		if (!val.Success || string.IsNullOrEmpty(val.ServerId))
		{
			return val;
		}
		FikaMatch match = matchService.GetMatch((MongoId?)new MongoId(val.ServerId));
		if (match == (FikaMatch)null)
		{
			return new NekoRaidInviteDecisionResponse
			{
				Success = false,
				Error = "Target raid no longer exists"
			};
		}
		val.JoinData = new NekoInviteJoinData
		{
			ServerId = val.ServerId,
			ServerGuid = match.ServerGuid.ToString(),
			Timestamp = match.Timestamp,
			GameVersion = match.GameVersion,
			Crc32 = match.CRC32,
			RaidCode = match.RaidCode
		};
		return val;
	}

	public NekoRaidInviteDecisionResponse HandleRaidInviteReject(NekoRaidInviteRejectRequest request, MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return groupService.RejectRaidInvite(sessionId, request.InviteId);
	}

	public NekoTransitConfigResponse HandleTransitConfig()
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		TransitConfigWire transitConfig = crossRaid.GetTransitConfig();
		if (transitConfig == null)
		{
			return new NekoTransitConfigResponse
			{
				Success = false
			};
		}
		return new NekoTransitConfigResponse
		{
			Success = true,
			Enabled = transitConfig.Enabled,
			PunchHost = transitConfig.PunchHost,
			PunchPort = transitConfig.PunchPort,
			RelayEnabled = transitConfig.RelayEnabled,
			NatProbePort1 = transitConfig.NatProbePort1,
			NatProbePort2 = transitConfig.NatProbePort2
		};
	}

	public NekoTransitRelayResponse HandleTransitRelay(NekoTransitRelayRequest request, MongoId sessionId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		TransitRelayAllocateResultWire val = crossRaid.AllocateRelay(request.ServerGuid, sessionId);
		if (val == null)
		{
			return new NekoTransitRelayResponse
			{
				Success = false,
				Error = "allocate failed"
			};
		}
		return new NekoTransitRelayResponse
		{
			Success = true,
			Host = val.Host,
			Port = val.Port,
			SessionId = val.SessionId
		};
	}
}
