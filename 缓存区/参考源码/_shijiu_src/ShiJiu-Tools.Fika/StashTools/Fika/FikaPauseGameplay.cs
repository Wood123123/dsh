using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.BattleTimer;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Generic;
using Fika.Core.Networking.Packets.World;
using HarmonyLib;
using UnityEngine;

namespace StashTools.Fika;

internal static class FikaPauseGameplay
{
	private sealed class DeferredCall
	{
		internal object Target;

		internal MethodBase Method;

		internal byte[]? Data;

		internal WorldPacket? World;

		internal NetPeer? Peer;

		internal int Cost;
	}

	private static CoopGame? _game;

	private static GameWorld? _world;

	private static GameTimer? _timer;

	private static GameDateTime? _dateTime;

	private static PauseDateSnapshot _start;

	private static PauseDateSnapshot _escape;

	private static DateTime _sessionStart;

	private static float _dateStart;

	private static float _scale;

	private static double _started;

	private static readonly List<Player> Effects = new List<Player>();

	private static readonly List<(TimerPanel Panel, DateTime End)> Panels = new List<(TimerPanel, DateTime)>();

	private static readonly PauseDeferredBuffer<DeferredCall> Deferred = new PauseDeferredBuffer<DeferredCall>();

	private static readonly FieldRef<TimerPanel, DateTime> PanelEnd = AccessTools.FieldRefAccess<TimerPanel, DateTime>("_dateTime");

	private static readonly FieldRef<CoopGame, DateTime> SessionStart = AccessTools.FieldRefAccess<CoopGame, DateTime>("SessionStartTime");

	private static ConstructorInfo _readerConstructor = null;

	internal static bool Active { get; private set; }

	private static double Elapsed => Math.Max(0.0, Time.unscaledTimeAsDouble - _started);

	private static bool SameWorld
	{
		get
		{
			if (Singleton<GameWorld>.Instantiated)
			{
				return Singleton<GameWorld>.Instance == _world;
			}
			return false;
		}
	}

	internal static void Install(Harmony patcher)
	{
		_readerConstructor = typeof(NetPacketReader).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single((ConstructorInfo c) => c.GetParameters().Length == 2);
		Type[] array = new Type[6]
		{
			typeof(FikaPauseSimulationPatch),
			typeof(FikaPauseExpirationPatch),
			typeof(FikaPauseDispatchPatch),
			typeof(FikaPauseSnapshotPatch),
			typeof(FikaPauseWorldPacketPatch),
			typeof(FikaPauseEndPatch)
		};
		foreach (Type type in array)
		{
			patcher.CreateClassProcessor(type).Patch();
		}
	}

