using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoParty.Server.Core.Models;
using NekoParty.Server.GroupEX.Callbacks;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Routers;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoPartyStaticRouter : StaticRouter
{
	public NekoPartyStaticRouter(NekoPartyCallbacks callbacks, JsonUtil jsonUtil)
		: base(jsonUtil, (IEnumerable<RouteAction>)new _003C_003Ez__ReadOnlyArray<RouteAction>((RouteAction[])(object)new RouteAction[16]
		{
			(RouteAction)new RouteAction<NekoPartyCreateRequest>("/nekoparty/create", (Func<string, NekoPartyCreateRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartyCreateRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyCreate(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartyInviteMemberRequest>("/nekoparty/invite-member", (Func<string, NekoPartyInviteMemberRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartyInviteMemberRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyInviteMember(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartyRemoveMemberRequest>("/nekoparty/remove-member", (Func<string, NekoPartyRemoveMemberRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartyRemoveMemberRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyRemoveMember(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<EmptyRequestData>("/nekoparty/current", (Func<string, EmptyRequestData, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, EmptyRequestData info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyCurrent(url, (IRequestData)info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartySyncSideRequest>("/nekoparty/member-side/sync", (Func<string, NekoPartySyncSideRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartySyncSideRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleSyncPreferredSide(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartySyncRaidSettingsRequest>("/nekoparty/raidsettings/sync", (Func<string, NekoPartySyncRaidSettingsRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartySyncRaidSettingsRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleSyncRaidSettings(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<EmptyRequestData>("/nekoparty/invite/pending", (Func<string, EmptyRequestData, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, EmptyRequestData info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleRaidInvitePending(url, (IRequestData)info, sessionId);
			}),
			(RouteAction)new RouteAction<EmptyRequestData>("/nekoparty/party-invite/pending", (Func<string, EmptyRequestData, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, EmptyRequestData info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyInvitePending(url, (IRequestData)info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartyInviteAcceptRequest>("/nekoparty/party-invite/accept", (Func<string, NekoPartyInviteAcceptRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartyInviteAcceptRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyInviteAccept(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPartyInviteRejectRequest>("/nekoparty/party-invite/reject", (Func<string, NekoPartyInviteRejectRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPartyInviteRejectRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePartyInviteReject(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoRaidInviteAcceptRequest>("/nekoparty/raid-invite/accept", (Func<string, NekoRaidInviteAcceptRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoRaidInviteAcceptRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleRaidInviteAccept(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoRaidInviteRejectRequest>("/nekoparty/raid-invite/reject", (Func<string, NekoRaidInviteRejectRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoRaidInviteRejectRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleRaidInviteReject(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<EmptyRequestData>("/nekoparty/transit/config", (Func<string, EmptyRequestData, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, EmptyRequestData info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleTransitConfig(url, (IRequestData)info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPlayerVisualUploadRequest>("/nekoparty/player-visual/upload", (Func<string, NekoPlayerVisualUploadRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPlayerVisualUploadRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePlayerVisualUpload(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoPlayerVisualGetRequest>("/nekoparty/player-visual/get", (Func<string, NekoPlayerVisualGetRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoPlayerVisualGetRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandlePlayerVisualGet(url, info, sessionId);
			}),
			(RouteAction)new RouteAction<NekoTransitRelayRequest>("/nekoparty/transit/relay", (Func<string, NekoTransitRelayRequest, MongoId, string, CancellationToken, ValueTask<string>>)async delegate(string url, NekoTransitRelayRequest info, MongoId sessionId, string? output, CancellationToken cancellationToken)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				return await callbacks.HandleTransitRelay(url, info, sessionId);
			})
		}))
	{
	}
}
