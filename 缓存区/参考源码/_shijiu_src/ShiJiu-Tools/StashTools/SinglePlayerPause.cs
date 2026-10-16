using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.BattleTimer;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

internal static class SinglePlayerPause
{
	private static GameWorld? _world;

	private static LocalGame? _game;

	private static GamePlayerOwner? _owner;

	private static GameTimer? _timer;

	private static GameDateTime? _clock;

	private static PauseDateSnapshot _start;

	private static PauseDateSnapshot _escape;

	private static float _clockStart;

	private static float _scale;

	private static bool _audio;

	private static bool _changedAudio;

	private static long _pauseTimestamp;

	private static string _profile = "";

	private static readonly List<(TimerPanel Panel, DateTime End)> Panels = new List<(TimerPanel, DateTime)>();

	private static readonly List<Player> Effects = new List<Player>();

	private static readonly FieldRef<TimerPanel, DateTime> PanelEnd = AccessTools.FieldRefAccess<TimerPanel, DateTime>("_dateTime");

	internal static bool Paused { get; private set; }

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

	internal static bool Owns(GameWorld world)
	{
		if (Paused)
		{
			return world == _world;
		}
		return false;
	}

	internal static bool Owns(GamePlayerOwner owner)
	{
		if (Paused)
		{
			return owner == _owner;
		}
		return false;
	}

	internal static bool Owns(LocalGame game)
	{
		if (Paused)
		{
			return game == _game;
		}
		return false;
	}

	internal static bool Owns(GameTimer timer)
	{
		if (Paused)
		{
			return timer == _timer;
		}
		return false;
	}

	internal static bool Context(out LocalGame? game, out GameWorld? world, out GamePlayerOwner? owner)
	{
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Invalid comparison between Unknown and I4
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Invalid comparison between Unknown and I4
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Invalid comparison between Unknown and I4
		game = null;
		world = null;
		owner = null;
		if (!Singleton<AbstractGame>.Instantiated || !Singleton<GameWorld>.Instantiated)
		{
			return false;
		}
		AbstractGame instance = Singleton<AbstractGame>.Instance;
		if ((Object)(object)instance == (Object)null || ((object)instance).GetType().FullName != "EFT.LocalGame")
		{
			return false;
		}
		game = instance.TryCast<LocalGame>();
		world = Singleton<GameWorld>.Instance;
		Player val = world?.MainPlayer;
		int num;
		if (!((Object)(object)game == (Object)null) && !((Object)(object)val == (Object)null) && val.IsYourPlayer)
		{
			IHealthController healthController = val.HealthController;
			num = ((healthController == null || !healthController.IsAlive) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag = (byte)num != 0;
		if (!flag)
		{
			GameStatus status = ((AbstractGame)game).Status;
			bool flag2 = (((int)status == 0 || status - 5 <= 1) ? true : false);
			flag = flag2;
		}
		if (!flag)
		{
			GameTimer gameTimer = ((AbstractGame)game).GameTimer;
			if (gameTimer != null && (int)gameTimer.Status == 1)
			{
				owner = ((Component)val).GetComponent<GamePlayerOwner>();
				if ((Object)(object)owner != (Object)null && (Object)(object)owner.TryCast<HideoutPlayerOwner>() == (Object)null)
				{
					return (int)((PlayerOwner)owner).State == 1;
				}
				return false;
			}
		}
		return false;
	}

	internal static void Begin(bool mute)
	{
		if (Paused)
		{
			return;
		}
		if (!Context(out LocalGame game, out GameWorld world, out GamePlayerOwner owner))
		{
			throw new InvalidOperationException("当前不在可暂停的单机战局中");
		}
		if (Time.timeScale <= 0f)
		{
			throw new InvalidOperationException("游戏已经被其他功能暂停，请先恢复");
		}
		Player[] array = world.AllAlivePlayersList.ToArray();
		if (array.Any((Player p) => (Object)(object)p != (Object)null && !p.IsYourPlayer && !p.IsAI))
		{
			throw new InvalidOperationException("检测到其他真人玩家，不能使用单机暂停");
		}
		if (!Batch.Busy && !((ItemController)world.MainPlayer.InventoryController).HasActiveEvents)
		{
			InventoryScreen? screenView = StashBehaviour.ScreenView;
			if (screenView == null || !((Component)screenView).gameObject.activeInHierarchy || !Input.GetMouseButton(0))
			{
				_world = world;
				_game = game;
				_owner = owner;
				_timer = ((AbstractGame)game).GameTimer;
				(PauseDateSnapshot Start, PauseDateSnapshot Escape) tuple = PauseTimerAccess.Read(_timer);
				_start = tuple.Start;
				_escape = tuple.Escape;
				double totalSeconds = _timer.PastTime.TotalSeconds;
				if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds) || totalSeconds < 0.0 || totalSeconds > 86400.0)
				{
					throw new InvalidOperationException("战局计时器读数异常，未执行暂停");
				}
				_clock = world.GameDateTime;
				_clockStart = _clock?._realtimeSinceStartup ?? 0f;
				_scale = Time.timeScale;
				_audio = AudioListener.pause;
				_changedAudio = mute;
				Panels.Clear();
				Effects.Clear();
				TimerPanel[] array2 = Object.FindObjectsOfType<TimerPanel>(true);
				foreach (TimerPanel val in array2)
				{
					if ((Object)(object)val != (Object)null)
					{
						Panels.Add((val, PanelEnd.Invoke(val)));
					}
				}
				_profile = world.MainPlayer.ProfileId;
				_pauseTimestamp = Stopwatch.GetTimestamp();
				try
				{
					Paused = true;
					Time.timeScale = 0f;
					if (mute)
					{
						AudioListener.pause = true;
					}
					Player[] array3 = array;
					foreach (Player val2 in array3)
					{
						if (!((Object)(object)val2 == (Object)null))
						{
							val2.PauseAllEffectsOnPlayer();
							Effects.Add(val2);
						}
					}
					Selection.Clear();
					StashBehaviour.Drawing = false;
					DragPreview.Clear();
					MovementEnhancements.End();
					Plugin.Logger.LogInfo((object)$"战局已暂停：原生已用时间={totalSeconds:F3}s，计时字段已验证；生命周期更新继续运行。");
					return;
				}
				catch
				{
					End();
					throw;
				}
			}
		}
		throw new InvalidOperationException("请先完成物品操作，再暂停战局");
	}

