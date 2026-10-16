using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using StashTools.History;
using UnityEngine;

namespace StashTools.Fika;

public static class FikaHistory
{
	private const string Provider = "shijiu.fika.2.4.4.history";

	private static FikaHistorySession _session = new FikaHistorySession();

	private static IFikaGame? _game;

	private static GameWorld? _world;

	private static string _localCharacter = "";

	private static bool _installed;

	private static bool _raidStarted;

	private static bool _ended;

	private static double _nextScan;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}
		try
		{
			FikaEventDispatcher.SubscribeEvent<FikaGameCreatedEvent>((Action<FikaGameCreatedEvent>)Created);
			FikaEventDispatcher.SubscribeEvent<GameWorldStartedEvent>((Action<GameWorldStartedEvent>)WorldStarted);
			FikaEventDispatcher.SubscribeEvent<FikaRaidStartedEvent>((Action<FikaRaidStartedEvent>)RaidStarted);
			FikaEventDispatcher.SubscribeEvent<FikaGameEndedEvent>((Action<FikaGameEndedEvent>)Ended);
			HistoryCoopBridge.Register("shijiu.fika.2.4.4.history", Capture, _session.Identity, () => (_game == null) ? ((bool?)null) : new bool?(FikaBackendUtils.IsTransit));
			_installed = true;
			FikaPluginAdapter.Logger.LogInfo((object)"Fika 2.4.4 战绩已连接实际开局/结束事件，队伍使用房间 GUID 和账号身份关联。 ");
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private static void Created(FikaGameCreatedEvent e)
	{
		Protect(delegate
		{
			if (_game != e.Game)
			{
				_game = e.Game;
				_world = null;
				_localCharacter = "";
				_raidStarted = false;
				_ended = false;
				_nextScan = 0.0;
				_session.Begin(FikaBackendUtils.ServerGuid, FikaBackendUtils.IsTransit);
			}
		});
	}

	private static void WorldStarted(GameWorldStartedEvent e)
	{
		Protect(delegate
		{
			_world = e.GameWorld;
			Scan();
		});
	}

	private static void RaidStarted(FikaRaidStartedEvent _)
	{
		Protect(delegate
		{
			_raidStarted = true;
			Scan();
			EnsureBegin();
		});
	}

	private static void EnsureBegin()
	{
		if (!_ended && _raidStarted && !FikaBackendUtils.IsHeadless && !FikaBackendUtils.IsSpectator)
		{
			if ((Object)(object)_world == (Object)null && Singleton<GameWorld>.Instantiated)
			{
				_world = Singleton<GameWorld>.Instance;
			}
			Player val = LocalPlayer();
			if (!((Object)(object)val == (Object)null) && !((Object)(object)_world == (Object)null) && val.IsYourPlayer)
			{
				_localCharacter = val.ProfileId;
				RaidEarnings.Begin(val, _world);
				HistoryCapture.Guard(HistoryCapture.UpdateTeam);
			}
		}
	}

	private static void Ended(FikaGameEndedEvent e)
	{
		Protect(delegate
		{
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Invalid comparison between Unknown and I4
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			if (!_ended)
			{
				Protect(RaidEarnings.StopPrices);
				Protect(delegate
				{
					RaidPause.End();
				});
				Protect(Scan);
				Protect(EnsureBegin);
				Player player = null;
				Protect(delegate
				{
					player = LocalPlayer();
				});
				Player obj = player;
				string text = ((obj != null) ? obj.ProfileId : null) ?? _localCharacter;
				ExitStatus status = e.ExitStatus;
				Protect(delegate
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_004d: Unknown result type (might be due to invalid IL or missing references)
					//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
					if ((int)status == 0)
					{
						Player obj2 = player;
						if (obj2 != null)
						{
							IHealthController healthController = obj2.HealthController;
							if (((healthController != null) ? new bool?(healthController.IsAlive) : ((bool?)null)) == false)
							{
								status = (ExitStatus)1;
							}
						}
					}
					IFikaGame? game = _game;
					CoopGame val = (CoopGame)(object)((game is CoopGame) ? game : null);
					if (val != null)
					{
						Player obj3 = player;
						FikaPlayer val2 = (FikaPlayer)(object)((obj3 is FikaPlayer) ? obj3 : null);
						if (val2 != null && ((AbstractGame)val).GameTimer != null)
						{
							TimeSpan? sessionTime = ((AbstractGame)val).GameTimer.SessionTime;
							if (!_game.ExtractedPlayers.Contains(val2.NetId) && sessionTime.HasValue && ((AbstractGame)val).GameTimer.PastTime >= sessionTime.Value)
							{
								status = (ExitStatus)4;
							}
						}
					}
				});
				if (text.Length > 0)
				{
					try
					{
						RaidEarnings.Finish(text, status);
					}
					catch (Exception ex)
					{
						RaidEarnings.Fail(ex);
					}
				}
				_session.End((int)status == 5);
				_ended = true;
				_raidStarted = false;
				_world = null;
				FikaPluginAdapter.Logger.LogInfo((object)("Fika 结束事件已采集：" + ((object)Unsafe.As<ExitStatus, ExitStatus>(ref status)/*cast due to constrained. prefix*/).ToString() + "，行动=" + _session.ActionId));
			}
		});
	}

	private static Player? LocalPlayer()
	{
		CoopHandler val = default(CoopHandler);
		if (CoopHandler.TryGetCoopHandler(ref val) && (Object)(object)val != (Object)null && (Object)(object)val.MyPlayer != (Object)null)
		{
			return (Player?)(object)val.MyPlayer;
		}
		return _world?.MainPlayer;
	}

	private static void Scan()
	{
		if (_game == null || _ended || FikaBackendUtils.IsHeadless || FikaBackendUtils.IsSpectator || (!_session.SegmentOpen && !_session.Begin(FikaBackendUtils.ServerGuid, FikaBackendUtils.IsTransit)))
		{
			return;
		}
		CoopHandler handler = default(CoopHandler);
		if (CoopHandler.TryGetCoopHandler(ref handler) && (Object)(object)handler != (Object)null)
		{
			FikaPlayer[] array = handler.Players?.Values.ToArray() ?? Array.Empty<FikaPlayer>();
			foreach (FikaPlayer player in array)
			{
				if ((Object)(object)player != (Object)null)
				{
					Protect(delegate
					{
						Observe((Player)(object)player);
					});
				}
			}
			if ((Object)(object)handler.MyPlayer != (Object)null)
			{
				Protect(delegate
				{
					Observe((Player)(object)handler.MyPlayer);
				});
			}
		}
		Player local = LocalPlayer();
		if ((Object)(object)local != (Object)null)
		{
			Protect(delegate
			{
				Observe(local);
			});
		}
	}

	private static void Observe(Player player)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Invalid comparison between Unknown and I4
		Profile profile = player.Profile;
		if (profile != null)
		{
			bool isAi = player.IsAI || (((FikaPlayer)(((player is FikaPlayer) ? player : null)?)).IsObservedAI ?? false);
			string text = profile.Info?.MainProfileNickname;
			if (string.IsNullOrWhiteSpace(text))
			{
				text = profile.Nickname ?? "";
			}
			_session.Observe(player.ProfileId, profile.AccountId, text, isAi, (int)player.Side == 4);
			if (player.IsYourPlayer)
			{
				_localCharacter = player.ProfileId;
			}
		}
	}

	private static CoopRaidContext? Capture(string character)
	{
		if (_game == null || _ended)
		{
			return null;
		}
		Scan();
		if (_localCharacter != character || !_session.ContainsMember(character))
		{
			return null;
		}
		return _session.Capture("shijiu.fika.2.4.4.history");
	}

	public static void Tick()
	{
		if (!_installed || _game == null || _ended || Time.unscaledTimeAsDouble < _nextScan)
		{
			return;
		}
		_nextScan = Time.unscaledTimeAsDouble + 1.0;
		if (_raidStarted && (Object)(object)_world != (Object)null && (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance?.Identity() != _world.Identity()))
		{
			_session.End(transit: false);
			_ended = true;
			_world = null;
		}
		else
		{
			Protect(delegate
			{
				Scan();
				EnsureBegin();
			});
		}
	}

	private static void Protect(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			FikaPluginAdapter.Logger.LogWarning((object)("Fika 战绩事件采集失败，游戏流程继续：" + ex));
		}
	}

	public static void Dispose()
	{
		HistoryCoopBridge.Unregister("shijiu.fika.2.4.4.history");
		FikaEventDispatcher.UnsubscribeEvent<FikaGameCreatedEvent>((Action<FikaGameCreatedEvent>)Created);
		FikaEventDispatcher.UnsubscribeEvent<GameWorldStartedEvent>((Action<GameWorldStartedEvent>)WorldStarted);
		FikaEventDispatcher.UnsubscribeEvent<FikaRaidStartedEvent>((Action<FikaRaidStartedEvent>)RaidStarted);
		FikaEventDispatcher.UnsubscribeEvent<FikaGameEndedEvent>((Action<FikaGameEndedEvent>)Ended);
		_installed = false;
		_game = null;
		_world = null;
		_raidStarted = false;
		_ended = true;
		_localCharacter = "";
		_session = new FikaHistorySession();
	}
}
