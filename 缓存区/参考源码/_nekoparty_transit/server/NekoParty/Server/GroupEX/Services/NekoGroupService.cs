using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FikaServer.Models.Enums;
using FikaServer.Models.Fika.WebSocket.Notifications;
using FikaServer.WebSockets;
using NekoParty.Server.Core.Models;
using NekoParty.Server.Core.Services;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoGroupService(NekoProfileResolver profileResolver, TimeUtil timeUtil, ISptLogger<NekoGroupService> logger, NotificationWebSocket notificationWebSocket, LocaleService localeService, NekoNativeGroupNotifier nativeGroupNotifier)
{
	private static readonly object _sync = new object();

	private static int _instanceCounter;

	private readonly int _instanceId = Interlocked.Increment(ref _instanceCounter);

	private static readonly Dictionary<string, NekoPartyData> _partiesByLeader = new Dictionary<string, NekoPartyData>();

	private static readonly Dictionary<string, string> _leaderByMember = new Dictionary<string, string>();

	private static readonly Dictionary<string, NekoRaidInviteData> _raidInvitesById = new Dictionary<string, NekoRaidInviteData>();

	private static readonly Dictionary<string, NekoPartyInviteData> _partyInvitesById = new Dictionary<string, NekoPartyInviteData>();

	private static readonly Dictionary<string, int> _preferredSideByProfileId = new Dictionary<string, int>();

	private static readonly ConcurrentDictionary<string, string> _playerVisualByAccount = new ConcurrentDictionary<string, string>();

	private const int InviteExpirationSeconds = 120;

	public void StorePlayerVisual(string accountId, string visualJson)
	{
		if (!string.IsNullOrWhiteSpace(accountId) && !string.IsNullOrWhiteSpace(visualJson))
		{
			_playerVisualByAccount[accountId] = visualJson;
		}
	}

	public string TryGetPlayerVisual(string accountId)
	{
		if (!string.IsNullOrWhiteSpace(accountId) && _playerVisualByAccount.TryGetValue(accountId, out var value))
		{
			return value;
		}
		return null;
	}

	public NekoPartyData UpsertLeaderParty(MongoId leaderSessionId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		string text = ((object)leaderSessionId).ToString();
		lock (_sync)
		{
			if (_partiesByLeader.TryGetValue(text, out var value))
			{
				return CloneParty(value);
			}
			NekoPartyData val = new NekoPartyData
			{
				PartyId = Guid.NewGuid().ToString("N"),
				LeaderProfileId = text,
				Status = (ENekoPartyStatus)0
			};
			int num = 1;
			List<NekoPartyMember> list = new List<NekoPartyMember>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new NekoPartyMember
			{
				ProfileId = text,
				Nickname = profileResolver.GetNicknameByProfileId(text),
				IsLeader = true,
				PreferredSide = GetPreferredSide(text)
			};
			val.Members = list;
			NekoPartyData val2 = val;
			_partiesByLeader[text] = val2;
			_leaderByMember[text] = text;
			return CloneParty(val2);
		}
	}

	public NekoPartyData? GetPartyByPlayer(MongoId playerSessionId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		string profileId = ((object)playerSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!TryGetPartyByProfileId(profileId, out var _, out var party))
			{
				return null;
			}
			return CloneParty(party);
		}
	}

	public string DebugPartyTable()
	{
		lock (_sync)
		{
			return "count=" + _partiesByLeader.Count + " keys=[" + string.Join(",", _partiesByLeader.Keys.Select(delegate(string k)
			{
				string text;
				if (k == null || k.Length <= 8)
				{
					text = k;
					if (text == null)
					{
						return "null";
					}
				}
				else
				{
					text = k.Substring(0, 8);
				}
				return text;
			})) + "]";
		}
	}

	public string? TryResolveLeaderProfileIdByNickname(string? leaderNickname)
	{
		if (string.IsNullOrWhiteSpace(leaderNickname))
		{
			return null;
		}
		lock (_sync)
		{
			foreach (KeyValuePair<string, NekoPartyData> item in _partiesByLeader)
			{
				NekoPartyData value = item.Value;
				NekoPartyMember val = ((value == null) ? null : value.Members?.FirstOrDefault((NekoPartyMember m) => m.IsLeader));
				if (val != (NekoPartyMember)null && string.Equals(val.Nickname, leaderNickname, StringComparison.OrdinalIgnoreCase))
				{
					return item.Key;
				}
			}
		}
		return null;
	}

	public NekoPartyOperationResponse InviteMember(MongoId leaderSessionId, string targetProfileId)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Expected O, but got Unknown
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Expected O, but got Unknown
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Expected O, but got Unknown
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0369: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Expected O, but got Unknown
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Expected O, but got Unknown
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		string leaderProfileId = ((object)leaderSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_partiesByLeader.TryGetValue(leaderProfileId, out var value))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party not found for leader"
				};
			}
			if (string.IsNullOrWhiteSpace(targetProfileId))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Target profile id is required"
				};
			}
			if (targetProfileId == leaderProfileId)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Leader is already in party"
				};
			}
			if (!profileResolver.ProfileExists(targetProfileId))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Target profile does not exist"
				};
			}
			if (_leaderByMember.TryGetValue(targetProfileId, out var value2) && value2 != leaderProfileId)
			{
				if (!_partiesByLeader.TryGetValue(value2, out var value3) || (int)value3.Status != 0 || value3.Members.Count != 1 || !(value3.Members[0].ProfileId == targetProfileId) || !value3.Members[0].IsLeader)
				{
					return new NekoPartyOperationResponse
					{
						Success = false,
						Error = "Target player is already in another party"
					};
				}
				_partiesByLeader.Remove(value2);
				_leaderByMember.Remove(targetProfileId);
			}
			if (_partyInvitesById.Values.FirstOrDefault(delegate(NekoPartyInviteData x)
			{
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002d: Invalid comparison between Unknown and I4
				return x.LeaderProfileId == leaderProfileId && x.TargetProfileId == targetProfileId && (int)x.Status == 0;
			}) != (NekoPartyInviteData)null)
			{
				return new NekoPartyOperationResponse
				{
					Success = true,
					Party = CloneParty(value)
				};
			}
			if (value.Members.Any((NekoPartyMember x) => x.ProfileId == targetProfileId))
			{
				return new NekoPartyOperationResponse
				{
					Success = true,
					Party = CloneParty(value)
				};
			}
			NekoPartyInviteData val = new NekoPartyInviteData
			{
				InviteId = Guid.NewGuid().ToString("N"),
				PartyId = value.PartyId,
				LeaderProfileId = leaderProfileId,
				LeaderNickname = profileResolver.GetNicknameByProfileId(leaderProfileId),
				TargetProfileId = targetProfileId,
				TargetNickname = profileResolver.GetNicknameByProfileId(targetProfileId),
				FromProfile = CreatePartyInvitePlayer(leaderProfileId, profileResolver.GetNicknameByProfileId(leaderProfileId), isLeader: true),
				Members = CreatePartyInviteMembers(value),
				ExpiresAtUnix = timeUtil.GetTimeStamp() + 120,
				Status = (ENekoInviteStatus)0
			};
			_partyInvitesById[val.InviteId] = val;
			List<NekoGroupCharacterDto> members = value.Members.Select((NekoPartyMember member) => nativeGroupNotifier.BuildCharacter(member.ProfileId, member.IsLeader, isReady: true)).ToList();
			nativeGroupNotifier.PushInviteSend(targetProfileId, leaderProfileId, val.InviteId, members);
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = CloneParty(value),
				InviteId = val.InviteId
			};
		}
	}

	public NekoPartyOperationResponse RemoveMember(MongoId actorSessionId, string targetProfileId)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Expected O, but got Unknown
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Expected O, but got Unknown
		string text = ((object)actorSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_leaderByMember.TryGetValue(text, out var value))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party not found"
				};
			}
			if (!_partiesByLeader.TryGetValue(value, out var value2))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party not found"
				};
			}
			bool flag = value == text;
			if (!flag && text != targetProfileId)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Only leader can remove other members"
				};
			}
			if (value2.Members.RemoveAll((NekoPartyMember x) => x.ProfileId == targetProfileId) == 0)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Member not found in party",
					Party = CloneParty(value2)
				};
			}
			_leaderByMember.Remove(targetProfileId);
			ExpireInvitesForTarget(targetProfileId);
			ExpirePartyInvitesForTarget(targetProfileId);
			string nicknameByProfileId = profileResolver.GetNicknameByProfileId(text);
			NekoNativeGroupProfile nativeGroupProfile = profileResolver.GetNativeGroupProfile(targetProfileId, 0);
			string targetAid = nativeGroupProfile.Aid.ToString();
			string nickname = nativeGroupProfile.Nickname;
			if (flag && targetProfileId != text)
			{
				notificationWebSocket.SendAsync<NekoPartyKickedNotification>(new MongoId(targetProfileId), new NekoPartyKickedNotification
				{
					LeaderProfileId = text,
					LeaderNickname = nicknameByProfileId
				});
				NotifyNativeMemberRemoved(value2, targetProfileId, targetAid, nickname);
			}
			else if (!flag && text == targetProfileId)
			{
				notificationWebSocket.SendAsync<NekoPartyMemberLeftNotification>(new MongoId(value), new NekoPartyMemberLeftNotification
				{
					MemberProfileId = targetProfileId,
					MemberNickname = profileResolver.GetNicknameByProfileId(targetProfileId)
				});
				NotifyNativeMemberRemoved(value2, targetProfileId, targetAid, nickname);
			}
			if (targetProfileId == value || value2.Members.Count == 0)
			{
				List<string> list = (from member in value2.Members
					where !string.IsNullOrWhiteSpace(member.ProfileId)
					select member.ProfileId).ToList();
				foreach (NekoPartyMember member in value2.Members)
				{
					_leaderByMember.Remove(member.ProfileId);
				}
				ExpirePartyInvitesForLeader(value);
				logger.Info("[NekoPartyDebug] 队伍被删除 RemoveMember leader=" + value + " actor=" + text + " target=" + targetProfileId, (Exception)null);
				_partiesByLeader.Remove(value);
				foreach (string item in list)
				{
					ExpireInvitesForTarget(item);
					ExpirePartyInvitesForTarget(item);
					notificationWebSocket.SendAsync<NekoPartyDisbandedNotification>(new MongoId(item), new NekoPartyDisbandedNotification
					{
						LeaderProfileId = text,
						LeaderNickname = nicknameByProfileId
					});
				}
				NotifyNativeGroupDisbanded(list, text);
				return new NekoPartyOperationResponse
				{
					Success = true,
					Party = null
				};
			}
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = CloneParty(value2)
			};
		}
	}

	public List<NekoRaidInviteData> CreateRaidInvitesForLeader(string leaderProfileId, string serverId, string raidCode, string location, int side, int time)
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Expected O, but got Unknown
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!TryGetPartyByProfileId(leaderProfileId, out var leaderProfileId2, out var party))
			{
				return new List<NekoRaidInviteData>();
			}
			party.Status = (ENekoPartyStatus)1;
			leaderProfileId = leaderProfileId2;
			string nicknameByProfileId = profileResolver.GetNicknameByProfileId(leaderProfileId);
			foreach (string item in _raidInvitesById.Keys.ToList())
			{
				if (_raidInvitesById.TryGetValue(item, out var value) && (int)value.Status == 0 && string.Equals(value.PartyId, party.PartyId, StringComparison.Ordinal))
				{
					value.Status = (ENekoInviteStatus)3;
					_raidInvitesById[item] = value;
				}
			}
			long expiresAtUnix = timeUtil.GetTimeStamp() + 120;
			List<NekoRaidInviteData> list = new List<NekoRaidInviteData>();
			foreach (NekoPartyMember item2 in party.Members.Where((NekoPartyMember x) => !x.IsLeader))
			{
				NekoRaidInviteData val = new NekoRaidInviteData
				{
					InviteId = Guid.NewGuid().ToString("N"),
					PartyId = party.PartyId,
					LeaderProfileId = leaderProfileId,
					LeaderNickname = nicknameByProfileId,
					TargetProfileId = item2.ProfileId,
					TargetNickname = item2.Nickname,
					ServerId = serverId,
					RaidCode = raidCode,
					Location = location,
					Side = side,
					TargetSide = GetPreferredSide(item2.ProfileId),
					Time = time,
					ExpiresAtUnix = expiresAtUnix,
					Status = (ENekoInviteStatus)0
				};
				_raidInvitesById[val.InviteId] = val;
				list.Add(val);
			}
			return list;
		}
	}

	public List<NekoRaidInviteData> CreateCrossServerRaidInvites(string partyId, string leaderProfileId, string leaderNickname, string shadowServerId, string raidCode, string location, int side, int time, IReadOnlyList<(string TargetProfileId, string TargetNickname)> localMembers)
	{
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Expected O, but got Unknown
		if (localMembers.Count == 0)
		{
			return new List<NekoRaidInviteData>();
		}
		lock (_sync)
		{
			CleanupExpiredInvites();
			long expiresAtUnix = timeUtil.GetTimeStamp() + 120;
			List<NekoRaidInviteData> list = new List<NekoRaidInviteData>();
			foreach (var localMember in localMembers)
			{
				var (targetProfileId, targetNickname) = localMember;
				if (!string.IsNullOrEmpty(targetProfileId) && !_raidInvitesById.Values.Any(delegate(NekoRaidInviteData x)
				{
					//IL_0044: Unknown result type (might be due to invalid IL or missing references)
					//IL_004a: Invalid comparison between Unknown and I4
					return x.TargetProfileId == targetProfileId && x.ServerId == shadowServerId && x.RaidCode == raidCode && (int)x.Status != 3;
				}))
				{
					NekoRaidInviteData val = new NekoRaidInviteData
					{
						InviteId = Guid.NewGuid().ToString("N"),
						PartyId = partyId,
						LeaderProfileId = leaderProfileId,
						LeaderNickname = leaderNickname,
						TargetProfileId = targetProfileId,
						TargetNickname = targetNickname,
						ServerId = shadowServerId,
						RaidCode = raidCode,
						Location = location,
						Side = side,
						TargetSide = GetPreferredSide(targetProfileId),
						Time = time,
						ExpiresAtUnix = expiresAtUnix,
						Status = (ENekoInviteStatus)0
					};
					_raidInvitesById[val.InviteId] = val;
					list.Add(val);
				}
			}
			return list;
		}
	}

	public void PublishRaidInvitesForParty(string leaderProfileId, string serverId, string location, int side, int time)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		List<NekoRaidInviteData> list = CreateRaidInvitesForLeader(leaderProfileId, serverId, string.Empty, location, side, time);
		foreach (NekoRaidInviteData item in list)
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
				});
			}
			catch (Exception ex)
			{
				logger.Error("[NekoMatchGroup] 选图邀请推送失败 target=" + item.TargetProfileId + ": " + ex.Message, (Exception)null);
			}
		}
	}

	public void ExpireRaidInvitesForServer(string serverId, string raidCode = "")
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Invalid comparison between Unknown and I4
		if (string.IsNullOrEmpty(serverId))
		{
			return;
		}
		lock (_sync)
		{
			foreach (string item in _raidInvitesById.Keys.ToList())
			{
				if (_raidInvitesById.TryGetValue(item, out var value) && value.ServerId == serverId && (string.IsNullOrEmpty(raidCode) || value.RaidCode == raidCode) && (int)value.Status != 3)
				{
					value.Status = (ENekoInviteStatus)3;
					_raidInvitesById[item] = value;
				}
			}
		}
	}

	public NekoPendingRaidInvitesResponse GetPendingRaidInvites(MongoId targetSessionId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		string targetProfileId = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			NekoPendingRaidInvitesResponse val = new NekoPendingRaidInvitesResponse();
			val.Invites = _raidInvitesById.Values.Where(delegate(NekoRaidInviteData x)
			{
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Invalid comparison between Unknown and I4
				return x.TargetProfileId == targetProfileId && (int)x.Status == 0;
			}).Select(CloneRaidInvite).ToList();
			return val;
		}
	}

	public NekoPendingPartyInvitesResponse GetPendingPartyInvites(MongoId targetSessionId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		string targetProfileId = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			NekoPendingPartyInvitesResponse val = new NekoPendingPartyInvitesResponse();
			val.Invites = _partyInvitesById.Values.Where(delegate(NekoPartyInviteData x)
			{
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Invalid comparison between Unknown and I4
				return x.TargetProfileId == targetProfileId && (int)x.Status == 0;
			}).Select(ClonePartyInvite).ToList();
			return val;
		}
	}

	public NekoPartyOperationResponse AcceptPartyInvite(MongoId targetSessionId, string inviteId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Expected O, but got Unknown
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Expected O, but got Unknown
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Expected O, but got Unknown
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Expected O, but got Unknown
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Expected O, but got Unknown
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		string targetProfileId = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_partyInvitesById.TryGetValue(inviteId, out var value))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party invite not found or expired"
				};
			}
			if (value.TargetProfileId != targetProfileId)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party invite does not belong to this player"
				};
			}
			if ((int)value.Status != 0)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = $"Party invite is already {value.Status}"
				};
			}
			if (!_partiesByLeader.TryGetValue(value.LeaderProfileId, out var value2))
			{
				value.Status = (ENekoInviteStatus)3;
				_partyInvitesById[inviteId] = value;
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Target party no longer exists"
				};
			}
			if (_leaderByMember.TryGetValue(targetProfileId, out var value3) && value3 != value.LeaderProfileId)
			{
				if (!_partiesByLeader.TryGetValue(value3, out var value4) || (int)value4.Status != 0 || value4.Members.Count != 1 || !(value4.Members[0].ProfileId == targetProfileId) || !value4.Members[0].IsLeader)
				{
					return new NekoPartyOperationResponse
					{
						Success = false,
						Error = "Target player is already in another party"
					};
				}
				_partiesByLeader.Remove(value3);
				_leaderByMember.Remove(targetProfileId);
				ExpirePartyInvitesForLeader(value3);
			}
			if (!value2.Members.Any((NekoPartyMember x) => x.ProfileId == targetProfileId))
			{
				value2.Members.Add(new NekoPartyMember
				{
					ProfileId = targetProfileId,
					Nickname = value.TargetNickname,
					IsLeader = false,
					PreferredSide = GetPreferredSide(targetProfileId)
				});
				_leaderByMember[targetProfileId] = value.LeaderProfileId;
			}
			value.Status = (ENekoInviteStatus)1;
			_partyInvitesById[inviteId] = value;
			ExpireOtherPartyInvitesForTarget(targetProfileId, inviteId);
			logger.Info("[NekoPartyDebug] accept 存储队伍 instance=" + _instanceId + " leader=" + value.LeaderProfileId + " members=" + value2.Members.Count + " 表count=" + _partiesByLeader.Count, (Exception)null);
			NotificationWebSocket val = notificationWebSocket;
			MongoId val2 = default(MongoId);
			((MongoId)(ref val2))._002Ector(value.LeaderProfileId);
			PushNotification val3 = new PushNotification();
			val3.NotificationIcon = (EEFTNotificationIconType)2;
			val3.Notification = GetLocalizedText("NKP_PartyInviteAcceptedNotification", value.TargetNickname);
			val.SendAsync<PushNotification>(val2, val3);
			SyncNativeParty(value2);
			if (!string.IsNullOrWhiteSpace(value2.Location) && targetProfileId != value.LeaderProfileId)
			{
				PushLeaderRaidReady(value.LeaderProfileId, new List<string> { targetProfileId });
				logger.Info("[NekoPartyDebug] 队员加入时补推队长准备 leader=" + value.LeaderProfileId + " target=" + targetProfileId + " loc=" + value2.Location, (Exception)null);
			}
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = CloneParty(value2)
			};
		}
	}

	private void SyncNativeParty(NekoPartyData party)
	{
		List<(string ProfileId, bool IsLeader, int PreferredSide)> memberRefs = party.Members.Select((NekoPartyMember member) => (ProfileId: member.ProfileId, IsLeader: member.IsLeader, PreferredSide: member.PreferredSide)).ToList();
		if (memberRefs.Count == 0)
		{
			return;
		}
		bool leaderReady = !string.IsNullOrWhiteSpace(party.Location);
		Task.Run(async delegate
		{
			try
			{
				await Task.Delay(800);
				List<NekoGroupCharacterDto> members = memberRefs.Select(((string ProfileId, bool IsLeader, int PreferredSide) reference) => nativeGroupNotifier.BuildCharacter(reference.ProfileId, reference.IsLeader, !reference.IsLeader | leaderReady, reference.PreferredSide)).ToList();
				nativeGroupNotifier.SyncParty(members);
			}
			catch (Exception ex)
			{
				logger.Error("[NekoNativeGroup] SyncNativeParty failed: " + ex.Message, (Exception)null);
			}
		});
	}

	private void NotifyNativeMemberRemoved(NekoPartyData party, string targetProfileId, string targetAid, string? targetNickname)
	{
		List<string> remainingIds = (from member in party.Members
			where !string.IsNullOrWhiteSpace(member.ProfileId) && !string.Equals(member.ProfileId, targetProfileId, StringComparison.Ordinal)
			select member.ProfileId).ToList();
		Task.Run(delegate
		{
			try
			{
				nativeGroupNotifier.PushGroupRemoved(targetProfileId);
				foreach (string item in remainingIds)
				{
					nativeGroupNotifier.PushUserLeave(item, targetAid, targetNickname);
				}
			}
			catch (Exception ex)
			{
				logger.Error("[NekoNativeGroup] NotifyNativeMemberRemoved failed: " + ex.Message, (Exception)null);
			}
		});
	}

	private void NotifyNativeGroupDisbanded(IReadOnlyList<string> remainingMemberIds, string actorProfileId)
	{
		List<string> targets = remainingMemberIds.Where((string id) => !string.IsNullOrWhiteSpace(id)).Append(actorProfileId).Distinct<string>(StringComparer.Ordinal)
			.ToList();
		Task.Run(delegate
		{
			try
			{
				foreach (string item in targets)
				{
					nativeGroupNotifier.PushGroupRemoved(item);
				}
			}
			catch (Exception ex)
			{
				logger.Error("[NekoNativeGroup] NotifyNativeGroupDisbanded failed: " + ex.Message, (Exception)null);
			}
		});
	}

	public NekoPartyOperationResponse RejectPartyInvite(MongoId targetSessionId, string inviteId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		string text = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_partyInvitesById.TryGetValue(inviteId, out var value))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party invite not found or expired"
				};
			}
			if (value.TargetProfileId != text)
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party invite does not belong to this player"
				};
			}
			value.Status = (ENekoInviteStatus)2;
			_partyInvitesById[inviteId] = value;
			NotificationWebSocket val = notificationWebSocket;
			MongoId val2 = default(MongoId);
			((MongoId)(ref val2))._002Ector(value.LeaderProfileId);
			PushNotification val3 = new PushNotification();
			val3.NotificationIcon = (EEFTNotificationIconType)2;
			val3.Notification = GetLocalizedText("NKP_PartyInviteRejectedNotification", value.TargetNickname);
			val.SendAsync<PushNotification>(val2, val3);
			return new NekoPartyOperationResponse
			{
				Success = true,
				Party = GetPartyByPlayer(targetSessionId)
			};
		}
	}

	public NekoRaidInviteDecisionResponse AcceptRaidInvite(MongoId targetSessionId, string inviteId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return AcceptRaidInvite(targetSessionId, inviteId, GetPreferredSide(((object)targetSessionId).ToString()));
	}

	public NekoRaidInviteDecisionResponse AcceptRaidInvite(MongoId targetSessionId, string inviteId, int preferredSide)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Expected O, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		string text = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			_preferredSideByProfileId[text] = NormalizePreferredSide(preferredSide);
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_raidInvitesById.TryGetValue(inviteId, out var value))
			{
				return new NekoRaidInviteDecisionResponse
				{
					Success = false,
					Error = "Invite not found or expired"
				};
			}
			if (value.TargetProfileId != text)
			{
				return new NekoRaidInviteDecisionResponse
				{
					Success = false,
					Error = "Invite does not belong to this player"
				};
			}
			if ((int)value.Status != 0)
			{
				return new NekoRaidInviteDecisionResponse
				{
					Success = false,
					Error = $"Invite is already {value.Status}"
				};
			}
			value.Status = (ENekoInviteStatus)1;
			_raidInvitesById[inviteId] = value;
			logger.Info($"NekoParty raid invite accepted: {value.InviteId} target={text} server={value.ServerId}", (Exception)null);
			return new NekoRaidInviteDecisionResponse
			{
				Success = true,
				ServerId = value.ServerId
			};
		}
	}

	public NekoRaidInviteDecisionResponse RejectRaidInvite(MongoId targetSessionId, string inviteId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		string text = ((object)targetSessionId).ToString();
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!_raidInvitesById.TryGetValue(inviteId, out var value))
			{
				return new NekoRaidInviteDecisionResponse
				{
					Success = false,
					Error = "Invite not found or expired"
				};
			}
			if (value.TargetProfileId != text)
			{
				return new NekoRaidInviteDecisionResponse
				{
					Success = false,
					Error = "Invite does not belong to this player"
				};
			}
			value.Status = (ENekoInviteStatus)2;
			_raidInvitesById[inviteId] = value;
			return new NekoRaidInviteDecisionResponse
			{
				Success = true,
				ServerId = value.ServerId
			};
		}
	}

	public NekoPartyOperationResponse SyncPreferredSide(MongoId sessionId, int preferredSide)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		string text = ((object)sessionId).ToString();
		int num = NormalizePreferredSide(preferredSide);
		string changedMemberProfileId = null;
		bool flag = false;
		bool flag2 = false;
		List<string> list = new List<string>();
		NekoPartyOperationResponse result;
		lock (_sync)
		{
			int preferredSide2 = GetPreferredSide(text);
			foreach (string relatedProfileId in profileResolver.GetRelatedProfileIds(text))
			{
				_preferredSideByProfileId[relatedProfileId] = num;
			}
			if (TryGetPartyByProfileId(text, out var _, out var party))
			{
				foreach (NekoPartyMember member in party.Members)
				{
					if (profileResolver.GetRelatedProfileIds(member.ProfileId).Contains<string>(text, StringComparer.Ordinal))
					{
						member.PreferredSide = num;
						changedMemberProfileId = member.ProfileId;
						flag = member.IsLeader;
					}
				}
				if (preferredSide2 != num && changedMemberProfileId != null)
				{
					flag2 = !string.IsNullOrWhiteSpace(party.Location);
					list = (from m in party.Members
						where !string.IsNullOrEmpty(m.ProfileId) && !string.Equals(m.ProfileId, changedMemberProfileId, StringComparison.Ordinal)
						select m.ProfileId).ToList();
				}
			}
			result = new NekoPartyOperationResponse
			{
				Success = true,
				Party = GetPartyByPlayer(sessionId)
			};
		}
		if (changedMemberProfileId != null && list.Count > 0)
		{
			PushMemberVisualRefresh(changedMemberProfileId, flag, num, !flag | flag2, list);
		}
		return result;
	}

	private void PushMemberVisualRefresh(string memberProfileId, bool isLeader, int preferredSide, bool isReady, List<string> recipientProfileIds)
	{
		Task.Run(delegate
		{
			try
			{
				NekoGroupCharacterDto newMember = nativeGroupNotifier.BuildCharacter(memberProfileId, isLeader, isReady, preferredSide);
				foreach (string recipientProfileId in recipientProfileIds)
				{
					if (!string.Equals(recipientProfileId, memberProfileId, StringComparison.Ordinal))
					{
						nativeGroupNotifier.PushInviteAccept(recipientProfileId, newMember);
					}
				}
				logger.Success($"[NekoNativeGroup] PushMemberVisualRefresh member={memberProfileId} side={preferredSide} recipients={recipientProfileIds.Count}", (Exception)null);
			}
			catch (Exception ex)
			{
				logger.Error("[NekoNativeGroup] PushMemberVisualRefresh failed: " + ex.Message, (Exception)null);
			}
		});
	}

	public NekoPartyOperationResponse SetPartyRaidSettings(MongoId sessionId, string? location, int side, int time)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected O, but got Unknown
		string text = ((object)sessionId).ToString();
		string text2 = null;
		List<string> list = new List<string>();
		NekoPartyOperationResponse result;
		lock (_sync)
		{
			CleanupExpiredInvites();
			CleanupExpiredPartyInvites();
			if (!TryGetPartyByProfileId(text, out var leaderProfileId, out var party))
			{
				return new NekoPartyOperationResponse
				{
					Success = false,
					Error = "Party not found"
				};
			}
			if (profileResolver.GetRelatedProfileIds(text).Contains<string>(leaderProfileId, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(location))
			{
				party.Location = location;
				party.Side = side;
				party.Time = time;
				text2 = leaderProfileId;
				list = (from member in party.Members
					where !member.IsLeader && !string.IsNullOrEmpty(member.ProfileId)
					select member.ProfileId).ToList();
			}
			result = new NekoPartyOperationResponse
			{
				Success = true,
				Party = CloneParty(party)
			};
		}
		if (text2 != null && list.Count > 0)
		{
			PushLeaderRaidReady(text2, list);
		}
		if (NekoGroupExServerConfig.TwoStageRaidInvites && text2 != null && !string.IsNullOrWhiteSpace(location))
		{
			try
			{
				PublishRaidInvitesForParty(text2, text2, location, side, time);
			}
			catch (Exception ex)
			{
				logger.Error("[NekoMatchGroup] 选图邀请发布异常: " + ex.Message, (Exception)null);
			}
		}
		return result;
	}

	private void PushLeaderRaidReady(string leaderProfileId, List<string> recipientProfileIds)
	{
		Task.Run(delegate
		{
			try
			{
				NekoGroupCharacterDto readyMember = nativeGroupNotifier.BuildCharacter(leaderProfileId, isLeader: true, isReady: true, GetPreferredSide(leaderProfileId));
				foreach (string recipientProfileId in recipientProfileIds)
				{
					if (!string.Equals(recipientProfileId, leaderProfileId, StringComparison.Ordinal))
					{
						nativeGroupNotifier.PushRaidReady(recipientProfileId, readyMember);
					}
				}
			}
			catch (Exception ex)
			{
				logger.Error("[NekoNativeGroup] PushLeaderRaidReady failed: " + ex.Message, (Exception)null);
			}
		});
	}

	public void MarkPartyOpen(string leaderProfileId)
	{
		lock (_sync)
		{
			if (_partiesByLeader.TryGetValue(leaderProfileId, out var value))
			{
				value.Status = (ENekoPartyStatus)0;
			}
		}
	}

	private void CleanupExpiredInvites()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		long timeStamp = timeUtil.GetTimeStamp();
		foreach (string item in _raidInvitesById.Keys.ToList())
		{
			if (_raidInvitesById.TryGetValue(item, out var value) && (int)value.Status == 0 && value.ExpiresAtUnix <= timeStamp)
			{
				value.Status = (ENekoInviteStatus)3;
				_raidInvitesById[item] = value;
			}
		}
	}

	private void CleanupExpiredPartyInvites()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		long timeStamp = timeUtil.GetTimeStamp();
		foreach (string item in _partyInvitesById.Keys.ToList())
		{
			if (_partyInvitesById.TryGetValue(item, out var value) && (int)value.Status == 0 && value.ExpiresAtUnix <= timeStamp)
			{
				value.Status = (ENekoInviteStatus)3;
				_partyInvitesById[item] = value;
			}
		}
	}

	private void ExpireInvitesForTarget(string targetProfileId)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		foreach (string item in _raidInvitesById.Keys.ToList())
		{
			if (_raidInvitesById.TryGetValue(item, out var value) && value.TargetProfileId == targetProfileId && (int)value.Status == 0)
			{
				value.Status = (ENekoInviteStatus)3;
				_raidInvitesById[item] = value;
			}
		}
	}

	private void ExpirePartyInvitesForTarget(string targetProfileId)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		foreach (string item in _partyInvitesById.Keys.ToList())
		{
			if (_partyInvitesById.TryGetValue(item, out var value) && value.TargetProfileId == targetProfileId && (int)value.Status == 0)
			{
				value.Status = (ENekoInviteStatus)3;
				_partyInvitesById[item] = value;
			}
		}
	}

	private void ExpirePartyInvitesForLeader(string leaderProfileId)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		foreach (string item in _partyInvitesById.Keys.ToList())
		{
			if (_partyInvitesById.TryGetValue(item, out var value) && value.LeaderProfileId == leaderProfileId && (int)value.Status == 0)
			{
				value.Status = (ENekoInviteStatus)3;
				_partyInvitesById[item] = value;
			}
		}
	}

	private void ExpireOtherPartyInvitesForTarget(string targetProfileId, string acceptedInviteId)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		foreach (string item in _partyInvitesById.Keys.ToList())
		{
			if (!(item == acceptedInviteId) && _partyInvitesById.TryGetValue(item, out var value) && value.TargetProfileId == targetProfileId && (int)value.Status == 0)
			{
				value.Status = (ENekoInviteStatus)3;
				_partyInvitesById[item] = value;
			}
		}
	}

	private static NekoPartyData CloneParty(NekoPartyData party)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		return new NekoPartyData
		{
			PartyId = party.PartyId,
			LeaderProfileId = party.LeaderProfileId,
			Status = party.Status,
			Location = party.Location,
			Side = party.Side,
			Time = party.Time,
			Members = ((IEnumerable<NekoPartyMember>)party.Members).Select((Func<NekoPartyMember, NekoPartyMember>)delegate(NekoPartyMember x)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0029: Unknown result type (might be due to invalid IL or missing references)
				//IL_0036: Expected O, but got Unknown
				return new NekoPartyMember
				{
					ProfileId = x.ProfileId,
					Nickname = x.Nickname,
					IsLeader = x.IsLeader,
					PreferredSide = x.PreferredSide
				};
			}).ToList()
		};
	}

	private static NekoRaidInviteData CloneRaidInvite(NekoRaidInviteData invite)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		return new NekoRaidInviteData
		{
			InviteId = invite.InviteId,
			PartyId = invite.PartyId,
			LeaderProfileId = invite.LeaderProfileId,
			LeaderNickname = invite.LeaderNickname,
			TargetProfileId = invite.TargetProfileId,
			TargetNickname = invite.TargetNickname,
			ServerId = invite.ServerId,
			RaidCode = invite.RaidCode,
			Location = invite.Location,
			Side = invite.Side,
			TargetSide = invite.TargetSide,
			Time = invite.Time,
			ExpiresAtUnix = invite.ExpiresAtUnix,
			Status = invite.Status
		};
	}

	private static NekoPartyInviteData ClonePartyInvite(NekoPartyInviteData invite)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		return new NekoPartyInviteData
		{
			InviteId = invite.InviteId,
			PartyId = invite.PartyId,
			LeaderProfileId = invite.LeaderProfileId,
			LeaderNickname = invite.LeaderNickname,
			TargetProfileId = invite.TargetProfileId,
			TargetNickname = invite.TargetNickname,
			FromProfile = ClonePartyInvitePlayer(invite.FromProfile),
			Members = ClonePartyInvitePlayers(invite.Members),
			ExpiresAtUnix = invite.ExpiresAtUnix,
			Status = invite.Status
		};
	}

	private List<NekoPartyInvitePlayerData> CreatePartyInviteMembers(NekoPartyData party)
	{
		if (((party != (NekoPartyData)null) ? party.Members : null) == null || party.Members.Count == 0)
		{
			return new List<NekoPartyInvitePlayerData>();
		}
		return (from member in party.Members
			where !string.IsNullOrWhiteSpace(member.ProfileId)
			select CreatePartyInvitePlayer(member.ProfileId, member.Nickname, member.IsLeader)).ToList();
	}

	private NekoPartyInvitePlayerData CreatePartyInvitePlayer(string profileId, string nickname, bool isLeader)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		return new NekoPartyInvitePlayerData
		{
			AccountId = profileId,
			ProfileId = profileId,
			Nickname = (string.IsNullOrWhiteSpace(nickname) ? profileId : nickname),
			IsLeader = isLeader,
			IsReady = true,
			Side = GetPreferredSide(profileId),
			Level = 1,
			PrestigeLevel = 0,
			MemberCategory = 0,
			SelectedMemberCategory = 0
		};
	}

	private static NekoPartyInvitePlayerData? ClonePartyInvitePlayer(NekoPartyInvitePlayerData? player)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		if (player == (NekoPartyInvitePlayerData)null)
		{
			return null;
		}
		return new NekoPartyInvitePlayerData
		{
			AccountId = player.AccountId,
			ProfileId = player.ProfileId,
			Nickname = player.Nickname,
			IsLeader = player.IsLeader,
			IsReady = player.IsReady,
			Side = player.Side,
			Level = player.Level,
			PrestigeLevel = player.PrestigeLevel,
			MemberCategory = player.MemberCategory,
			SelectedMemberCategory = player.SelectedMemberCategory
		};
	}

	private static List<NekoPartyInvitePlayerData> ClonePartyInvitePlayers(IEnumerable<NekoPartyInvitePlayerData> players)
	{
		if (players == null)
		{
			return new List<NekoPartyInvitePlayerData>();
		}
		return (from player in players.Select(ClonePartyInvitePlayer)
			where player != (NekoPartyInvitePlayerData)null
			select (player)).ToList();
	}

	private int GetPreferredSide(string profileId)
	{
		foreach (string relatedProfileId in profileResolver.GetRelatedProfileIds(profileId))
		{
			if (_preferredSideByProfileId.TryGetValue(relatedProfileId, out var value))
			{
				return NormalizePreferredSide(value);
			}
		}
		return 0;
	}

	private static int NormalizePreferredSide(int preferredSide)
	{
		return (preferredSide == 1) ? 1 : 0;
	}

	private bool TryGetPartyByProfileId(string profileId, out string leaderProfileId, out NekoPartyData party)
	{
		foreach (string relatedProfileId in profileResolver.GetRelatedProfileIds(profileId))
		{
			if (_leaderByMember.TryGetValue(relatedProfileId, out leaderProfileId) && _partiesByLeader.TryGetValue(leaderProfileId, out party))
			{
				return true;
			}
			if (_partiesByLeader.TryGetValue(relatedProfileId, out party))
			{
				leaderProfileId = relatedProfileId;
				return true;
			}
		}
		leaderProfileId = string.Empty;
		party = null;
		return false;
	}

	private string GetLocalizedText(string key, params object[] args)
	{
		string text = ((localeService.GetLocaleDb((string)null).TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) ? value : key);
		if (args != null && args.Length != 0)
		{
			return string.Format(text, args);
		}
		return text;
	}
}