	internal static bool CanPrepare(CoopGame game, GameWorld world, out string reason)
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Invalid comparison between Unknown and I4
		reason = "";
		if (!RaidPause.Enabled)
		{
			reason = "本机的战局暂停功能未开启";
			return false;
		}
		if (FikaBackendUtils.IsHeadlessGame || FikaBackendUtils.IsHeadless || FikaBackendUtils.IsSpectator)
		{
			reason = "此适配版暂不支持无头主机或旁观模式的暂停";
			return false;
		}
		if (Active)
		{
			if (SameWorld && _game == game)
			{
				return true;
			}
			reason = "正在切换战局";
			return false;
		}
		Player mainPlayer = world.MainPlayer;
		if ((Object)(object)mainPlayer == (Object)null)
		{
			reason = "本机玩家尚未生成";
			return false;
		}
		if (((AbstractGame)game).GameTimer == null || (int)((AbstractGame)game).GameTimer.Status != 1)
		{
			reason = "战局计时尚未开始";
			return false;
		}
		if (Time.timeScale <= 0f)
		{
			reason = "游戏已被其他功能冻结";
			return false;
		}
		if (!Batch.Busy)
		{
			InventoryController inventoryController = mainPlayer.InventoryController;
			if ((inventoryController == null || !((ItemController)inventoryController).HasActiveEvents) && !Input.GetMouseButton(0))
			{
				PauseTimerAccess.Read(((AbstractGame)game).GameTimer);
				SessionStart.Invoke(game);
				return true;
			}
		}
		reason = "请先完成当前物品或鼠标操作";
		return false;
	}

	internal static void Begin(CoopGame game, GameWorld world)
	{
		if (Active)
		{
			return;
		}
		if (!CanPrepare(game, world, out string reason))
		{
			throw new InvalidOperationException(reason);
		}
		_game = game;
		_world = world;
		_timer = ((AbstractGame)game).GameTimer;
		(PauseDateSnapshot Start, PauseDateSnapshot Escape) tuple = PauseTimerAccess.Read(_timer);
		_start = tuple.Start;
		_escape = tuple.Escape;
		_dateTime = world.GameDateTime;
		_dateStart = _dateTime?._realtimeSinceStartup ?? 0f;
		_sessionStart = SessionStart.Invoke(game);
		_scale = Time.timeScale;
		_started = Time.unscaledTimeAsDouble;
		Effects.Clear();
		Panels.Clear();
		Deferred.Clear();
		TimerPanel[] array = Object.FindObjectsOfType<TimerPanel>(true);
		foreach (TimerPanel val in array)
		{
			if ((Object)(object)val != (Object)null)
			{
				Panels.Add((val, PanelEnd.Invoke(val)));
			}
		}
		try
		{
			Player[] array2 = world.AllAlivePlayersList.ToArray();
			foreach (Player val2 in array2)
			{
				if (!((Object)(object)val2 == (Object)null))
				{
					FikaPlayer val3 = (FikaPlayer)(object)((val2 is FikaPlayer) ? val2 : null);
					if (val3 == null || !val3.Downed)
					{
						val2.PauseAllEffectsOnPlayer();
						Effects.Add(val2);
					}
				}
			}
			Active = true;
			Time.timeScale = 0f;
			Selection.Clear();
			StashBehaviour.Drawing = false;
			DragPreview.Clear();
			MovementEnhancements.End();
			FikaPause.Log.LogInfo((object)"联机模拟已暂停；网络、语音及暂停控制继续运行。");
		}
		catch
		{
			Active = true;
			Resume();
			throw;
		}
	}

	internal static void Tick()
	{
		if (Active)
		{
			if (!SameWorld)
			{
				Resume();
			}
			else if (Time.timeScale != 0f)
			{
				FikaPause.Fail("其他功能改变了暂停中的游戏速度");
			}
			else
			{
				Compensate();
			}
		}
	}

	internal static void Compensate()
	{
		if (!Active || !SameWorld)
		{
			return;
		}
		double elapsed = Elapsed;
		long num = checked((long)(elapsed * 10000000.0));
		if (_timer != null)
		{
			PauseTimerAccess.Write(_timer, _start.Shift(num), _escape.Shift(num));
		}
		if (_dateTime != null)
		{
			_dateTime._realtimeSinceStartup = _dateStart + (float)elapsed;
		}
		if ((Object)(object)_game != (Object)null)
		{
			SessionStart.Invoke(_game) = _sessionStart.AddTicks(num);
		}
		foreach (var (val, dateTime) in Panels)
		{
			if ((Object)(object)val != (Object)null)
			{
				PanelEnd.Invoke(val) = dateTime.AddTicks(num);
			}
		}
	}

	internal static void Resume()
	{
		if (!Active)
		{
			return;
		}
		double elapsed = Elapsed;
		bool sameWorld = SameWorld;
		try
		{
			if (sameWorld)
			{
				Compensate();
			}
		}
		catch (Exception ex)
		{
			FikaPause.Log.LogError((object)("恢复联机计时器失败：" + ex));
		}
		finally
		{
			Active = false;
			try
			{
				if (Time.timeScale == 0f)
				{
					Time.timeScale = _scale;
				}
			}
			catch (Exception ex2)
			{
				FikaPause.Log.LogError((object)ex2);
			}
			if (sameWorld)
			{
				foreach (Player effect in Effects)
				{
					try
					{
						if ((Object)(object)effect != (Object)null)
						{
							effect.UnpauseAllEffectsOnPlayer();
						}
					}
					catch (Exception ex3)
					{
						FikaPause.Log.LogError((object)ex3);
					}
				}
				Player[] array = _world.AllAlivePlayersList.ToArray();
				foreach (Player val in array)
				{
					try
					{
						((FikaPlayer)(((val is FikaPlayer) ? val : null)?)).Snapshotter?.Clear();
					}
					catch (Exception ex4)
					{
						FikaPause.Log.LogWarning((object)ex4);
					}
				}
				if ((Object)(object)_world.MainPlayer != (Object)null)
				{
					HistoryCapture.AddPause(_world.MainPlayer.ProfileId, elapsed);
				}
				Flush();
			}
			Deferred.Clear();
			Effects.Clear();
			Panels.Clear();
			_game = null;
			_world = null;
			_timer = null;
			_dateTime = null;
			FikaPause.Log.LogInfo((object)$"联机模拟已恢复，补偿暂停时间 {elapsed:F2} 秒。");
		}
	}

	private static bool Enqueue(DeferredCall call)
	{
		if (!Deferred.TryAdd(call, call.Cost))
		{
			FikaPause.Fail("暂停期间状态包过多");
			return true;
		}
		return false;
	}

	internal static bool DeferDispatch(object target, MethodBase method, object[] args)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I4
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Invalid comparison between Unknown and I4
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Invalid comparison between Unknown and I4
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		if (!Active)
		{
			return true;
		}
		try
		{
			NetPacketReader val = (NetPacketReader)args[0];
			bool flag;
			if (method.Name == "HandleGenericPacket")
			{
				EGenericPacketType val2 = (EGenericPacketType)((NetDataReader)val).PeekByte();
				if (val2 - 11 <= 1)
				{
					FikaPause.Fail("收到进行中的联机物品操作");
					return true;
				}
				if ((int)val2 <= 4)
				{
					if ((int)val2 <= 2 || (int)val2 == 4)
					{
						goto IL_0067;
					}
				}
				else if ((int)val2 == 10 || val2 - 13 <= 1)
				{
					goto IL_0067;
				}
				flag = false;
				goto IL_006f;
			}
			goto IL_0077;
			IL_0077:
			byte[] remainingBytes = ((NetDataReader)val).GetRemainingBytes(false);
			DeferredCall obj = new DeferredCall
			{
				Target = target,
				Method = method,
				Data = remainingBytes
			};
			object peer;
			if (args.Length <= 1)
			{
				peer = null;
			}
			else
			{
				object obj2 = args[1];
				peer = ((obj2 is NetPeer) ? obj2 : null);
			}
			obj.Peer = (NetPeer?)peer;
			obj.Cost = remainingBytes.Length;
			return Enqueue(obj);
			IL_0067:
			flag = true;
			goto IL_006f;
			IL_006f:
			if (flag)
			{
				return true;
			}
			goto IL_0077;
		}
		catch (Exception ex)
		{
			FikaPause.Log.LogError((object)ex);
			FikaPause.Fail("暂停状态包暂存失败");
			return true;
		}
	}

	internal static bool DeferWorld(object target, MethodBase method, object[] args)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		if (!Active)
		{
			return true;
		}
		try
		{
			WorldPacket val = (WorldPacket)args[0];
			WorldPacket val2 = new WorldPacket
			{
				ArtilleryPackets = val.ArtilleryPackets.ToList(),
				GrenadePackets = val.GrenadePackets.ToList(),
				SyncObjectPackets = val.SyncObjectPackets.ToList(),
				LootSyncStructs = val.LootSyncStructs.ToList()
			};
			int num = val2.ArtilleryPackets.Count + val2.GrenadePackets.Count + val2.SyncObjectPackets.Count + val2.LootSyncStructs.Count;
			DeferredCall obj = new DeferredCall
			{
				Target = target,
				Method = method,
				World = val2
			};
			object peer;
			if (args.Length <= 1)
			{
				peer = null;
			}
			else
			{
				object obj2 = args[1];
				peer = ((obj2 is NetPeer) ? obj2 : null);
			}
			obj.Peer = (NetPeer?)peer;
			obj.Cost = checked(256 + num * 1024);
			bool num2 = Enqueue(obj);
			if (!num2)
			{
				val.Flush();
			}
			return num2;
		}
		catch (Exception ex)
		{
			FikaPause.Log.LogError((object)ex);
			FikaPause.Fail("暂停世界状态暂存失败");
			return true;
		}
	}

	private static void Flush()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		while (SameWorld && Deferred.Count > 0)
		{
			DeferredCall deferredCall = Deferred.Take();
			try
			{
				object obj;
				if (deferredCall.World != null)
				{
					obj = deferredCall.World;
				}
				else
				{
					NetPacketReader val = (NetPacketReader)_readerConstructor.Invoke(new object[2]);
					((NetDataReader)val).SetSource(deferredCall.Data);
					obj = (object)val;
				}
				deferredCall.Method.Invoke(deferredCall.Target, (deferredCall.Method.GetParameters().Length != 1) ? new object[2] { obj, deferredCall.Peer } : new object[1] { obj });
			}
			catch (Exception ex)
			{
				FikaPause.Log.LogError((object)("恢复暂存联机状态失败：" + ex));
			}
		}
	}
}
