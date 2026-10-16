using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using FikaServer.WebSockets;
using NekoNet.Server.Core;
using NekoNet.Server.Core.Models;
using NekoParty.Server.Core.Models;
using NekoParty.Server.Core.Services;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Servers.Ws;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoCrossPartyService(NekoState state, HubClient hub, NekoHooks hooks, HubWsClient hubWs, NekoNativeGroupNotifier notifier, NekoProfileResolver profileResolver, NekoModFingerprintService fingerprint, NotificationWebSocket notificationWebSocket, SaveServer saveServer, SptWebSocketConnectionHandler wsHandler, JsonUtil jsonUtil, ISptLogger<NekoCrossPartyService> logger)
{
	private readonly ConcurrentDictionary<string, byte> _remoteInviteIds = new ConcurrentDictionary<string, byte>();

	private const int StaleEventSeconds = 600;

	private int _started;

	public bool Ready
	{
		get
		{
			if (state.Config.Enabled && state.Config.EnableParty && state.Authenticated)
			{
				return !string.IsNullOrEmpty(state.Token);
			}
			return false;
		}
	}

	public void Start()
	{
		if (Interlocked.Exchange(ref _started, 1) != 1)
		{
			hooks.HubRawMessage += OnHubRawMessage;
			hooks.HubConnected += OnHubConnected;
			hubWs.EnsureStarted();
			logger.Success("[NekoCrossParty] 跨服组队桥接已启用（Hub party 插件 + 实时通道 + 拉取兜底）。", (Exception)null);
		}
	}

	private static string SelfHash(MongoId sessionId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return NekoState.Hash(((object)sessionId).ToString());
	}

	public bool TryResolveRemoteTarget(string to, out string remoteHash)
	{
		remoteHash = "";
		if (string.IsNullOrWhiteSpace(to))
		{
			return false;
		}
		if (state.RemoteByLocalUserId.TryGetValue(to, out var value) && !string.IsNullOrEmpty(value))
		{
			remoteHash = value;
			return true;
		}
		if (int.TryParse(to, out var result) && state.RemoteSellerByAid.TryGetValue(result, out var value2) && !string.IsNullOrEmpty(value2))
		{
			remoteHash = value2;
			return true;
		}
		return false;
	}

	public bool IsRemoteInvite(string? inviteId)
	{
		if (!string.IsNullOrEmpty(inviteId))
		{
			return _remoteInviteIds.ContainsKey(inviteId);
		}
		return false;
	}

	public string? Invite(MongoId leaderSid, string targetRemoteHash)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		if (!Ready)
		{
			return null;
		}
		string text = SelfHash(leaderSid);
		state.LocalSessionByProfileHash[text] = ((object)leaderSid).ToString();
		PartyMemberCardWire leaderCard = BuildSelfCard(leaderSid, isLeader: true, 0);
		PartyInviteCreateWire val = new PartyInviteCreateWire
		{
			FromProfileId = text,
			ToProfileId = targetRemoteHash,
			LeaderCard = leaderCard
		};
		try
		{
			ApiResponse<PartyInviteResultWire> result = hub.PartyInviteAsync(state.Token, val).GetAwaiter().GetResult();
			if (result != null && result.Ok && result.Data != null && !string.IsNullOrEmpty(result.Data.InviteId))
			{
				logger.Success("[NekoCrossParty] 跨服邀请已发起 inviteId=" + result.Data.InviteId + " target=" + Short(targetRemoteHash), (Exception)null);
				return result.Data.InviteId;
			}
			logger.Warning("[NekoCrossParty] 跨服邀请被 Hub 拒绝: " + (result?.Error ?? "响应为空"), (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 跨服邀请异常: " + ex.Message, (Exception)null);
		}
		return null;
	}

	public List<NekoGroupCharacterDto>? Accept(MongoId sessionId, string inviteId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		if (!Ready)
		{
			return null;
		}
		string text = SelfHash(sessionId);
		state.LocalSessionByProfileHash[text] = ((object)sessionId).ToString();
		PartyMemberCardWire selfCard = BuildSelfCard(sessionId, isLeader: false, 0);
		try
		{
			ApiResponse<PartySnapshotResultWire> result = hub.PartyAcceptAsync(state.Token, new PartyInviteAcceptWire
			{
				InviteId = inviteId,
				SelfProfileId = text,
				SelfCard = selfCard
			}).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Snapshot == null)
			{
				logger.Warning("[NekoCrossParty] 接受跨服邀请失败 inviteId=" + inviteId, (Exception)null);
				return null;
			}
			_remoteInviteIds.TryRemove(inviteId, out var _);
			List<NekoGroupCharacterDto> list = BuildSquadFromSnapshot(result.Data.Snapshot);
			logger.Success($"[NekoCrossParty] 已接受跨服邀请 inviteId={inviteId} squad={list.Count}", (Exception)null);
			TryRegisterRaidAccounts(new _003C_003Ez__ReadOnlySingleElementList<string>(text));
			return list;
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 接受跨服邀请异常: " + ex.Message, (Exception)null);
			return null;
		}
	}

	public void Decline(MongoId sessionId, string inviteId)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		if (!Ready)
		{
			return;
		}
		_remoteInviteIds.TryRemove(inviteId, out var _);
		try
		{
			hub.PartyDeclineAsync(state.Token, new PartyInviteActionWire
			{
				InviteId = inviteId,
				SelfProfileId = SelfHash(sessionId),
				SelfCard = BuildSelfCard(sessionId, isLeader: false, 0, includeVisual: false)
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 拒绝跨服邀请异常: " + ex.Message, (Exception)null);
		}
	}

	public bool Leave(MongoId sessionId)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		if (!Ready)
		{
			return false;
		}
		try
		{
			ApiResponse<string> result = hub.PartyLeaveAsync(state.Token, new PartyLeaveWire
			{
				SelfProfileId = SelfHash(sessionId)
			}).GetAwaiter().GetResult();
			return result != null && result.Ok && result.Data == "ok";
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 离队异常: " + ex.Message, (Exception)null);
			return false;
		}
	}

	public bool KickRemote(MongoId leaderSid, string targetRemoteHash)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		if (!Ready)
		{
			return false;
		}
		try
		{
			return hub.PartyKickAsync(state.Token, new PartyKickWire
			{
				LeaderProfileId = SelfHash(leaderSid),
				TargetProfileId = targetRemoteHash
			}).GetAwaiter().GetResult()?.Ok ?? false;
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 踢人异常: " + ex.Message, (Exception)null);
			return false;
		}
	}

	public void SyncSide(MongoId sessionId, int preferredSide)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		if (!Ready)
		{
			return;
		}
		try
		{
			hub.PartySyncSideAsync(state.Token, new PartySyncSideWire
			{
				SelfProfileId = SelfHash(sessionId),
				PreferredSide = preferredSide
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 阵营同步异常: " + ex.Message, (Exception)null);
		}
	}

	public List<NekoGroupCharacterDto>? GetCurrentSquad(MongoId sessionId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return null;
		}
		string text = SelfHash(sessionId);
		state.LocalSessionByProfileHash[text] = ((object)sessionId).ToString();
		try
		{
			ApiResponse<PartySnapshotResultWire> result = hub.PartyCurrentAsync(state.Token, text).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Snapshot == null || result.Data.Snapshot.Members.Count == 0)
			{
				return null;
			}
			return BuildSquadFromSnapshot(result.Data.Snapshot);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 拉取当前队伍异常: " + ex.Message, (Exception)null);
			return null;
		}
	}

	public bool SyncRaidSettings(MongoId sessionId, string? location, int side, int time, out NekoPartyData? party)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		party = null;
		if (!Ready)
		{
			return false;
		}
		string text = SelfHash(sessionId);
		state.LocalSessionByProfileHash[text] = ((object)sessionId).ToString();
		try
		{
			ApiResponse<PartySnapshotResultWire> result = hub.PartyRaidSettingsAsync(state.Token, new PartyRaidSettingsWire
			{
				SelfProfileId = text,
				Location = location,
				Side = side,
				Time = time
			}).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Snapshot == null || result.Data.Snapshot.Members.Count == 0)
			{
				return false;
			}
			party = BuildPartyDataFromSnapshot(result.Data.Snapshot);
			return true;
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 同步 raid 设置异常: " + ex.Message, (Exception)null);
			return false;
		}
	}

	public NekoPartyData? GetCurrentParty(MongoId sessionId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return null;
		}
		string text = SelfHash(sessionId);
		state.LocalSessionByProfileHash[text] = ((object)sessionId).ToString();
		try
		{
			ApiResponse<PartySnapshotResultWire> result = hub.PartyCurrentAsync(state.Token, text).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Snapshot == null || result.Data.Snapshot.Members.Count == 0)
			{
				return null;
			}
			return BuildPartyDataFromSnapshot(result.Data.Snapshot);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 拉取当前队伍数据异常: " + ex.Message, (Exception)null);
			return null;
		}
	}

	private void OnHubConnected()
	{
		if (Ready)
		{
			logger.Info("[NekoCrossParty] Hub WS 已重连，立即拉取待投递组队事件。", (Exception)null);
			PollEvents();
		}
	}

	private void OnHubRawMessage(string json)
	{
		if (!string.IsNullOrEmpty(json) && json.Contains("\"party\"", StringComparison.Ordinal))
		{
			HubWsEnvelope<PartyEventWire> val;
			try
			{
				val = JsonSerializer.Deserialize<HubWsEnvelope<PartyEventWire>>(json);
			}
			catch
			{
				return;
			}
			if (val != null && string.Equals(val.Channel, "party", StringComparison.Ordinal) && val.Event != null && TryDispatch(val.Event))
			{
				int num = 1;
				List<string> list = new List<string>(num);
				CollectionsMarshal.SetCount(list, num);
				CollectionsMarshal.AsSpan(list)[0] = val.Event.Id;
				AckEvents(list);
			}
		}
	}

	public void PollEvents()
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return;
		}
		RefreshOnlineSessions();
		List<string> list = state.LocalSessionByProfileHash.Keys.ToList();
		if (list.Count == 0)
		{
			return;
		}
		TryRegisterRaidAccounts(list);
		try
		{
			ApiResponse<PartyEventsResultWire> result = hub.PartyEventsAsync(state.Token, new PartyEventsPullWire
			{
				SelfProfileIds = list
			}).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Events.Count == 0)
			{
				return;
			}
			List<string> list2 = new List<string>(result.Data.Events.Count);
			long num = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			foreach (PartyEventWire @event in result.Data.Events)
			{
				if (TryDispatch(@event))
				{
					list2.Add(@event.Id);
				}
				else if (@event.CreatedAt > 0 && num - @event.CreatedAt > 600)
				{
					logger.Warning($"[NekoCrossParty] 组队事件超龄未能投递，丢弃 kind={@event.Kind} target={Short(@event.TargetProfileId)}", (Exception)null);
					list2.Add(@event.Id);
				}
			}
			AckEvents(list2);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 拉取事件异常: " + ex.Message, (Exception)null);
		}
	}

	private void AckEvents(List<string> ids)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		if (ids.Count == 0)
		{
			return;
		}
		try
		{
			hub.PartyEventsAckAsync(state.Token, new PartyEventsAckWire
			{
				Ids = ids
			}).GetAwaiter().GetResult();
		}
		catch
		{
		}
	}

	private bool TryDispatch(PartyEventWire ev)
	{
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected I4, but got Unknown
		RefreshOnlineSessions();
		if (!state.LocalSessionByProfileHash.TryGetValue(ev.TargetProfileId, out var value) || string.IsNullOrEmpty(value))
		{
			logger.Warning($"[NekoCrossParty] 组队事件暂缓投递（目标非本服账户） kind={ev.Kind} target={Short(ev.TargetProfileId)}", (Exception)null);
			return false;
		}
		try
		{
			PartyEventKindWire kind = ev.Kind;
			switch ((int)kind)
			{
			case 0:
			{
				if (CheckPeerFingerprint(ev.Actor, value, state.Config.CrossRaidBlockOnModMismatch).Mismatch && state.Config.CrossRaidBlockOnModMismatch)
				{
					AutoDeclineForModMismatch(ev, value);
					return true;
				}
				if (!string.IsNullOrEmpty(ev.InviteId))
				{
					_remoteInviteIds[ev.InviteId] = 0;
				}
				NekoGroupCharacterDto nekoGroupCharacterDto2 = BuildCharacterFromCard(ev.Actor);
				notifier.PushInviteSend(value, nekoGroupCharacterDto2.Id ?? "", ev.InviteId, new _003C_003Ez__ReadOnlySingleElementList<NekoGroupCharacterDto>(nekoGroupCharacterDto2));
				break;
			}
			case 1:
			{
				CheckPeerFingerprint(ev.Actor, value, blocked: false);
				NekoGroupCharacterDto newMember = BuildCharacterFromCard(ev.Actor);
				notifier.PushInviteAccept(value, newMember);
				PartySnapshotWire snapshot = ev.Snapshot;
				PartyMemberCardWire val = ((snapshot != null) ? snapshot.Members.FirstOrDefault((PartyMemberCardWire m) => m.IsLeader) : null);
				if (val != null)
				{
					notifier.PushLeaderChanged(value, AidOf(val));
				}
				break;
			}
			case 2:
				notifier.PushUserLeave(value, AidOf(ev.Actor), ev.Actor.Nickname);
				break;
			case 3:
				notifier.PushGroupRemoved(value);
				break;
			case 5:
			{
				NekoGroupCharacterDto nekoGroupCharacterDto = BuildCharacterFromCard(ev.Actor);
				nekoGroupCharacterDto.IsReady = true;
				nekoGroupCharacterDto.IsLeader = true;
				notifier.PushRaidReady(value, nekoGroupCharacterDto);
				break;
			}
			case 4:
				CheckPeerFingerprint(ev.Actor, value, blocked: true);
				break;
			}
			return true;
		}
		catch (Exception ex)
		{
			logger.Error($"[NekoCrossParty] Dispatch({ev.Kind}) 异常: {ex.Message}", (Exception)null);
			return false;
		}
	}

	private void RefreshOnlineSessions()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		foreach (KeyValuePair<MongoId, SptProfile> profile in saveServer.GetProfiles())
		{
			var (val3, _) = profile;
			state.LocalSessionByProfileHash[NekoState.Hash(((object)val3).ToString())] = ((object)val3).ToString();
		}
	}

	private NekoModFingerprintService.CompareResult CheckPeerFingerprint(PartyMemberCardWire actor, string sid, bool blocked)
	{
		NekoModFingerprintService.CompareResult compareResult = fingerprint.Compare(actor.ModFingerprintJson, actor.Nickname);
		if (!compareResult.Mismatch)
		{
			return compareResult;
		}
		logger.Warning($"[NekoCrossParty] 与 {actor.Nickname}({Short(actor.ProfileId)}) 的服务器 MOD 配置不一致，共 {compareResult.Diffs.Count} 项差异" + (blocked ? "（本次组队已被拦截/拒绝）" : "（仅告警）") + ":\n  - " + string.Join("\n  - ", compareResult.Diffs), (Exception)null);
		PushModMismatch(sid, actor.Nickname, compareResult.Report, blocked);
		return compareResult;
	}

	private void AutoDeclineForModMismatch(PartyEventWire ev, string sid)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		if (string.IsNullOrEmpty(ev.InviteId))
		{
			return;
		}
		_remoteInviteIds.TryRemove(ev.InviteId, out var _);
		try
		{
			hub.PartyDeclineAsync(state.Token, new PartyInviteActionWire
			{
				InviteId = ev.InviteId,
				SelfProfileId = ev.TargetProfileId,
				SelfCard = BuildSelfCard(new MongoId(sid), isLeader: false, 0, includeVisual: false)
			}).GetAwaiter().GetResult();
			logger.Warning("[NekoCrossParty] MOD 不一致，已按配置自动拒绝邀请 inviteId=" + ev.InviteId + " target=" + Short(ev.TargetProfileId), (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossParty] 自动拒绝邀请异常: " + ex.Message, (Exception)null);
		}
	}

	private void PushModMismatch(string sid, string peerNickname, string report, bool blocked)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			notificationWebSocket.SendAsync<NekoModMismatchNotification>(new MongoId(sid), new NekoModMismatchNotification
			{
				PeerNickname = peerNickname,
				Report = report,
				Blocked = blocked
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Warning("[NekoCrossParty] 推送 MOD 差异报告失败 target=" + Short(sid) + ": " + ex.Message, (Exception)null);
		}
	}

	private PartyMemberCardWire BuildSelfCard(MongoId sessionId, bool isLeader, int preferredSide, bool includeVisual = true)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		NekoNativeGroupProfile nativeGroupProfile = profileResolver.GetNativeGroupProfile(((object)sessionId).ToString(), preferredSide);
		PartyMemberCardWire val = new PartyMemberCardWire
		{
			ProfileId = SelfHash(sessionId),
			Nickname = nativeGroupProfile.Nickname,
			Side = nativeGroupProfile.Side,
			Level = nativeGroupProfile.Level,
			MemberCategory = nativeGroupProfile.MemberCategory,
			IsLeader = isLeader,
			PreferredSide = preferredSide,
			ModFingerprintJson = fingerprint.Json
		};
		if (includeVisual && state.Config.PartySyncVisual)
		{
			try
			{
				PlayerVisualRepresentation nativeVisual = profileResolver.GetNativeVisual(((object)sessionId).ToString(), preferredSide);
				if (nativeVisual != (PlayerVisualRepresentation)null)
				{
					val.PlayerVisualJson = jsonUtil.Serialize<PlayerVisualRepresentation>(nativeVisual, false);
				}
			}
			catch (Exception ex)
			{
				logger.Warning("[NekoCrossParty] 序列化本人 3D 外观失败（远程将显示占位形象）: " + ex.Message, (Exception)null);
			}
		}
		return val;
	}

	private List<NekoGroupCharacterDto> BuildSquadFromSnapshot(PartySnapshotWire snapshot)
	{
		return snapshot.Members.Select(BuildCharacterFromCard).ToList();
	}

	private NekoPartyData BuildPartyDataFromSnapshot(PartySnapshotWire snapshot)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		return new NekoPartyData
		{
			PartyId = snapshot.PartyId,
			LeaderProfileId = MapCardProfileId(snapshot.LeaderProfileId),
			Status = (ENekoPartyStatus)snapshot.LeaderReady,
			Location = snapshot.Location,
			Side = snapshot.Side,
			Time = snapshot.Time,
			Members = ((IEnumerable<PartyMemberCardWire>)snapshot.Members).Select((Func<PartyMemberCardWire, NekoPartyMember>)delegate(PartyMemberCardWire card)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Unknown result type (might be due to invalid IL or missing references)
				//IL_003c: Expected O, but got Unknown
				return new NekoPartyMember
				{
					ProfileId = MapCardProfileId(card.ProfileId),
					Nickname = card.Nickname,
					IsLeader = card.IsLeader,
					PreferredSide = card.PreferredSide
				};
			}).ToList()
		};
	}

	private string MapCardProfileId(string profileHash)
	{
		if (state.LocalSessionByProfileHash.TryGetValue(profileHash, out var value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		return NekoState.ProfileIdToLocalUserId(profileHash);
	}

	private NekoGroupCharacterDto BuildCharacterFromCard(PartyMemberCardWire card)
	{
		if (state.LocalSessionByProfileHash.TryGetValue(card.ProfileId, out var value) && !string.IsNullOrEmpty(value))
		{
			return notifier.BuildCharacter(value, card.IsLeader, isReady: true, card.PreferredSide);
		}
		state.RegisterRemote(card.ProfileId);
		PlayerVisualRepresentation playerVisualRepresentation = null;
		if (!string.IsNullOrEmpty(card.PlayerVisualJson))
		{
			try
			{
				playerVisualRepresentation = jsonUtil.Deserialize<PlayerVisualRepresentation>(card.PlayerVisualJson);
			}
			catch (Exception ex)
			{
				logger.Warning("[NekoCrossParty] 反序列化远程 3D 外观失败: " + ex.Message, (Exception)null);
			}
		}
		return new NekoGroupCharacterDto
		{
			Id = NekoState.ProfileIdToLocalUserId(card.ProfileId),
			Aid = NekoState.ProfileIdToAid(card.ProfileId).ToString(),
			IsLeader = card.IsLeader,
			IsReady = true,
			Region = string.Empty,
			LookingGroup = false,
			Info = new NekoGroupInfoDto
			{
				Nickname = card.Nickname,
				Side = card.Side,
				Level = card.Level,
				PrestigeLevel = 0,
				MemberCategory = card.MemberCategory,
				SelectedMemberCategory = card.MemberCategory,
				GameVersion = "live",
				HasCoopExtension = false
			},
			PlayerVisualRepresentation = playerVisualRepresentation
		};
	}

	private string AidOf(PartyMemberCardWire card)
	{
		if (state.LocalSessionByProfileHash.TryGetValue(card.ProfileId, out var value) && !string.IsNullOrEmpty(value))
		{
			return profileResolver.GetNativeGroupProfile(value, card.PreferredSide).Aid.ToString();
		}
		return NekoState.ProfileIdToAid(card.ProfileId).ToString();
	}

	private static string Short(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			if (s.Length > 8)
			{
				return s.Substring(0, 8);
			}
			return s;
		}
		return "-";
	}

	private void TryRegisterRaidAccounts(IReadOnlyList<string> selfIds)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		if (!Ready || !state.Config.EnableCrossRaid || selfIds.Count == 0)
		{
			return;
		}
		try
		{
			HubClient val = hub;
			string token = state.Token;
			RaidEventsPullWire val2 = new RaidEventsPullWire();
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = state.AccountId;
			val2.TargetAccountIds = list;
			val2.SelfProfileIds = selfIds.ToList();
			val.RaidEventsAsync(token, val2).GetAwaiter().GetResult();
		}
		catch
		{
		}
	}
}
