using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FikaServer.Models.Enums;
using FikaServer.Models.Fika;
using FikaServer.Models.Fika.Routes.Client;
using FikaServer.Services;
using FikaServer.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using NekoNet.Server.Core;
using NekoNet.Server.Core.Models;
using NekoParty.Server.Core.Models;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.RaidSettings.TimeAndWeather;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoCrossRaidService(NekoState state, HubClient hub, NekoHooks hooks, HubWsClient hubWs, MatchService matchService, NekoCrossPartyService crossParty, NekoGroupService groupService, NotificationWebSocket notificationWebSocket, NekoModFingerprintService fingerprint, JsonUtil jsonUtil, ISptLogger<NekoCrossRaidService> logger)
{
	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly ConcurrentDictionary<string, byte> _shadowLeaderHashes = new ConcurrentDictionary<string, byte>();

	private readonly ConcurrentDictionary<string, string> _shadowToLeaderHash = new ConcurrentDictionary<string, string>();

	private readonly ConcurrentDictionary<string, byte> _modMismatchWarned = new ConcurrentDictionary<string, byte>();

	private int _started;

	public bool Ready
	{
		get
		{
			if (state.Config.Enabled && state.Config.EnableParty && state.Config.EnableCrossRaid && state.Authenticated)
			{
				return !string.IsNullOrEmpty(state.Token);
			}
			return false;
		}
	}

	public bool IsShadowMatch(MongoId matchId)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return _shadowToLeaderHash.ContainsKey(((object)matchId).ToString());
	}

	public void CleanStaleShadowPlayer(MongoId matchId, MongoId playerId)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (matchService.Matches.TryGetValue(matchId, out var value) && value.Players.Remove(playerId))
		{
			logger.Info("[NekoCrossRaid] 影子 match 残留玩家记录已清理，重新登记 player=" + Short(((object)playerId).ToString()), (Exception)null);
		}
	}

	public void Start()
	{
		if (Interlocked.Exchange(ref _started, 1) != 1)
		{
			hooks.HubRawMessage += OnHubRawMessage;
			hubWs.EnsureStarted();
			RegisterSelfRaidAccounts();
			logger.Success("[NekoCrossRaid] 跨服战局桥接已启用（Hub raid 插件 + WS + 拉取兜底）。", (Exception)null);
		}
	}

	public void RegisterSelfRaidAccounts()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		if (!Ready)
		{
			return;
		}
		List<string> list = state.LocalSessionByProfileHash.Keys.ToList();
		if (list.Count == 0)
		{
			return;
		}
		try
		{
			HubClient val = hub;
			string token = state.Token;
			RaidEventsPullWire val2 = new RaidEventsPullWire();
			int num = 1;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			CollectionsMarshal.AsSpan(list2)[0] = state.AccountId;
			val2.TargetAccountIds = list2;
			val2.SelfProfileIds = list;
			val.RaidEventsAsync(token, val2).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Warning("[NekoCrossRaid] 登记 raid 账户映射失败: " + ex.Message, (Exception)null);
		}
	}

	private static string SelfHash(MongoId sessionId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return NekoState.Hash(((object)sessionId).ToString());
	}

	public void TryAnnounceHostMatch(MongoId hostSessionId, FikaMatch match)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		if (!Ready || match == (FikaMatch)null)
		{
			return;
		}
		string text = SelfHash(hostSessionId);
		if (!TryGetRemotePartyMemberHashes(hostSessionId, out var memberHashes))
		{
			return;
		}
		try
		{
			RaidSnapshotWire snapshot = BuildSnapshotWire(match, text, state.AccountId);
			RaidAnnounceWire val = new RaidAnnounceWire
			{
				LeaderProfileId = text,
				Snapshot = snapshot,
				MemberProfileIds = memberHashes
			};
			ApiResponse<RaidSnapshotResultWire> result = hub.RaidAnnounceAsync(state.Token, val).GetAwaiter().GetResult();
			if (result != null && result.Ok)
			{
				logger.Info($"[NekoCrossRaid] 已上报战局 leader={Short(text)} members={memberHashes.Count}", (Exception)null);
			}
			else
			{
				logger.Warning("[NekoCrossRaid] 上报战局被拒 leader=" + Short(text) + " error=" + (result?.Error ?? "null"), (Exception)null);
			}
		}
		catch (Exception value)
		{
			logger.Error($"[NekoCrossRaid] 上报战局失败: {value}", (Exception)null);
		}
	}

	public void TryUploadLocationAsync(MongoId hostSessionId, LocationBase locationData)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return;
		}
		Task.Run(delegate
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				string text = SelfHash(hostSessionId);
				string s = jsonUtil.Serialize<LocationBase>(locationData, false);
				byte[] array = Gzip(Encoding.UTF8.GetBytes(s));
				RaidLocationUploadWire val = new RaidLocationUploadWire
				{
					LeaderProfileId = text,
					GzipBase64 = Convert.ToBase64String(array)
				};
				hub.RaidLocationUploadAsync(state.Token, val).GetAwaiter().GetResult();
				logger.Info($"[NekoCrossRaid] LocationData 已上传 leader={Short(text)} bytes={array.Length}", (Exception)null);
				MatchService service = ServiceLocator.ServiceProvider.GetService<MatchService>();
				FikaMatch val2 = ((service != null) ? service.GetMatch((MongoId?)hostSessionId) : null);
				if (val2 != (FikaMatch)null)
				{
					TryAnnounceHostMatchWithLocationFlag(hostSessionId, val2, hasLocationBlob: true);
				}
			}
			catch (Exception ex)
			{
				logger.Error("[NekoCrossRaid] LocationData 上传失败: " + ex.Message, (Exception)null);
			}
		});
	}

	private void TryAnnounceHostMatchWithLocationFlag(MongoId hostSessionId, FikaMatch match, bool hasLocationBlob)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		if (!Ready || match == (FikaMatch)null)
		{
			return;
		}
		string text = SelfHash(hostSessionId);
		if (!TryGetRemotePartyMemberHashes(hostSessionId, out var memberHashes))
		{
			return;
		}
		RaidSnapshotWire val = BuildSnapshotWire(match, text, state.AccountId);
		val.HasLocationBlob = hasLocationBlob;
		RaidAnnounceWire val2 = new RaidAnnounceWire
		{
			LeaderProfileId = text,
			Snapshot = val,
			MemberProfileIds = memberHashes
		};
		try
		{
			hub.RaidAnnounceAsync(state.Token, val2).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 上传后重新上报战局失败: " + ex.Message, (Exception)null);
		}
	}

	public void TryEndHostRaid(MongoId hostSessionId)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		if (!Ready)
		{
			return;
		}
		try
		{
			hub.RaidEndAsync(state.Token, new RaidEndWire
			{
				LeaderProfileId = SelfHash(hostSessionId)
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 结束战局上报失败: " + ex.Message, (Exception)null);
		}
	}

	public void TryForwardShadowJoin(MongoId matchId, MongoId playerSessionId, bool isSpectator)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		if (!Ready || !TryResolveShadowLeader(((object)matchId).ToString(), out var leaderHash))
		{
			return;
		}
		try
		{
			hub.RaidMemberAddAsync(state.Token, new RaidMemberWire
			{
				LeaderProfileId = leaderHash,
				PlayerProfileId = SelfHash(playerSessionId),
				IsSpectator = isSpectator
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 转发 join 失败: " + ex.Message, (Exception)null);
		}
	}

	public void TryForwardShadowLeave(MongoId matchId, MongoId playerSessionId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		if (!Ready || !TryResolveShadowLeader(((object)matchId).ToString(), out var leaderHash))
		{
			return;
		}
		try
		{
			hub.RaidMemberRemoveAsync(state.Token, new RaidMemberWire
			{
				LeaderProfileId = leaderHash,
				PlayerProfileId = SelfHash(playerSessionId)
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 转发 leave 失败: " + ex.Message, (Exception)null);
		}
	}

	public TransitConfigWire? GetTransitConfig()
	{
		if (!Ready)
		{
			return null;
		}
		try
		{
			return hub.TransitConfigAsync(state.Token).GetAwaiter().GetResult()?.Data;
		}
		catch
		{
			return null;
		}
	}

	public TransitRelayAllocateResultWire? AllocateRelay(string serverGuid, MongoId clientSessionId)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		if (!Ready)
		{
			return null;
		}
		try
		{
			return hub.TransitRelayAllocateAsync(state.Token, new TransitRelayAllocateWire
			{
				ServerGuid = serverGuid,
				ClientProfileId = SelfHash(clientSessionId)
			}).GetAwaiter().GetResult()?.Data;
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 中继 allocate 失败: " + ex.Message, (Exception)null);
			return null;
		}
	}

	public void PollEvents()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Expected O, but got Unknown
		if (!Ready)
		{
			return;
		}
		List<string> selfProfileIds = state.LocalSessionByProfileHash.Keys.ToList();
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
			val2.SelfProfileIds = selfProfileIds;
			ApiResponse<RaidEventsResultWire> result = val.RaidEventsAsync(token, val2).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Events.Count == 0)
			{
				return;
			}
			List<string> list2 = new List<string>(result.Data.Events.Count);
			foreach (RaidEventWire @event in result.Data.Events)
			{
				Dispatch(@event);
				list2.Add(@event.Id);
			}
			hub.RaidEventsAckAsync(state.Token, new RaidEventsAckWire
			{
				Ids = list2
			}).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			logger.Error("[NekoCrossRaid] 拉取 raid 事件失败: " + ex.Message, (Exception)null);
		}
	}

	private void OnHubRawMessage(string json)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		if (string.IsNullOrEmpty(json) || !json.Contains("\"raid\"", StringComparison.Ordinal))
		{
			return;
		}
		HubWsEnvelope<RaidEventWire> val;
		try
		{
			val = JsonSerializer.Deserialize<HubWsEnvelope<RaidEventWire>>(json, JsonOpts);
		}
		catch
		{
			return;
		}
		if (val == null || !string.Equals(val.Channel, "raid", StringComparison.Ordinal) || val.Event == null)
		{
			return;
		}
		Dispatch(val.Event);
		try
		{
			HubClient val2 = hub;
			string token = state.Token;
			RaidEventsAckWire val3 = new RaidEventsAckWire();
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = val.Event.Id;
			val3.Ids = list;
			val2.RaidEventsAckAsync(token, val3).GetAwaiter().GetResult();
		}
		catch
		{
		}
	}

	private void Dispatch(RaidEventWire ev)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected I4, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RaidEventKindWire kind = ev.Kind;
			switch ((int)kind)
			{
			case 0:
				if (ev.Snapshot != null)
				{
					EnsureShadowMatch(ev.Snapshot);
					TryCreateRemoteRaidInvites(ev.Snapshot);
				}
				break;
			case 1:
				if (ev.Snapshot != null)
				{
					EnsureShadowMatch(ev.Snapshot);
					TryCreateRemoteRaidInvites(ev.Snapshot);
				}
				break;
			case 2:
				HandleRemotePlayerJoined(ev);
				break;
			case 3:
				HandleRemotePlayerLeft(ev);
				break;
			case 4:
				if (ev.Snapshot != null)
				{
					RemoveShadowMatch(ev.Snapshot.LeaderProfileId, ev.Snapshot.RaidCode);
				}
				break;
			}
		}
		catch (Exception ex)
		{
			logger.Error($"[NekoCrossRaid] Dispatch({ev.Kind}) 异常: {ex.Message}", (Exception)null);
		}
	}

	private void HandleRemotePlayerJoined(RaidEventWire ev)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(ev.PlayerProfileId) || ev.Snapshot == null || !string.Equals(ev.Snapshot.HostAccountId, state.AccountId, StringComparison.Ordinal))
		{
			return;
		}
		MongoId? val = ResolveLocalLeaderSessionId(ev.Snapshot.LeaderProfileId);
		if (val.HasValue)
		{
			MongoId val2 = ResolvePlayerMongoId(ev.PlayerProfileId);
			if (!matchService.Matches.TryGetValue(val.Value, out var value) || !value.Players.ContainsKey(val2))
			{
				matchService.AddPlayerToMatch(val.Value, val2, new FikaPlayer
				{
					GroupId = string.Empty,
					IsDead = false,
					IsSpectator = false
				});
				logger.Info("[NekoCrossRaid] 远程队员已加入真实 match player=" + Short(ev.PlayerProfileId), (Exception)null);
			}
		}
	}

	private void HandleRemotePlayerLeft(RaidEventWire ev)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (!string.IsNullOrEmpty(ev.PlayerProfileId) && ev.Snapshot != null && string.Equals(ev.Snapshot.HostAccountId, state.AccountId, StringComparison.Ordinal))
		{
			MongoId? val = ResolveLocalLeaderSessionId(ev.Snapshot.LeaderProfileId);
			if (val.HasValue)
			{
				MongoId val2 = ResolvePlayerMongoId(ev.PlayerProfileId);
				matchService.RemovePlayerFromMatch(val.Value, val2);
			}
		}
	}

	private void EnsureShadowMatch(RaidSnapshotWire snap)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Expected O, but got Unknown
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		if (string.Equals(snap.HostAccountId, state.AccountId, StringComparison.Ordinal))
		{
			return;
		}
		MongoId key = default(MongoId);
		((MongoId)(ref key))._002Ector(NekoState.ProfileIdToLocalUserId(snap.LeaderProfileId));
		state.RegisterRemote(snap.LeaderProfileId);
		_shadowLeaderHashes[snap.LeaderProfileId] = 0;
		_shadowToLeaderHash[((object)key).ToString()] = snap.LeaderProfileId;
		if (matchService.Matches.TryGetValue(key, out var value))
		{
			UpdateShadowMatch(value, snap);
			return;
		}
		LocationBase val = null;
		if (snap.HasLocationBlob)
		{
			val = TryDownloadLocation(snap.LeaderProfileId);
		}
		if (val == (LocationBase)null)
		{
			val = CreatePlaceholderLocation(snap.Location);
		}
		FikaMatch val2 = new FikaMatch
		{
			Ips = (snap.Ips ?? Array.Empty<string>()),
			ServerGuid = (Guid.TryParse(snap.ServerGuid, out var result) ? result : Guid.Empty),
			Port = (ushort)Math.Clamp(snap.Port, 0, 65535),
			HostUsername = snap.HostUsername,
			Timestamp = snap.Timestamp,
			CRC32 = snap.Crc32,
			GameVersion = snap.GameVersion,
			RaidConfig = ParseRaidConfig(snap),
			LocationData = val,
			Status = (EFikaMatchStatus)snap.Status,
			Timeout = 0,
			Players = new Dictionary<MongoId, FikaPlayer>(),
			Side = (EFikaSide)snap.Side,
			Time = (EFikaTime)snap.Time,
			RaidCode = snap.RaidCode,
			NatPunch = snap.NatPunch,
			UseFikaNatPunchServer = snap.UseFikaNatPunchServer,
			IsHeadless = snap.IsHeadless,
			Raids = 0,
			CustomRaidSettings = ParseCustomRaidSettings(snap)
		};
		MergeSnapshotPlayers(val2, snap);
		matchService.Matches[key] = val2;
		logger.Info("[NekoCrossRaid] 影子 match 已注入 shadowId=" + Short(((object)key).ToString()) + " leader=" + Short(snap.LeaderProfileId), (Exception)null);
	}

	private void UpdateShadowMatch(FikaMatch match, RaidSnapshotWire snap)
	{
		if (!string.IsNullOrEmpty(snap.RaidCode) && !string.IsNullOrEmpty(match.RaidCode) && !string.Equals(match.RaidCode, snap.RaidCode, StringComparison.Ordinal))
		{
			match.Players.Clear();
			logger.Info($"[NekoCrossRaid] 影子 match 换局（{match.RaidCode} → {snap.RaidCode}），已清空残留玩家记录", (Exception)null);
		}
		match.Ips = snap.Ips ?? match.Ips;
		if (Guid.TryParse(snap.ServerGuid, out var result) && result != Guid.Empty)
		{
			match.ServerGuid = result;
		}
		if (snap.Port > 0)
		{
			match.Port = (ushort)Math.Clamp(snap.Port, 0, 65535);
		}
		match.HostUsername = snap.HostUsername;
		match.Timestamp = snap.Timestamp;
		match.CRC32 = snap.Crc32;
		match.GameVersion = snap.GameVersion;
		match.Status = (EFikaMatchStatus)snap.Status;
		match.Side = (EFikaSide)snap.Side;
		match.Time = (EFikaTime)snap.Time;
		match.RaidCode = snap.RaidCode;
		match.NatPunch = snap.NatPunch;
		match.UseFikaNatPunchServer = snap.UseFikaNatPunchServer;
		match.IsHeadless = snap.IsHeadless;
		if (!string.IsNullOrEmpty(snap.RaidConfigJson))
		{
			match.RaidConfig = ParseRaidConfig(snap);
		}
		match.CustomRaidSettings = ParseCustomRaidSettings(snap);
		if (snap.HasLocationBlob && IsPlaceholderLocation(match.LocationData))
		{
			LocationBase val = TryDownloadLocation(snap.LeaderProfileId);
			if (val != (LocationBase)null)
			{
				match.LocationData = val;
				logger.Info("[NekoCrossRaid] 影子 match LocationData 已补齐 leader=" + Short(snap.LeaderProfileId), (Exception)null);
			}
		}
		MergeSnapshotPlayers(match, snap);
	}

	private void MergeSnapshotPlayers(FikaMatch match, RaidSnapshotWire snap)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		foreach (string playerProfileId in snap.PlayerProfileIds)
		{
			if (!state.LocalSessionByProfileHash.ContainsKey(playerProfileId))
			{
				MongoId key = ResolvePlayerMongoId(playerProfileId);
				if (!match.Players.ContainsKey(key))
				{
					match.Players[key] = new FikaPlayer
					{
						GroupId = string.Empty,
						IsDead = false,
						IsSpectator = false
					};
				}
			}
		}
	}

	private GetRaidConfigurationRequestData ParseRaidConfig(RaidSnapshotWire snap)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Expected O, but got Unknown
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		GetRaidConfigurationRequestData val = null;
		if (!string.IsNullOrEmpty(snap.RaidConfigJson))
		{
			try
			{
				val = jsonUtil.Deserialize<GetRaidConfigurationRequestData>(snap.RaidConfigJson);
			}
			catch
			{
			}
		}
		if (val == (GetRaidConfigurationRequestData)null)
		{
			val = new GetRaidConfigurationRequestData
			{
				Location = snap.Location
			};
		}
		GetRaidConfigurationRequestData val2 = val;
		bool? metabolismDisabled = ((RaidSettings)val2).MetabolismDisabled;
		bool valueOrDefault = metabolismDisabled == true;
		if (!metabolismDisabled.HasValue)
		{
			valueOrDefault = false;
			GetRaidConfigurationRequestData val3 = val2;
			bool? metabolismDisabled2 = valueOrDefault;
			((RaidSettings)val3).MetabolismDisabled = metabolismDisabled2;
		}
		val2 = val;
		PlayersSpawnPlace? playersSpawnPlace = ((RaidSettings)val2).PlayersSpawnPlace;
		PlayersSpawnPlace valueOrDefault2 = playersSpawnPlace.GetValueOrDefault();
		if (!playersSpawnPlace.HasValue)
		{
			valueOrDefault2 = (PlayersSpawnPlace)0;
			GetRaidConfigurationRequestData val4 = val2;
			PlayersSpawnPlace? playersSpawnPlace2 = valueOrDefault2;
			((RaidSettings)val4).PlayersSpawnPlace = playersSpawnPlace2;
		}
		val2 = val;
		TimeAndWeatherSettings val6;
		if (((RaidSettings)val2).TimeAndWeatherSettings == (TimeAndWeatherSettings)null)
		{
			GetRaidConfigurationRequestData val5 = val2;
			val6 = (((RaidSettings)val5).TimeAndWeatherSettings = new TimeAndWeatherSettings());
		}
		val6 = ((RaidSettings)val).TimeAndWeatherSettings;
		int? hourOfDay = val6.HourOfDay;
		int valueOrDefault3 = hourOfDay.GetValueOrDefault();
		if (!hourOfDay.HasValue)
		{
			valueOrDefault3 = 12;
			TimeAndWeatherSettings val8 = val6;
			int? hourOfDay2 = valueOrDefault3;
			val8.HourOfDay = hourOfDay2;
		}
		val6 = ((RaidSettings)val).TimeAndWeatherSettings;
		TimeFlowType? timeFlowType = val6.TimeFlowType;
		TimeFlowType valueOrDefault4 = timeFlowType.GetValueOrDefault();
		if (!timeFlowType.HasValue)
		{
			valueOrDefault4 = (TimeFlowType)4;
			TimeAndWeatherSettings val9 = val6;
			TimeFlowType? timeFlowType2 = valueOrDefault4;
			val9.TimeFlowType = timeFlowType2;
		}
		return val;
	}

	private FikaCustomRaidSettings ParseCustomRaidSettings(RaidSnapshotWire snap)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		if (!string.IsNullOrEmpty(snap.CustomRaidSettingsJson))
		{
			try
			{
				FikaCustomRaidSettings val = JsonSerializer.Deserialize<FikaCustomRaidSettings>(snap.CustomRaidSettingsJson, JsonOpts);
				if (val != (FikaCustomRaidSettings)null)
				{
					return val;
				}
			}
			catch
			{
			}
		}
		return new FikaCustomRaidSettings();
	}

	private static bool IsPlaceholderLocation(LocationBase? location)
	{
		if (((location != (LocationBase)null) ? location.Loot : null) != null)
		{
			return !location.Loot.Any();
		}
		return true;
	}

	private void TryCreateRemoteRaidInvites(RaidSnapshotWire snap)
	{
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		if (string.Equals(snap.HostAccountId, state.AccountId, StringComparison.Ordinal))
		{
			return;
		}
		string[] ips = snap.Ips;
		if (ips == null || ips.Length == 0 || snap.Port <= 0 || !Guid.TryParse(snap.ServerGuid, out var result) || result == Guid.Empty)
		{
			logger.Info("[NekoCrossRaid] 战局公告尚无有效端点（sethost 未到），暂缓发送战局邀请 leader=" + Short(snap.LeaderProfileId), (Exception)null);
			return;
		}
		string text = NekoState.ProfileIdToLocalUserId(snap.LeaderProfileId);
		string leaderNickname = (string.IsNullOrWhiteSpace(snap.HostUsername) ? "Leader" : snap.HostUsername);
		string partyId;
		List<(string, string)> list;
		try
		{
			ApiResponse<PartySnapshotResultWire> result2 = hub.PartyCurrentAsync(state.Token, snap.LeaderProfileId).GetAwaiter().GetResult();
			if (result2 == null || !result2.Ok || result2.Data == null || result2.Data.Snapshot == null)
			{
				return;
			}
			partyId = result2.Data.Snapshot.PartyId;
			list = new List<(string, string)>();
			foreach (PartyMemberCardWire member in result2.Data.Snapshot.Members)
			{
				if (!member.IsLeader && state.LocalSessionByProfileHash.TryGetValue(member.ProfileId, out var value) && !string.IsNullOrEmpty(value))
				{
					list.Add((value, member.Nickname));
				}
			}
		}
		catch (Exception ex)
		{
			logger.Warning("[NekoCrossRaid] 拉取跨服队伍失败，跳过战局邀请: " + ex.Message, (Exception)null);
			return;
		}
		if (list.Count == 0 || !CheckModFingerprint(snap, leaderNickname, list))
		{
			return;
		}
		List<NekoRaidInviteData> list2 = groupService.CreateCrossServerRaidInvites(partyId, text, leaderNickname, text, snap.RaidCode, snap.Location, snap.Side, snap.Time, list);
		foreach (NekoRaidInviteData item in list2)
		{
			try
			{
				notificationWebSocket.SendAsync<NekoRaidInviteReceivedNotification>(new MongoId(item.TargetProfileId), new NekoRaidInviteReceivedNotification
				{
					InviteId = item.InviteId,
					PartyId = item.PartyId,
					LeaderProfileId = item.LeaderProfileId,
					LeaderNickname = item.LeaderNickname,
					TargetProfileId = item.TargetProfileId,
					TargetNickname = item.TargetNickname,
					ServerId = item.ServerId,
					RaidCode = item.RaidCode,
					Location = item.Location,
					Side = item.Side,
					TargetSide = item.TargetSide,
					Time = item.Time,
					ExpiresAtUnix = item.ExpiresAtUnix
				}).GetAwaiter().GetResult();
			}
			catch (Exception ex2)
			{
				logger.Warning("[NekoCrossRaid] 推送战局邀请 WS 失败 target=" + Short(item.TargetProfileId) + ": " + ex2.Message, (Exception)null);
			}
		}
		if (list2.Count > 0)
		{
			logger.Info($"[NekoCrossRaid] 已向 {list2.Count} 名本地队员推送跨服战局邀请 shadow={Short(text)}", (Exception)null);
		}
	}

	private void RemoveShadowMatch(string leaderHash, string endedRaidCode = "")
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		MongoId key = default(MongoId);
		((MongoId)(ref key))._002Ector(NekoState.ProfileIdToLocalUserId(leaderHash));
		if (!string.IsNullOrEmpty(endedRaidCode) && matchService.Matches.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value.RaidCode) && !string.Equals(value.RaidCode, endedRaidCode, StringComparison.Ordinal))
		{
			groupService.ExpireRaidInvitesForServer(((object)key).ToString(), endedRaidCode);
			logger.Info("[NekoCrossRaid] 忽略过期 Ended（影子 match 已是新一局） leader=" + Short(leaderHash) + " endedRaid=" + endedRaidCode, (Exception)null);
			return;
		}
		groupService.ExpireRaidInvitesForServer(((object)key).ToString());
		if (matchService.Matches.TryRemove(key, out var _))
		{
			_shadowToLeaderHash.TryRemove(((object)key).ToString(), out var _);
			_shadowLeaderHashes.TryRemove(leaderHash, out var _);
			logger.Info("[NekoCrossRaid] 影子 match 已移除 leader=" + Short(leaderHash), (Exception)null);
		}
	}

	private LocationBase? TryDownloadLocation(string leaderHash)
	{
		try
		{
			ApiResponse<RaidLocationDownloadResultWire> result = hub.RaidLocationDownloadAsync(state.Token, leaderHash).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || string.IsNullOrEmpty(result.Data.GzipBase64))
			{
				return null;
			}
			byte[] bytes = Gunzip(Convert.FromBase64String(result.Data.GzipBase64));
			string text = Encoding.UTF8.GetString(bytes);
			return jsonUtil.Deserialize<LocationBase>(text);
		}
		catch (Exception ex)
		{
			logger.Warning("[NekoCrossRaid] 下载 LocationData 失败: " + ex.Message, (Exception)null);
			return null;
		}
	}

	private static LocationBase CreatePlaceholderLocation(string locationId)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		return new LocationBase
		{
			Id = (string.IsNullOrEmpty(locationId) ? "unknown" : locationId)
		};
	}

	private MongoId ResolvePlayerMongoId(string profileHash)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (state.LocalSessionByProfileHash.TryGetValue(profileHash, out var value) && !string.IsNullOrEmpty(value))
		{
			return new MongoId(value);
		}
		state.RegisterRemote(profileHash);
		return new MongoId(NekoState.ProfileIdToLocalUserId(profileHash));
	}

	private MongoId? ResolveLocalLeaderSessionId(string leaderHash)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (state.LocalSessionByProfileHash.TryGetValue(leaderHash, out var value) && !string.IsNullOrEmpty(value))
		{
			return new MongoId(value);
		}
		return null;
	}

	private bool TryResolveShadowLeader(string shadowMatchId, out string leaderHash)
	{
		if (_shadowToLeaderHash.TryGetValue(shadowMatchId, out leaderHash))
		{
			return !string.IsNullOrEmpty(leaderHash);
		}
		return false;
	}

	private bool TryGetRemotePartyMemberHashes(MongoId leaderSid, out List<string> memberHashes)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		memberHashes = new List<string>();
		if (!crossParty.Ready)
		{
			return false;
		}
		string text = SelfHash(leaderSid);
		state.LocalSessionByProfileHash[text] = ((object)leaderSid).ToString();
		try
		{
			ApiResponse<PartySnapshotResultWire> result = hub.PartyCurrentAsync(state.Token, text).GetAwaiter().GetResult();
			if (result == null || !result.Ok || result.Data == null || result.Data.Snapshot == null || result.Data.Snapshot.Members.Count < 2)
			{
				return false;
			}
			foreach (PartyMemberCardWire member in result.Data.Snapshot.Members)
			{
				if (!string.Equals(member.ProfileId, text, StringComparison.Ordinal))
				{
					memberHashes.Add(member.ProfileId);
				}
			}
			return memberHashes.Any((string h) => !state.LocalSessionByProfileHash.ContainsKey(h));
		}
		catch
		{
			return false;
		}
	}

	private bool CheckModFingerprint(RaidSnapshotWire snap, string leaderNickname, List<(string TargetProfileId, string TargetNickname)> localTargets)
	{
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		NekoModFingerprintService.CompareResult compareResult = fingerprint.Compare(snap.ModFingerprintJson, leaderNickname);
		if (!compareResult.Mismatch)
		{
			return true;
		}
		bool crossRaidBlockOnModMismatch = state.Config.CrossRaidBlockOnModMismatch;
		string key = snap.LeaderProfileId + ":" + snap.RaidCode;
		if (_modMismatchWarned.TryAdd(key, 0))
		{
			if (_modMismatchWarned.Count > 256)
			{
				_modMismatchWarned.Clear();
			}
			logger.Warning($"[NekoCrossRaid] 队长服与本服 MOD 配置不一致 leader={Short(snap.LeaderProfileId)}，共 {compareResult.Diffs.Count} 项差异" + (crossRaidBlockOnModMismatch ? "（已按配置拦截本局邀请）" : "（仅告警，捡到本服不存在的物品会出问题；可设 crossRaidBlockOnModMismatch=true 拦截）") + ":\n  - " + string.Join("\n  - ", compareResult.Diffs), (Exception)null);
			foreach (var (text, _) in localTargets)
			{
				try
				{
					notificationWebSocket.SendAsync<NekoModMismatchNotification>(new MongoId(text), new NekoModMismatchNotification
					{
						PeerNickname = leaderNickname,
						Report = compareResult.Report,
						Blocked = crossRaidBlockOnModMismatch
					}).GetAwaiter().GetResult();
				}
				catch (Exception ex)
				{
					logger.Warning("[NekoCrossRaid] 推送 MOD 差异报告失败 target=" + Short(text) + ": " + ex.Message, (Exception)null);
				}
			}
		}
		return !crossRaidBlockOnModMismatch;
	}

	private RaidSnapshotWire BuildSnapshotWire(FikaMatch match, string leaderHash, string hostAccountId)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected I4, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected I4, but got Unknown
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected I4, but got Unknown
		//IL_00ca: Expected O, but got Unknown
		RaidSnapshotWire val = new RaidSnapshotWire
		{
			LeaderProfileId = leaderHash,
			HostAccountId = hostAccountId,
			ServerGuid = match.ServerGuid.ToString(),
			Ips = match.Ips,
			Port = match.Port,
			NatPunch = match.NatPunch,
			UseFikaNatPunchServer = match.UseFikaNatPunchServer,
			IsHeadless = match.IsHeadless,
			HostUsername = match.HostUsername,
			Timestamp = match.Timestamp,
			Crc32 = match.CRC32,
			GameVersion = match.GameVersion,
			RaidCode = match.RaidCode,
			Status = (int)match.Status,
			Side = (int)match.Side,
			Time = (int)match.Time
		};
		GetRaidConfigurationRequestData raidConfig = match.RaidConfig;
		object obj = ((raidConfig != (GetRaidConfigurationRequestData)null) ? ((RaidSettings)raidConfig).Location : null);
		if (obj == null)
		{
			LocationBase locationData = match.LocationData;
			obj = ((locationData != (LocationBase)null) ? locationData.Id : null) ?? "";
		}
		val.Location = (string)obj;
		val.RaidConfigJson = JsonSerializer.Serialize<GetRaidConfigurationRequestData>(match.RaidConfig, JsonOpts);
		val.CustomRaidSettingsJson = ((match.CustomRaidSettings == (FikaCustomRaidSettings)null) ? "" : JsonSerializer.Serialize<FikaCustomRaidSettings>(match.CustomRaidSettings, JsonOpts));
		val.ModFingerprintJson = fingerprint.Json;
		val.HasLocationBlob = false;
		val.PlayerProfileIds = BuildSnapshotPlayerHashes(match, leaderHash);
		val.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return val;
	}

	private List<string> BuildSnapshotPlayerHashes(FikaMatch match, string leaderHash)
	{
		List<string> list = match.Players.Keys.Select(delegate(MongoId k)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			return PlayerKeyToProfileHash(((object)k).ToString());
		}).ToList();
		if (!list.Contains(leaderHash))
		{
			list.Insert(0, leaderHash);
		}
		return list;
	}

	private string PlayerKeyToProfileHash(string playerKey)
	{
		if (!state.RemoteByLocalUserId.TryGetValue(playerKey, out var value) || string.IsNullOrEmpty(value))
		{
			return NekoState.Hash(playerKey);
		}
		return value;
	}

	private static byte[] Gzip(byte[] input)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
		{
			gZipStream.Write(input, 0, input.Length);
		}
		return memoryStream.ToArray();
	}

	private static byte[] Gunzip(byte[] input)
	{
		using MemoryStream stream = new MemoryStream(input);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		gZipStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
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
}
