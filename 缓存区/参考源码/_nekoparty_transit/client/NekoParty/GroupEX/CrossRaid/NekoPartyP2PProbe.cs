using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Layers;
using Fika.Core.Networking.LiteNetLib.Utils;
using LiteNetLib;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.State;
using UnityEngine;

namespace NekoParty.GroupEX.CrossRaid;

internal static class NekoPartyP2PProbe
{
	private sealed class PeerEntry
	{
		public IPEndPoint EndPoint;

		public float LastSeenAt;

		public int ReportedRttMs;

		public NekoNatType NatType;
	}

	private sealed class ProbeListener : INetEventListener, INatPunchListener
	{
		public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
		{
			string text = null;
			if (!((NetDataReader)reader).TryGetString(ref text))
			{
				return;
			}
			if (!(text == "neko.p2p.ping"))
			{
				if (text == "neko.p2p.pong")
				{
					HandlePong(remoteEndPoint, reader);
				}
			}
			else
			{
				HandlePing(remoteEndPoint, reader);
			}
		}

		public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			if (_isLeader || _leaderEndPoint != null)
			{
				return;
			}
			bool flag = false;
			foreach (IPEndPoint leaderCandidate in _leaderCandidates)
			{
				if (leaderCandidate.Address.Equals(targetEndPoint.Address) && leaderCandidate.Port == targetEndPoint.Port)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				_leaderCandidates.Add(targetEndPoint);
				FikaGlobals.LogInfo($"[NekoP2P] 信标介绍队长候选端点 {targetEndPoint} ({type})", "OnNatIntroductionSuccess");
			}
		}

		public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
		{
		}

		public void OnConnectionRequest(ConnectionRequest request)
		{
			((LiteConnectionRequest)request).Reject();
		}

		public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
		{
		}

		public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
		{
		}

		public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
		{
		}

		public void OnPeerConnected(NetPeer peer)
		{
		}