	private static double AdvanceTimers()
	{
		long num = PauseClockPolicy.ElapsedTicks(_pauseTimestamp, Stopwatch.GetTimestamp(), Stopwatch.Frequency);
		PauseDateSnapshot start = _start.Shift(num);
		PauseDateSnapshot escape = _escape.Shift(num);
		if (_timer != null)
		{
			PauseTimerAccess.Write(_timer, start, escape);
		}
		double num2 = (double)num / 10000000.0;
		if (_clock != null)
		{
			_clock._realtimeSinceStartup = _clockStart + (float)num2;
		}
		foreach (var (val, dateTime) in Panels)
		{
			if ((Object)(object)val != (Object)null)
			{
				PanelEnd.Invoke(val) = dateTime.AddTicks(num);
			}
		}
		return num2;
	}

	internal static void Tick()
	{
		if (Paused)
		{
			LocalGame val = ((!Singleton<AbstractGame>.Instantiated) ? null : Singleton<AbstractGame>.Instance?.TryCast<LocalGame>());
			if (!SameWorld || (Object)(object)val == (Object)null || val != _game)
			{
				End();
			}
			else
			{
				AdvanceTimers();
			}
		}
	}

	internal static void End()
	{
		if (!Paused)
		{
			return;
		}
		double num = (double)PauseClockPolicy.ElapsedTicks(_pauseTimestamp, Stopwatch.GetTimestamp(), Stopwatch.Frequency) / 10000000.0;
		try
		{
			if (!SameWorld)
			{
				return;
			}
			try
			{
				num = AdvanceTimers();
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning((object)("恢复计时器失败：" + ex.Message));
			}
			foreach (Player effect in Effects)
			{
				try
				{
					if ((Object)(object)effect != (Object)null)
					{
						effect.UnpauseAllEffectsOnPlayer();
					}
				}
				catch (Exception ex2)
				{
					Plugin.Logger.LogWarning((object)("恢复状态效果失败：" + ex2.Message));
				}
			}
		}
		finally
		{
			Paused = false;
			try
			{
				if (Time.timeScale == 0f)
				{
					Time.timeScale = _scale;
				}
			}
			catch (Exception ex3)
			{
				Plugin.Logger.LogWarning((object)ex3.Message);
			}
			try
			{
				if (_changedAudio)
				{
					AudioListener.pause = _audio;
				}
			}
			catch (Exception ex4)
			{
				Plugin.Logger.LogWarning((object)ex4.Message);
			}
			HistoryCapture.AddPause(_profile, num);
			Panels.Clear();
			Effects.Clear();
			_world = null;
			_game = null;
			_owner = null;
			_timer = null;
			_clock = null;
			_start = default(PauseDateSnapshot);
			_escape = default(PauseDateSnapshot);
			_profile = "";
			Plugin.Logger.LogInfo((object)$"战局已恢复，暂停 {num:F1} 秒，已补偿战局计时。");
		}
	}
}
