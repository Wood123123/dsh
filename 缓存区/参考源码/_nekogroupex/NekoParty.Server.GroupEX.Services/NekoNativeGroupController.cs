using System;
using System.Collections.Generic;
using System.Linq;
using NekoParty.Server.Core.Models;
using NekoParty.Server.Core.Services;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoNativeGroupController(NekoGroupService groupService, NekoNativeGroupNotifier notifier, NekoProfileResolver profileResolver, NekoCrossPartyService cross, HttpResponseUtil httpResponseUtil, JsonUtil jsonUtil, ISptLogger<NekoNativeGroupController> logger)
{
	public static readonly IReadOnlySet<string> HandledPaths = new HashSet<string>(StringComparer.Ordinal)
	{
		"/client/match/group/current", "/client/match/group/status", "/client/match/group/invite/send", "/client/match/group/invite/accept", "/client/match/group/invite/decline", "/client/match/group/invite/cancel", "/client/match/group/invite/cancel-all", "/client/match/group/transfer", "/client/match/group/leave", "/client/match/group/delete",
		"/client/match/group/player/remove", "/client/match/group/looking/start", "/client/match/group/looking/stop", "/client/match/group/exit_from_menu"
	};

	public string Dispatch(string path, string? body, MongoId sessionId)
	{
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		switch (path)
		{
		case "/client/match/group/current":
			return httpResponseUtil.GetBody<NekoMatchGroupCurrentResponse>(new NekoMatchGroupCurrentResponse
			{
				Squad = BuildSquad(sessionId)
			}, (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/status":
			return httpResponseUtil.GetBody<NekoMatchGroupStatusResponse>(new NekoMatchGroupStatusResponse
			{
				Players = BuildSquad(sessionId),
				MaxPveCountExceeded = false
			}, (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/invite/accept":
		{
			HttpResponseUtil val2 = httpResponseUtil;
			RequestIdRequest val3 = Deserialize<RequestIdRequest>(body);
			return val2.GetBody<List<NekoGroupCharacterDto>>(AcceptInvite(sessionId, (val3 != (RequestIdRequest)null) ? val3.RequestId : null), (BackendErrorCodes)0, (string)null, true);
		}
		case "/client/match/group/invite/decline":
		{
			RequestIdRequest val4 = Deserialize<RequestIdRequest>(body);
			DeclineInvite(sessionId, (val4 != (RequestIdRequest)null) ? val4.RequestId : null);
			return httpResponseUtil.GetBody<bool>(true, (BackendErrorCodes)0, (string)null, true);
		}
		case "/client/match/group/invite/cancel":
		case "/client/match/group/invite/cancel-all":
			return httpResponseUtil.GetBody<bool>(true, (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/invite/send":
			return httpResponseUtil.GetBody<string>(SendInvite(sessionId, Deserialize<MatchGroupInviteSendRequest>(body)), (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/delete":
		case "/client/match/group/leave":
			LeaveOrDisband(sessionId);
			return httpResponseUtil.GetBody<bool>(true, (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/player/remove":
		{
			MatchGroupPlayerRemoveRequest val = Deserialize<MatchGroupPlayerRemoveRequest>(body);
			RemovePlayer(sessionId, (val != (MatchGroupPlayerRemoveRequest)null) ? val.AidToKick : null);
			return httpResponseUtil.GetBody<bool>(true, (BackendErrorCodes)0, (string)null, true);
		}
		case "/client/match/group/transfer":
			return httpResponseUtil.GetBody<bool>(true, (BackendErrorCodes)0, (string)null, true);
		case "/client/match/group/looking/start":
		case "/client/match/group/exit_from_menu":
		case "/client/match/group/looking/stop":
			return httpResponseUtil.NullResponse();
		default:
			logger.Warning("[NekoMatchGroup] 未覆盖的原生组队路由: " + path, (Exception)null);
			return httpResponseUtil.NullResponse();
		}
	}

	private string SendInvite(MongoId sessionId, MatchGroupInviteSendRequest? request)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		string text = ((request != (MatchGroupInviteSendRequest)null) ? request.To : null);
		if (string.IsNullOrWhiteSpace(text))
		{
			logger.Warning("[NekoMatchGroup] invite/send: 缺少 to", (Exception)null);
			return string.Empty;
		}
		if (cross.TryResolveRemoteTarget(text, out var remoteHash))
		{
			string text2 = cross.Invite(sessionId, remoteHash);
			if (!string.IsNullOrEmpty(text2))
			{
				logger.Success("[NekoMatchGroup] invite/send(跨服) -> remote=" + remoteHash.Substring(0, Math.Min(8, remoteHash.Length)) + " req=" + text2, (Exception)null);
				return text2;
			}
			logger.Warning("[NekoMatchGroup] invite/send(跨服) 失败 to=" + text, (Exception)null);
			return string.Empty;
		}
		string text3 = ResolveTargetProfileId(text);
		logger.Success($"[NekoMatchGroup] invite/send 收到: to={text} inLobby={((request != (MatchGroupInviteSendRequest)null) ? request.InLobby : ((bool?)null))} -> target={text3 ?? "<unresolved>"}", (Exception)null);
		if (string.IsNullOrWhiteSpace(text3))
		{
			logger.Warning("[NekoMatchGroup] invite/send: 无法解析目标 to=" + text, (Exception)null);
			return string.Empty;
		}
		groupService.UpsertLeaderParty(sessionId);
		NekoPartyOperationResponse val = groupService.InviteMember(sessionId, text3);
		if (!val.Success)
		{
			logger.Warning($"[NekoMatchGroup] invite/send 失败 to={text} target={text3} err={val.Error}", (Exception)null);
			return string.Empty;
		}
		logger.Success("[NekoMatchGroup] invite/send -> target=" + text3 + " req=" + val.InviteId, (Exception)null);
		return val.InviteId;
	}

	private string? ResolveTargetProfileId(string to)
	{
		if (int.TryParse(to, out var result))
		{
			string profileIdByAid = profileResolver.GetProfileIdByAid(result);
			if (!string.IsNullOrWhiteSpace(profileIdByAid))
			{
				return profileIdByAid;
			}
		}
		if (!profileResolver.ProfileExists(to))
		{
			return null;
		}
		return to;
	}

	private List<NekoGroupCharacterDto> BuildSquad(MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		List<NekoGroupCharacterDto> currentSquad = cross.GetCurrentSquad(sessionId);
		if (currentSquad != null)
		{
			return currentSquad;
		}
		NekoPartyData partyByPlayer = groupService.GetPartyByPlayer(sessionId);
		List<NekoPartyMember> list = ((partyByPlayer != (NekoPartyData)null) ? partyByPlayer.Members : null);
		if (list == null || list.Count <= 0)
		{
			return new List<NekoGroupCharacterDto>();
		}
		return partyByPlayer.Members.Select((NekoPartyMember member) => notifier.BuildCharacter(member.ProfileId, member.IsLeader, isReady: true, member.PreferredSide)).ToList();
	}

	private List<NekoGroupCharacterDto> AcceptInvite(MongoId sessionId, string? requestId)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(requestId))
		{
			logger.Warning("[NekoMatchGroup] accept: requestId 为空", (Exception)null);
			return new List<NekoGroupCharacterDto>();
		}
		if (cross.IsRemoteInvite(requestId))
		{
			return cross.Accept(sessionId, requestId) ?? new List<NekoGroupCharacterDto>();
		}
		NekoPartyOperationResponse val = groupService.AcceptPartyInvite(sessionId, requestId);
		if (val.Success)
		{
			NekoPartyData party = val.Party;
			List<NekoPartyMember> list = ((party != (NekoPartyData)null) ? party.Members : null);
			if (list != null && list.Count > 0)
			{
				NekoPartyData party2 = val.Party;
				List<NekoGroupCharacterDto> list2 = party2.Members.Select((NekoPartyMember member) => notifier.BuildCharacter(member.ProfileId, member.IsLeader, isReady: true)).ToList();
				string selfId = ((object)sessionId).ToString();
				NekoGroupCharacterDto nekoGroupCharacterDto = list2.FirstOrDefault((NekoGroupCharacterDto member) => string.Equals(member.Id, selfId, StringComparison.Ordinal));
				if (nekoGroupCharacterDto != null)
				{
					foreach (NekoPartyMember member in party2.Members)
					{
						if (!string.Equals(member.ProfileId, selfId, StringComparison.Ordinal))
						{
							notifier.PushInviteAccept(member.ProfileId, nekoGroupCharacterDto);
						}
					}
				}
				logger.Success($"[NekoMatchGroup] accept 成功 req={requestId} self={selfId} squad={list2.Count}", (Exception)null);
				return list2;
			}
		}
		logger.Warning("[NekoMatchGroup] accept 失败 req=" + requestId + " err=" + val.Error, (Exception)null);
		return new List<NekoGroupCharacterDto>();
	}

	private void DeclineInvite(MongoId sessionId, string? requestId)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (!string.IsNullOrWhiteSpace(requestId))
		{
			if (cross.IsRemoteInvite(requestId))
			{
				cross.Decline(sessionId, requestId);
			}
			else
			{
				groupService.RejectPartyInvite(sessionId, requestId);
			}
		}
	}

	private void LeaveOrDisband(MongoId sessionId)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (!cross.Leave(sessionId))
		{
			groupService.RemoveMember(sessionId, ((object)sessionId).ToString());
		}
	}

	private void RemovePlayer(MongoId sessionId, string? aidToKick)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		if (!string.IsNullOrWhiteSpace(aidToKick) && int.TryParse(aidToKick, out var result) && (!cross.TryResolveRemoteTarget(aidToKick, out var remoteHash) || !cross.KickRemote(sessionId, remoteHash)))
		{
			string profileIdByAid = profileResolver.GetProfileIdByAid(result);
			if (string.IsNullOrWhiteSpace(profileIdByAid))
			{
				logger.Warning($"[NekoMatchGroup] remove: 无法按 aid={result} 反查 profileId", (Exception)null);
			}
			else
			{
				groupService.RemoveMember(sessionId, profileIdByAid);
			}
		}
	}

	private T? Deserialize<T>(string? body) where T : class
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return null;
		}
		try
		{
			return jsonUtil.Deserialize<T>(body);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoMatchGroup] 反序列化 " + typeof(T).Name + " 失败: " + ex.Message, (Exception)null);
			return null;
		}
	}
}