		public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
		{
		}
	}

	private const float IntroduceIntervalSeconds = 3f;

	private const float IntroduceKeepaliveSeconds = 10f;

	private const float LeaderHeartbeatSeconds = 5f;

	private const float PingIntervalSeconds = 2f;

	private const float PunchFailSeconds = 30f;

	private const float PeerStaleSeconds = 10f;

	private const float UiRebuildSeconds = 1f;

	private const string PingHeader = "neko.p2p.ping";

	private const string PongHeader = "neko.p2p.pong";

	private static NetManager _net;

	private static ProbeListener _listener;

	private static NetDataWriter _writer;

	private static bool _isLeader;

	private static string _fingerprint;

	private static string _selfProfileId;

	private static Guid _punchGuid;

	private static IPEndPoint _beaconEndPoint;

	private static float _startedAt;

	private static float _nextIntroduce;

	private static float _nextPing;

	private static float _nextUiRebuild;

	private static float _nextStartAttempt;

	private static int _consecutiveErrors;

	private static bool _disabledForSession;

	private static readonly List<IPEndPoint> _leaderCandidates = new List<IPEndPoint>();

	private static IPEndPoint _leaderEndPoint;

	private static float _lastPongAt = -1f;

	private static int _selfRttMs = -1;

	private static readonly Dictionary<string, PeerEntry> _peers = new Dictionary<string, PeerEntry>(StringComparer.Ordinal);

	private static readonly Dictionary<string, (NekoP2PState State, int RttMs, NekoNatType NatType)> _teamFromLeader = new Dictionary<string, (NekoP2PState, int, NekoNatType)>(StringComparer.Ordinal);

	private static List<NekoP2PMemberStatus> _uiRows;

	internal static bool IsRunning => _net != null;

	internal static bool SelfDirectFailed
	{
		get
		{
			if (_net == null || _isLeader)
			{
				return false;
			}
			return SelfState() == NekoP2PState.Failed;
		}
	}

	internal static IReadOnlyList<NekoP2PMemberStatus> GetSnapshot()
	{
		if (_net != null)
		{
			return _uiRows;
		}
		return null;
	}

	internal static void Tick()
	{
		if (_disabledForSession)
		{
			return;
		}
		try
		{
			TickCore();
			_consecutiveErrors = 0;
		}
		catch (Exception arg)
		{
			if (++_consecutiveErrors >= 5)
			{
				_disabledForSession = true;
				FikaGlobals.LogError($"[NekoP2P] 预打洞探测器连续异常，本次会话停用: {arg}", "Tick");
				Stop();
			}
		}
	}

	private static void TickCore()
	{
		if (!ShouldRun(out var party))
		{
			Stop();
			return;
		}
		string text = BuildFingerprint(party);
		if (_net != null && !string.Equals(text, _fingerprint, StringComparison.Ordinal))
		{
			Stop();
		}
		if (_net == null)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup < _nextStartAttempt)
			{
				return;
			}
			_nextStartAttempt = realtimeSinceStartup + 10f;
			if (!TryStart(party, text))
			{
				return;
			}
		}
		((LiteNetManager)_net).PollEvents();
		NatPunchModule natPunchModule = ((LiteNetManager)_net).NatPunchModule;
		if (natPunchModule != null)
		{
			natPunchModule.PollEvents();
		}
		float realtimeSinceStartup2 = Time.realtimeSinceStartup;
		if (realtimeSinceStartup2 >= _nextIntroduce)
		{
			if (_isLeader)
			{
				NatPunchModule natPunchModule2 = ((LiteNetManager)_net).NatPunchModule;
				if (natPunchModule2 != null)
				{
					natPunchModule2.SendNatIntroduceRequest(_beaconEndPoint, $"Server:{_punchGuid}");
				}
				_nextIntroduce = realtimeSinceStartup2 + 5f;
			}
			else
			{
				NatPunchModule natPunchModule3 = ((LiteNetManager)_net).NatPunchModule;
				if (natPunchModule3 != null)
				{
					natPunchModule3.SendNatIntroduceRequest(_beaconEndPoint, $"Client:{_punchGuid}");
				}
				_nextIntroduce = realtimeSinceStartup2 + ((_leaderCandidates.Count == 0 && _leaderEndPoint == null) ? 3f : 10f);
			}
		}
		if (!_isLeader && realtimeSinceStartup2 >= _nextPing)
		{
			SendPings(realtimeSinceStartup2);
			_nextPing = realtimeSinceStartup2 + 2f;
		}
		if (realtimeSinceStartup2 >= _nextUiRebuild)
		{
			RebuildUiRows(party, realtimeSinceStartup2);
			_nextUiRebuild = realtimeSinceStartup2 + 1f;
		}
	}

	internal static void Stop()
	{
		if (_net != null)
		{
			try
			{
				((LiteNetManager)_net).Stop();
			}
			catch
			{
			}
			_net = null;
			_listener = null;
			_writer = null;
			_fingerprint = null;
			_leaderCandidates.Clear();
			_leaderEndPoint = null;
			_lastPongAt = -1f;
			_selfRttMs = -1;
			_peers.Clear();
			_teamFromLeader.Clear();
			_uiRows = null;
			FikaGlobals.LogInfo("[NekoP2P] 预打洞探测器已停止。", "Stop");
		}
	}

	private static bool ShouldRun(out NekoPartyData party)
	{
		party = default(NekoPartyData);
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return false;
		}
		try
		{
			if (FikaGlobals.IsInRaid)
			{
				return false;
			}
		}
		catch
		{
			return false;
		}
		NekoPartyData? currentParty = NekoGroupState.CurrentParty;
		if (!currentParty.HasValue || string.IsNullOrEmpty(currentParty.Value.PartyId) || currentParty.Value.Members == null || currentParty.Value.Members.Length < 2)
		{
			return false;
		}
		party = currentParty.Value;
		return true;
	}

	private static string BuildFingerprint(NekoPartyData party)
	{
		List<string> list = new List<string>(party.Members.Length);
		NekoPartyMemberData[] members = party.Members;
		for (int i = 0; i < members.Length; i++)
		{
			NekoPartyMemberData nekoPartyMemberData = members[i];
			if (!string.IsNullOrEmpty(nekoPartyMemberData.ProfileId))
			{
				list.Add(nekoPartyMemberData.ProfileId);
			}
		}
		list.Sort(StringComparer.Ordinal);
		return party.PartyId + "|" + party.LeaderProfileId + "|" + string.Join(",", list);
	}

	private static bool TryStart(NekoPartyData party, string fingerprint)
	{
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Expected O, but got Unknown
		if (!NekoCrossTransitRuntime.TryGetPunchEndpoint(out var host, out var port))
		{
			return false;
		}
		IPEndPoint iPEndPoint;
		try
		{
			iPEndPoint = new IPEndPoint(NetUtils.ResolveAddress(host, AddressFamily.InterNetwork), port);
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning($"[NekoP2P] 打洞信标地址解析失败 {host}:{port}: {ex.Message}", "TryStart");
			return false;
		}
		HashSet<string> localProfileIds = NekoPartyMixedSideRuntime.GetLocalProfileIds();
		_isLeader = localProfileIds.Contains(party.LeaderProfileId);
		_selfProfileId = party.LeaderProfileId;
		NekoPartyMemberData[] members = party.Members;
		for (int i = 0; i < members.Length; i++)
		{
			NekoPartyMemberData nekoPartyMemberData = members[i];
			if (!string.IsNullOrEmpty(nekoPartyMemberData.ProfileId) && localProfileIds.Contains(nekoPartyMemberData.ProfileId))
			{
				_selfProfileId = nekoPartyMemberData.ProfileId;
				break;
			}
		}
		_listener = new ProbeListener();
		_net = new NetManager((INetEventListener)_listener, (PacketLayerBase)null)
		{
			UnconnectedMessagesEnabled = true,
			NatPunchEnabled = true,
			IPv6Enabled = false
		};
		try
		{
			((LiteNetManager)_net).Start();
			((LiteNetManager)_net).NatPunchModule.Init((INatPunchListener)_listener);
		}
		catch (Exception ex2)
		{
			FikaGlobals.LogWarning("[NekoP2P] 预打洞 socket 启动失败: " + ex2.Message, "TryStart");
			_net = null;
			_listener = null;
			return false;
		}
		NekoNatTypeDetector.EnsureStarted();
		_writer = new NetDataWriter();
		_fingerprint = fingerprint;
		_punchGuid = DerivePunchGuid(party.PartyId);
		_beaconEndPoint = iPEndPoint;
		float num = (_startedAt = Time.realtimeSinceStartup);
		_nextIntroduce = 0f;
		_nextPing = num + 0.5f;
		_nextUiRebuild = 0f;
		FikaGlobals.LogInfo(string.Format("[NekoP2P] 预打洞探测器启动 role={0} token={1} beacon={2}", _isLeader ? "leader" : "member", _punchGuid, iPEndPoint), "TryStart");
		return true;
	}

	private static Guid DerivePunchGuid(string partyId)
	{
		using MD5 mD = MD5.Create();
		return new Guid(mD.ComputeHash(Encoding.UTF8.GetBytes("nekop2p:" + partyId)));
	}

	private static NekoP2PState SelfState()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_lastPongAt >= 0f)
		{
			if (!(realtimeSinceStartup - _lastPongAt <= 10f))
			{
				return NekoP2PState.Punching;
			}
			return NekoP2PState.Direct;
		}
		if (!(realtimeSinceStartup - _startedAt >= 30f))
		{
			return NekoP2PState.Punching;
		}
		return NekoP2PState.Failed;
	}

	private static void SendPings(float now)
	{
		_writer.Reset();
		_writer.Put("neko.p2p.ping", 0);
		_writer.Put(_selfProfileId ?? string.Empty, 0);
		_writer.Put((long)(now * 1000f));
		_writer.Put(_selfRttMs);
		_writer.Put((byte)NekoNatTypeDetector.CurrentType);
		if (_leaderEndPoint != null)
		{
			((LiteNetManager)_net).SendUnconnectedMessage(_writer, _leaderEndPoint);
			return;
		}
		foreach (IPEndPoint leaderCandidate in _leaderCandidates)
		{
			((LiteNetManager)_net).SendUnconnectedMessage(_writer, leaderCandidate);
		}
	}

	private static void RebuildUiRows(NekoPartyData party, float now)
	{
		List<NekoP2PMemberStatus> list = new List<NekoP2PMemberStatus>(party.Members.Length);
		NekoPartyMemberData[] members = party.Members;
		for (int i = 0; i < members.Length; i++)
		{
			NekoPartyMemberData nekoPartyMemberData = members[i];
			if (string.IsNullOrEmpty(nekoPartyMemberData.ProfileId))
			{
				continue;
			}
			NekoP2PMemberStatus nekoP2PMemberStatus = new NekoP2PMemberStatus
			{
				ProfileId = nekoPartyMemberData.ProfileId,
				Nickname = (string.IsNullOrEmpty(nekoPartyMemberData.Nickname) ? nekoPartyMemberData.ProfileId : nekoPartyMemberData.Nickname),
				IsLeader = (nekoPartyMemberData.IsLeader || string.Equals(nekoPartyMemberData.ProfileId, party.LeaderProfileId, StringComparison.Ordinal)),
				IsSelf = string.Equals(nekoPartyMemberData.ProfileId, _selfProfileId, StringComparison.Ordinal)
			};
			(NekoP2PState, int, NekoNatType) value3;
			if (nekoP2PMemberStatus.IsSelf)
			{
				nekoP2PMemberStatus.State = NekoP2PState.Direct;
				nekoP2PMemberStatus.RttMs = 0;
				nekoP2PMemberStatus.NatType = NekoNatTypeDetector.CurrentType;
			}
			else if (_isLeader)
			{
				if (_peers.TryGetValue(nekoPartyMemberData.ProfileId, out var value))
				{
					bool flag = now - value.LastSeenAt <= 10f;
					nekoP2PMemberStatus.State = ((!flag) ? NekoP2PState.Punching : NekoP2PState.Direct);
					nekoP2PMemberStatus.RttMs = value.ReportedRttMs;
					nekoP2PMemberStatus.NatType = value.NatType;
				}
				else
				{
					nekoP2PMemberStatus.State = ((!(now - _startedAt >= 30f)) ? NekoP2PState.Punching : NekoP2PState.Waiting);
					nekoP2PMemberStatus.RttMs = -1;
				}
			}
			else if (nekoP2PMemberStatus.IsLeader)
			{
				nekoP2PMemberStatus.State = SelfState();
				nekoP2PMemberStatus.RttMs = _selfRttMs;
				if (_teamFromLeader.TryGetValue(nekoPartyMemberData.ProfileId, out (NekoP2PState, int, NekoNatType) value2))
				{
					nekoP2PMemberStatus.NatType = value2.Item3;
				}
			}
			else if (_teamFromLeader.TryGetValue(nekoPartyMemberData.ProfileId, out value3))
			{
				(nekoP2PMemberStatus.State, nekoP2PMemberStatus.RttMs, nekoP2PMemberStatus.NatType) = value3;
			}
			else
			{
				nekoP2PMemberStatus.State = NekoP2PState.Waiting;
				nekoP2PMemberStatus.RttMs = -1;
			}
			list.Add(nekoP2PMemberStatus);
		}
		list.Sort(delegate(NekoP2PMemberStatus a, NekoP2PMemberStatus b)
		{
			if (a.IsLeader == b.IsLeader)
			{
				return string.Compare(a.Nickname, b.Nickname, StringComparison.OrdinalIgnoreCase);
			}
			return (!a.IsLeader) ? 1 : (-1);
		});
		_uiRows = list;
	}

	private static void HandlePing(IPEndPoint sender, NetPacketReader reader)
	{
		string text = null;
		long num = 0L;
		int reportedRttMs = 0;
		if (!_isLeader || !((NetDataReader)reader).TryGetString(ref text) || !((NetDataReader)reader).TryGetLong(ref num) || !((NetDataReader)reader).TryGetInt(ref reportedRttMs))
		{
			return;
		}
		byte natType = 0;
		if (!((NetDataReader)reader).TryGetByte(ref natType))
		{
			natType = 0;
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!_peers.TryGetValue(text, out var value))
		{
			value = new PeerEntry();
			_peers[text] = value;
			FikaGlobals.LogInfo($"[NekoP2P] 队员打洞包已到达 profileId={text} ep={sender}", "HandlePing");
		}
		value.EndPoint = sender;
		value.LastSeenAt = realtimeSinceStartup;
		value.ReportedRttMs = reportedRttMs;
		value.NatType = (NekoNatType)natType;
		_writer.Reset();
		_writer.Put("neko.p2p.pong", 0);
		_writer.Put(num);
		NekoPartyMemberData[] array = NekoGroupState.CurrentParty?.Members ?? Array.Empty<NekoPartyMemberData>();
		int num2 = 0;
		NekoPartyMemberData[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if (!string.IsNullOrEmpty(array2[i].ProfileId))
			{
				num2++;
			}
		}
		_writer.Put(num2);
		array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			NekoPartyMemberData nekoPartyMemberData = array2[j];
			if (!string.IsNullOrEmpty(nekoPartyMemberData.ProfileId))
			{
				int num3 = -1;
				NekoNatType nekoNatType = NekoNatType.Unknown;
				NekoP2PState nekoP2PState;
				PeerEntry value2;
				if (string.Equals(nekoPartyMemberData.ProfileId, _selfProfileId, StringComparison.Ordinal))
				{
					nekoP2PState = NekoP2PState.Direct;
					num3 = 0;
					nekoNatType = NekoNatTypeDetector.CurrentType;
				}
				else if (_peers.TryGetValue(nekoPartyMemberData.ProfileId, out value2))
				{
					nekoP2PState = ((!(realtimeSinceStartup - value2.LastSeenAt <= 10f)) ? NekoP2PState.Punching : NekoP2PState.Direct);
					num3 = value2.ReportedRttMs;
					nekoNatType = value2.NatType;
				}
				else
				{
					nekoP2PState = ((!(realtimeSinceStartup - _startedAt >= 30f)) ? NekoP2PState.Punching : NekoP2PState.Waiting);
				}
				_writer.Put(nekoPartyMemberData.ProfileId, 0);
				_writer.Put((byte)nekoP2PState);
				_writer.Put(num3);
				_writer.Put((byte)nekoNatType);
			}
		}
		((LiteNetManager)_net).SendUnconnectedMessage(_writer, sender);
	}

	private static void HandlePong(IPEndPoint sender, NetPacketReader reader)
	{
		long num = 0L;
		if (_isLeader || !((NetDataReader)reader).TryGetLong(ref num))
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		int num2 = (int)Math.Max(0L, (long)(realtimeSinceStartup * 1000f) - num);
		if (_lastPongAt < 0f)
		{
			FikaGlobals.LogInfo($"[NekoP2P] 与队长打洞成功 ep={sender} rtt={num2}ms", "HandlePong");
		}
		_lastPongAt = realtimeSinceStartup;
		_selfRttMs = num2;
		_leaderEndPoint = sender;
		int num3 = 0;
		if (!((NetDataReader)reader).TryGetInt(ref num3) || num3 < 0 || num3 > 32)
		{
			return;
		}
		string text = null;
		byte item = 0;
		int item2 = 0;
		byte item3 = 0;
		for (int i = 0; i < num3; i++)
		{
			if (!((NetDataReader)reader).TryGetString(ref text))
			{
				break;
			}
			if (!((NetDataReader)reader).TryGetByte(ref item))
			{
				break;
			}
			if (!((NetDataReader)reader).TryGetInt(ref item2))
			{
				break;
			}
			if (!((NetDataReader)reader).TryGetByte(ref item3))
			{
				item3 = 0;
			}
			if (!string.IsNullOrEmpty(text))
			{
				_teamFromLeader[text] = ((NekoP2PState)item, item2, (NekoNatType)item3);
			}
		}
	}
}
