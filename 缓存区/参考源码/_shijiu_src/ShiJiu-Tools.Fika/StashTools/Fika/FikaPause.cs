using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace StashTools.Fika;

public static class FikaPause
{
	private const string Provider = "shijiu.fika.2.4.4.pause";

	internal static readonly ManualLogSource Log = Logger.CreateLogSource("拾玖Fika暂停");

	private static readonly Dictionary<string, (double Seen, bool Ready, string Reason)> PeerReady = new Dictionary<string, (double, bool, string)>(StringComparer.Ordinal);

	private static readonly PauseApplyAcks Acks = new PauseApplyAcks();

	private static readonly FikaPauseReadiness Loading = new FikaPauseReadiness();

	private static readonly FikaPauseRegistration Registration = new FikaPauseRegistration();

	private static readonly List<NetPeer> ConnectedPeers = new List<NetPeer>();

	private static Harmony? _patcher;

	private static IFikaNetworkManager? _manager;

	private static CoopGame? _game;

	private static GameWorld? _world;

	private static List<PauseMember> _members = new List<PauseMember>();

	private static PauseHostClock _clock = new PauseHostClock();

	private static string _room = "";

	private static string _map = "";

	private static string _local = "";

	private static string _host = "";

	private static string _roster = "";

	private static string _rejectedRoom = "";

	private static string _prepareReason = "";

	private static string _transportMembersReason = "";

	private static string _reportedReadiness = "";

	private static double _nextHello;

	private static double _lastHello;

	private static bool _installed;

	private static bool _ready;

	private static bool _prepared;

	private static bool _helloCompatible;

	private static bool _failing;

	private static PauseRoomState? _lastState;

	private static bool IsHost
	{
		get
		{
			CoopGame? game = _game;
			if (game == null)
			{
				return false;
			}
			return game.GameController.IsServer;
		}
	}

	private static double Now => Time.unscaledTimeAsDouble;

	private static bool NetworkStarted
	{
		get
		{
			IFikaNetworkManager? manager = _manager;
			FikaServer val = (FikaServer)(object)((manager is FikaServer) ? manager : null);
			if (val == null)
			{
				IFikaNetworkManager? manager2 = _manager;
				FikaClient val2 = (FikaClient)(object)((manager2 is FikaClient) ? manager2 : null);
				if (val2 != null)
				{
					return val2.Started;
				}
				return false;
			}
			return val.Started;
		}
	}

	public static void Install()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		if (typeof(CoopGame).Assembly.GetName().Version != new Version(2, 4, 4, 0))
		{
			throw new NotSupportedException("联机暂停适配器需要 Fika.Core 2.4.4");
		}
		try
		{
			_patcher = new Harmony("shijiu.fika.2.4.4.pause");
			FikaPauseGameplay.Install(_patcher);
			FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)NetworkCreated);
			FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>((Action<FikaNetworkManagerDestroyedEvent>)NetworkDestroyed);
			FikaEventDispatcher.SubscribeEvent<FikaGameEndedEvent>((Action<FikaGameEndedEvent>)GameEnded);
			RaidPauseCoopBridge.Register("shijiu.fika.2.4.4.pause", new RaidPauseCoopAdapter
			{
				Capture = Capture,
				SendPressToHost = SendPress,
				BroadcastFromHost = Broadcast,
				SetGameplayPaused = Apply,
				ReportFailure = Fail
			});
			_installed = true;
			Attach(Singleton<IFikaNetworkManager>.Instance);
			Log.LogInfo((object)"Fika 2.4.4 联机暂停适配已注册：可靠通信、成员握手、应用确认与故障恢复。");
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private static void NetworkCreated(FikaNetworkManagerCreatedEvent e)
	{
		Attach(e.Manager);
	}

	private static void NetworkDestroyed(FikaNetworkManagerDestroyedEvent e)
	{
		if (_manager == e.Manager)
		{
			Attach(null);
		}
	}

	private static void GameEnded(FikaGameEndedEvent _)
	{
		EndRoom();
	}

	private static void Attach(IFikaNetworkManager? manager)
	{
		if (_manager != manager)
		{
			EndRoom();
			if (_manager != null && Registration.Registered)
			{
				try
				{
					_manager.UnregisterPacket<FikaPausePacket>();
				}
				catch (Exception ex)
				{
					Log.LogWarning((object)ex);
				}
			}
			_manager = manager;
			Registration.Reset();
		}
		if (_manager != null)
		{
			Registration.TryRegister(NetworkStarted, Now, delegate
			{
				_manager.RegisterPacket<FikaPausePacket, NetPeer>((Action<FikaPausePacket, NetPeer>)Receive);
			}, out Exception error);
			if (error != null)
			{
				Log.LogWarning((object)("暂停网络包注册尚未完成，将重试：" + error));
			}
		}
	}

	private static void EndRoom()
	{
		FikaPauseGameplay.Resume();
		RaidPause.End();
		_game = null;
		_world = null;
		_room = "";
		_map = "";
		_local = "";
		_host = "";
		_roster = "";
		_rejectedRoom = "";
		_members.Clear();
		PeerReady.Clear();
		Acks.Clear();
		_clock = new PauseHostClock();
		_lastState = null;
		_ready = false;
		_prepared = false;
		_helloCompatible = false;
		_nextHello = 0.0;
		_lastHello = 0.0;
		_prepareReason = "";
		_transportMembersReason = "";
		_reportedReadiness = "";
		ConnectedPeers.Clear();
		Loading.Reset();
	}

	public static void Tick()
	{
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Invalid comparison between Unknown and I4
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Invalid comparison between Unknown and I4
		if (!_installed)
		{
			return;
		}
		try
		{
			Attach(Singleton<IFikaNetworkManager>.Instance);
			IFikaNetworkManager? manager = _manager;
			object obj;
			if (manager == null)
			{
				obj = null;
			}
			else
			{
				CoopHandler coopHandler = manager.CoopHandler;
				obj = ((coopHandler != null) ? coopHandler.LocalGameInstance : null);
			}
			CoopGame val = (CoopGame)((obj is CoopGame) ? obj : null);
			GameWorld val2 = (Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null);
			if ((Object)(object)val == (Object)null || (Object)(object)val2 == (Object)null || val.GameController == null)
			{
				if ((Object)(object)_game != (Object)null)
				{
					EndRoom();
				}
				return;
			}
			IntPtr? intPtr = _game?.Identity();
			IntPtr intPtr2 = val.Identity();
			if (!intPtr.HasValue || intPtr.GetValueOrDefault() != intPtr2 || _world?.Identity() != val2.Identity())
			{
				EndRoom();
				_game = val;
				_world = val2;
			}
			FikaPlayer myPlayer = _manager.CoopHandler.MyPlayer;
			string local = ((myPlayer != null) ? ((Player)myPlayer).ProfileId : null) ?? "";
			string map = val.GameController.Location?.Id ?? "";
			bool isHost = IsHost;
			bool raidStarted = val.GameController.RaidStarted;
			bool gameStarted = (int)((AbstractGame)val).Status == 4;
			GameTimer gameTimer = ((AbstractGame)val).GameTimer;
			bool timerStarted = gameTimer != null && (int)gameTimer.Status == 1;
			bool transportReady = Registration.Registered && NetworkStarted;
			if (Loading.Observe(local, map, isHost, transportReady, raidStarted, gameStarted, timerStarted))
			{
				EndRoom();
				_game = val;
				_world = val2;
				Loading.Observe(local, map, isHost, transportReady, raidStarted, gameStarted, timerStarted);
			}
			_local = Loading.LocalId;
			_map = Loading.MapId;
			if (isHost)
			{
				_host = Loading.HostId;
				if (Loading.CanHandshake && _room.Length == 0)
				{
					_room = Guid.NewGuid().ToString("N");
				}
			}
			_ready = Loading.Ready;
			_prepareReason = Loading.Reason;
			bool flag = false;
			if (_ready)
			{
				try
				{
					flag = FikaPauseGameplay.CanPrepare(val, val2, out _prepareReason);
				}
				catch (Exception ex)
				{
					_prepareReason = "暂停接口检查失败：" + ex.GetType().Name + "：" + ex.Message;
				}
				if (_rejectedRoom.Length > 0 && _rejectedRoom == _room)
				{
					flag = false;
					_prepareReason = "正在重新建立暂停同步";
				}
			}
			if (!_ready && FikaPauseGameplay.Active)
			{
				Fail(Loading.Reason);
			}
			if (IsHost && _prepared && !flag && _lastState != null)
			{
				Fail("本机暂停条件发生变化");
			}
			_prepared = flag;
			if (!Loading.CanHandshake)
			{
				ReportReadiness();
				FikaPauseGameplay.Tick();
				return;
			}
			List<PauseMember> list;
			string text;
			if (IsHost)
			{
				list = ReadMembers();
				text = string.Join("\n", list.Select((PauseMember m) => m.Id).OrderBy<string, string>((string id) => id, StringComparer.Ordinal));
				if (text != _roster)
				{
					if (!FikaPauseGameplay.Active)
					{
						PauseRoomState? lastState = _lastState;
						if (lastState == null || !lastState.Paused)
						{
							goto IL_03d3;
						}
					}
					Fail("联机成员发生变化");
					goto IL_03d3;
				}
				goto IL_03e1;
			}
			if (_room.Length > 0 && Now - _lastHello > 6.0 && _rejectedRoom != _room)
			{
				Fail("与房主的暂停同步中断");
			}
			goto IL_04f4;
			IL_03d3:
			_members = list;
			_roster = text;
			goto IL_03e1;
			IL_03e1:
			_transportMembersReason = "";
			IFikaNetworkManager? manager2 = _manager;
			FikaServer val3 = (FikaServer)(object)((manager2 is FikaServer) ? manager2 : null);
			if (val3 != null)
			{
				ConnectedPeers.Clear();
				val3.NetServer.GetConnectedPeers(ConnectedPeers);
				if (!FikaPauseReadiness.CompleteTransportRoster(ConnectedPeers.Where((NetPeer p) => (Object)(object)p.Player == (Object)null || !_manager.CoopHandler.ExtractedPlayers.Contains(p.Player.NetId)).Select(delegate(NetPeer p)
				{
					FikaPlayer player = p.Player;
					return (player == null) ? null : ((Player)player).ProfileId;
				}), _members, _local))
				{
					_transportMembersReason = "有联机成员尚未生成或未进入暂停名单，等待完成加载";
				}
			}
			if (FikaPauseGameplay.Active && !AllCompatible())
			{
				Fail("暂停成员响应超时或正在操作物品");
			}
			if (Acks.Expired(Now))
			{
				Fail("有成员未确认暂停状态，已统一恢复");
			}
			goto IL_04f4;
			IL_04f4:
			if (Now >= _nextHello)
			{
				_nextHello = Now + 1.0;
				if (IsHost)
				{
					SendHello();
				}
				else
				{
					Send(new FikaPausePacket
					{
						Kind = FikaPausePacket.MessageKind.Capability,
						Room = _room,
						Map = _map,
						Sent = Now,
						Flag = Loading.CapabilityReady(_prepared, _clock.Ready, _room.Length > 0 && _rejectedRoom != _room),
						Sequence = ((_rejectedRoom.Length > 0 && _rejectedRoom == _room) ? 1 : 0),
						Json = LocalReadinessReason()
					});
				}
			}
			ReportReadiness();
			FikaPauseGameplay.Tick();
		}
		catch (Exception ex2)
		{
			Log.LogError((object)ex2);
			Fail("暂停适配出现异常：" + ex2.Message);
		}
	}

	private static List<PauseMember> ReadMembers()
	{
		CoopHandler handler = _manager.CoopHandler;
		List<PauseMember> list = (from g in handler.HumanPlayers.Where((FikaPlayer p) => (Object)(object)p != (Object)null && !handler.ExtractedPlayers.Contains(p.NetId)).Select(delegate(FikaPlayer p)
			{
				PauseMember obj3 = new PauseMember
				{
					Id = ((Player)p).ProfileId
				};
				Profile profile2 = ((Player)p).Profile;
				string obj4 = ((profile2 != null) ? profile2.Nickname : null) ?? ((Player)p).ProfileId;
				Profile profile3 = ((Player)p).Profile;
				obj3.Name = obj4.Substring(0, Math.Min(80, (((profile3 != null) ? profile3.Nickname : null) ?? ((Player)p).ProfileId).Length));
				return obj3;
			}).GroupBy<PauseMember, string>((PauseMember m) => m.Id, StringComparer.Ordinal)
			select g.First()).ToList();
		if (!list.Any((PauseMember m) => m.Id == _local))
		{
			PauseMember obj = new PauseMember
			{
				Id = _local
			};
			FikaPlayer myPlayer = handler.MyPlayer;
			object obj2;
			if (myPlayer == null)
			{
				obj2 = null;
			}
			else
			{
				Profile profile = ((Player)myPlayer).Profile;
				obj2 = ((profile != null) ? profile.Nickname : null);
			}
			if (obj2 == null)
			{
				obj2 = "房主";
			}
			obj.Name = (string)obj2;
			list.Add(obj);
		}
		if (!PauseWirePolicy.ValidMembers(list, _local, _local))
		{
			throw new InvalidOperationException("联机成员名单无效");
		}
		return list;
	}

	private static bool AllCompatible()
	{
		if (_prepared && _transportMembersReason.Length == 0)
		{
			return _members.All((PauseMember m) => m.Id == _local || (PeerReady.TryGetValue(m.Id, out (double, bool, string) value) && value.Item2 && Now - value.Item1 <= 6.0));
		}
		return false;
	}

	private static string LocalReadinessReason()
	{
		if (!_ready || !_prepared)
		{
			return ShortReason((_prepareReason.Length > 0) ? _prepareReason : "本机暂停接口尚未准备好");
		}
		if (IsHost)
		{
			return "";
		}
		if (_room.Length == 0 || _host.Length == 0)
		{
			return "正在与房主建立暂停同步";
		}
		if (_rejectedRoom == _room)
		{
			return "正在重新建立暂停同步";
		}
		if (!_clock.Ready)
		{
			return "正在校准房主的暂停同步时钟";
		}
		if (Now - _lastHello > 6.0)
		{
			return "房主的暂停同步响应已超时";
		}
		return "";
	}

	private static string ReadinessReason()
	{
		string text = LocalReadinessReason();
		if (text.Length > 0)
		{
			return text;
		}
		if (!IsHost)
		{
			if (!_helloCompatible)
			{
				return "等待房主确认所有联机成员的暂停准备状态";
			}
			return "";
		}
		if (_transportMembersReason.Length > 0)
		{
			return _transportMembersReason;
		}
		foreach (PauseMember item in _members.Where((PauseMember m) => m.Id != _local))
		{
			string text2 = (string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name);
			if (!PeerReady.TryGetValue(item.Id, out (double, bool, string) value))
			{
				return "等待成员「" + text2 + "」完成暂停握手，请确认双方安装了相同版本的联机暂停适配器";
			}
			if (Now - value.Item1 > 6.0)
			{
				return "成员「" + text2 + "」的暂停同步响应已超时";
			}
			if (!value.Item2)
			{
				return "成员「" + text2 + "」：" + ((value.Item3.Length > 0) ? value.Item3 : "暂停接口尚未准备好");
			}
		}
		return "";
	}

	private static string ShortReason(string? reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			return "";
		}
		return new string(reason.Where((char c) => !char.IsControl(c)).Take(240).ToArray());
	}

	private static void ReportReadiness()
	{
		string text = ReadinessReason();
		if (!(text == _reportedReadiness))
		{
			_reportedReadiness = text;
			Log.LogInfo((object)("暂停准备状态：" + ((text.Length > 0) ? text : "本机与所有成员已准备好")));
		}
	}

	private static PauseRoomContext? Capture()
	{
		if ((Object)(object)_game == (Object)null)
		{
			return null;
		}
		return new PauseRoomContext
		{
			RoomId = _room,
			LocalId = _local,
			HostId = _host,
			Members = _members.Select((PauseMember m) => new PauseMember
			{
				Id = m.Id,
				Name = m.Name
			}).ToList(),
			Ready = (_ready && _prepared && (IsHost || (_clock.Ready && Now - _lastHello <= 6.0))),
			AllMembersCompatible = (IsHost ? AllCompatible() : (_helloCompatible && _rejectedRoom != _room)),
			ReadinessReason = ReadinessReason(),
			HostTime = (IsHost ? Now : _clock.Now(Now))
		};
	}

	private static void Send(FikaPausePacket packet, NetPeer? peer = null)
	{
		if (_manager != null && Registration.Registered)
		{
			if (peer == null)
			{
				_manager.SendData<FikaPausePacket>(ref packet, (DeliveryMethod)2, false);
			}
			else
			{
				_manager.SendDataToPeer<FikaPausePacket>(ref packet, (DeliveryMethod)2, peer);
			}
		}
	}

	private static void SendHello(NetPeer? peer = null, double echo = 0.0)
	{
		Send(new FikaPausePacket
		{
			Kind = FikaPausePacket.MessageKind.Hello,
			Room = _room,
			Map = _map,
			Host = _host,
			Sent = echo,
			HostTime = Now,
			Flag = AllCompatible(),
			Json = JsonConvert.SerializeObject((object)_members)
		}, peer);
	}

	private static void SendPress(long sequence)
	{
		Send(new FikaPausePacket
		{
			Kind = FikaPausePacket.MessageKind.Press,
			Room = _room,
			Sequence = sequence
		});
	}

	private static void Broadcast(PauseRoomState state)
	{
		if (!IsHost || state.RoomId != _room)
		{
			return;
		}
		bool num = ((_lastState == null) ? state.Paused : (_lastState.Paused != state.Paused));
		_lastState = state.Copy();
		if (num)
		{
			Acks.Begin(state.Revision, state.Paused, from m in _members
				where m.Id != _local
				select m.Id, Now);
		}
		Send(new FikaPausePacket
		{
			Kind = FikaPausePacket.MessageKind.State,
			Room = _room,
			Host = _host,
			HostTime = Now,
			Json = JsonConvert.SerializeObject((object)state)
		});
	}

	private static void Apply(bool paused)
	{
		if (paused)
		{
			if ((Object)(object)_game == (Object)null || (Object)(object)_world == (Object)null || !_prepared)
			{
				throw new InvalidOperationException("本机暂停条件尚未准备好");
			}
			FikaPauseGameplay.Begin(_game, _world);
		}
		else
		{
			FikaPauseGameplay.Resume();
		}
		if (!IsHost && _lastState != null)
		{
			SendApplied(_lastState.Revision, paused);
		}
	}

	private static void SendApplied(long revision, bool paused)
	{
		Send(new FikaPausePacket
		{
			Kind = FikaPausePacket.MessageKind.Applied,
			Room = _room,
			Sequence = revision,
			Flag = paused
		});
	}

	private static void Receive(FikaPausePacket packet, NetPeer peer)
	{
		try
		{
			if (!_installed || (Object)(object)_game == (Object)null || packet.Protocol != 10402)
			{
				return;
			}
			if (IsHost)
			{
				FikaPlayer player = peer.Player;
				string sender = ((player != null) ? ((Player)player).ProfileId : null) ?? "";
				if (sender.Length == 0 || sender == _local || !_members.Any((PauseMember m) => m.Id == sender))
				{
					return;
				}
				if (packet.Kind == FikaPausePacket.MessageKind.Capability)
				{
					if (!(packet.Map != _map))
					{
						if (packet.Room == _room && packet.Sequence == 1)
						{
							Fail("成员请求重新建立暂停同步");
						}
						PeerReady[sender] = (Now, packet.Flag && packet.Room == _room, ShortReason(packet.Json));
						SendHello(peer, packet.Sent);
					}
				}
				else
				{
					if (packet.Room != _room || !PeerReady.TryGetValue(sender, out (double, bool, string) value) || Now - value.Item1 > 6.0)
					{
						return;
					}
					switch (packet.Kind)
					{
					case FikaPausePacket.MessageKind.Press:
						if (value.Item2)
						{
							RaidPauseCoopBridge.ReceivePress("shijiu.fika.2.4.4.pause", _room, sender, packet.Sequence);
						}
						break;
					case FikaPausePacket.MessageKind.Applied:
						if (_lastState != null && packet.Sequence <= _lastState.Revision)
						{
							Acks.Accept(sender, packet.Sequence, packet.Flag);
						}
						break;
					case FikaPausePacket.MessageKind.Failure:
						Fail("成员暂停状态应用失败或已断开");
						break;
					case FikaPausePacket.MessageKind.State:
						break;
					}
				}
				return;
			}
			IFikaNetworkManager? manager = _manager;
			FikaClient val = (FikaClient)(object)((manager is FikaClient) ? manager : null);
			if (val == null || peer != val.ServerConnection)
			{
				return;
			}
			if (packet.Kind == FikaPausePacket.MessageKind.Hello)
			{
				if (packet.Map != _map || string.IsNullOrWhiteSpace(packet.Room))
				{
					return;
				}
				List<PauseMember> members = JsonConvert.DeserializeObject<List<PauseMember>>(packet.Json);
				if (PauseWirePolicy.ValidMembers(members, packet.Host, _local))
				{
					if (_room != packet.Room)
					{
						FikaPauseGameplay.Resume();
						RaidPause.End();
						_lastState = null;
						_clock = new PauseHostClock();
						_room = packet.Room;
						_rejectedRoom = "";
						_nextHello = 0.0;
					}
					_host = packet.Host;
					_members = members;
					_lastHello = Now;
					_helloCompatible = packet.Flag;
					if (packet.Sent > 0.0)
					{
						_clock.Observe(packet.Sent, packet.HostTime, Now);
					}
				}
			}
			else
			{
				if (packet.Room != _room || _rejectedRoom == _room)
				{
					return;
				}
				switch (packet.Kind)
				{
				case FikaPausePacket.MessageKind.State:
				{
					PauseRoomState pauseRoomState = JsonConvert.DeserializeObject<PauseRoomState>(packet.Json);
					if (pauseRoomState == null || pauseRoomState.RoomId != _room || packet.Host != _host || (_lastState != null && pauseRoomState.Revision < _lastState.Revision))
					{
						break;
					}
					PauseRoomContext pauseRoomContext = Capture();
					if (pauseRoomContext != null && new PauseReplica().Accept(pauseRoomContext, _host, pauseRoomState))
					{
						_lastState = pauseRoomState.Copy();
						RaidPauseCoopBridge.ReceiveState("shijiu.fika.2.4.4.pause", _host, pauseRoomState);
						if (FikaPauseGameplay.Active == pauseRoomState.Paused)
						{
							SendApplied(pauseRoomState.Revision, pauseRoomState.Paused);
						}
					}
					break;
				}
				case FikaPausePacket.MessageKind.Failure:
					Fail("房主已取消暂停并恢复战局", report: false);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Log.LogError((object)ex);
			Fail("暂停同步包处理失败");
		}
	}

	internal static void Fail(string reason)
	{
		Fail(reason, report: true);
	}

	private static void Fail(string reason, bool report)
	{
		if (_failing)
		{
			return;
		}
		_failing = true;
		try
		{
			if (report && _room.Length > 0)
			{
				try
				{
					Send(new FikaPausePacket
					{
						Kind = FikaPausePacket.MessageKind.Failure,
						Room = _room
					});
				}
				catch (Exception ex)
				{
					Log.LogWarning((object)ex);
				}
			}
			FikaPauseGameplay.Resume();
			RaidPause.End();
			Acks.Clear();
			_lastState = null;
			if (IsHost)
			{
				_room = Guid.NewGuid().ToString("N");
				PeerReady.Clear();
			}
			else
			{
				_rejectedRoom = _room;
				_helloCompatible = false;
			}
			_nextHello = 0.0;
			Log.LogWarning((object)(reason + "，已解除本机暂停并重新进行成员握手。"));
		}
		finally
		{
			_failing = false;
		}
	}

	public static void Dispose()
	{
		try
		{
			EndRoom();
		}
		catch (Exception ex)
		{
			Log.LogWarning((object)ex);
		}
		RaidPauseCoopBridge.Unregister("shijiu.fika.2.4.4.pause");
		try
		{
			if (Registration.Registered)
			{
				IFikaNetworkManager? manager = _manager;
				if (manager != null)
				{
					manager.UnregisterPacket<FikaPausePacket>();
				}
			}
		}
		catch (Exception ex2)
		{
			Log.LogWarning((object)ex2);
		}
		_manager = null;
		Registration.Reset();
		FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)NetworkCreated);
		FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerDestroyedEvent>((Action<FikaNetworkManagerDestroyedEvent>)NetworkDestroyed);
		FikaEventDispatcher.UnsubscribeEvent<FikaGameEndedEvent>((Action<FikaGameEndedEvent>)GameEnded);
		Harmony? patcher = _patcher;
		if (patcher != null)
		{
			patcher.UnpatchSelf();
		}
		_patcher = null;
		_installed = false;
	}
}
