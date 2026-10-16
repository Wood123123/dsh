using System;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EFT;
using UnityEngine;

namespace StashTools;

internal static class RaidPause
{
	internal static bool Ready;

	private static ConfigEntry<bool> _enabled = null;

	private static ConfigEntry<bool> _mute = null;

	private static ConfigEntry<KeyCode> _key = null;

	private static RaidPauseCoopAdapter? _adapter;

	private static PauseRoomContext? _context;

	private static PauseVoting? _host;

	private static PauseReplica _replica = new PauseReplica();

	private static long _sequence;

	private static bool _coopPaused;

	private static double _broadcastAt;

	private static long _broadcastRevision;

	private static float _retryAt;

	private static float _unavailableNoticeAt;

	internal static bool BlockGameplayUi
	{
		get
		{
			if (!SinglePlayerPause.Paused)
			{
				return _coopPaused;
			}
			return true;
		}
	}

	internal static bool CoopPaused => _coopPaused;

	internal static bool Enabled
	{
		get
		{
			if (Ready)
			{
				return _enabled?.Value ?? false;
			}
			return false;
		}
	}

	internal static string KeyLabel
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			return SettingsText.KeyName(((object)_key.Value/*cast due to constrained. prefix*/).ToString());
		}
	}

	private static bool MultiplayerInstalled => Chainloader.PluginInfos.Values.Any((PluginInfo p) => p.Metadata.GUID.Contains("fika", StringComparison.OrdinalIgnoreCase) || p.Metadata.Name.Contains("fika", StringComparison.OrdinalIgnoreCase));

	internal static void Configure(ConfigFile config)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		_enabled = config.Bind<bool>("11 战局暂停", "启用暂停战局", true, ChineseSettings.ToggleDescription("单机战局按热键暂停，再按恢复；冻结战局计时、游戏逻辑及状态。局外、藏身处和加载中不生效。联机必须安装与当前运行时匹配的暂停适配器，未适配时不会只暂停本机。"));
		_key = config.Bind<KeyCode>("11 战局暂停", "暂停战局热键", (KeyCode)290, new ConfigDescription("统一用于暂停、恢复、申请和同意。默认 F9；修改后提示显示你的本地按键。房主直接控制，队员申请；10 秒无人同意则取消，联机恢复前倒计时 3 秒。", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				CustomHotkeyDrawer = ChineseSettings.DrawKey
			}
		}));
		_mute = config.Bind<bool>("11 战局暂停", "单机暂停时静音", true, ChineseSettings.ToggleDescription("暂停单机战局声音，恢复时还原原来的声音状态。联机语音及声音由适配器处理。"));
	}

	private static void Notify(string text)
	{
		StashBehaviour.Notify(text);
	}

	internal static void Tick()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return;
		}
		try
		{
			if (!_enabled.Value)
			{
				End(report: true);
			}
			else
			{
				if (Time.unscaledTime < _retryAt)
				{
					return;
				}
				SinglePlayerPause.Tick();
				bool flag = !ChineseSettings.IsOpen && (int)_key.Value != 0 && Input.GetKeyDown(_key.Value);
				RaidPauseCoopAdapter adapter = RaidPauseCoopBridge.Adapter;
				PauseRoomContext pauseRoomContext = adapter?.Capture();
				if (pauseRoomContext != null)
				{
					SinglePlayerPause.End();
					if (!PauseVoting.ValidContext(pauseRoomContext) || !pauseRoomContext.Ready || !pauseRoomContext.AllMembersCompatible)
					{
						EndCoop(report: true);
						if (flag && Time.unscaledTime >= _unavailableNoticeAt)
						{
							_unavailableNoticeAt = Time.unscaledTime + 3f;
							string text = (string.IsNullOrWhiteSpace(pauseRoomContext.ReadinessReason) ? "正在等待联机战局与成员准备完成" : pauseRoomContext.ReadinessReason);
							text = text.Replace('<', '〈').Replace('>', '〉');
							if (text.Length > 180)
							{
								text = text.Substring(0, 180);
							}
							Plugin.Logger.LogWarning((object)("联机暂停暂不可用：" + text + "；房间=" + pauseRoomContext.RoomId + "，本机=" + pauseRoomContext.LocalId + "，房主=" + pauseRoomContext.HostId + "，Ready=" + pauseRoomContext.Ready + "，Members=" + (pauseRoomContext.Members?.Count ?? 0) + "，Compatible=" + pauseRoomContext.AllMembersCompatible));
							Notify("暂不能暂停：" + text);
						}
					}
					else
					{
						CoopTick(adapter, pauseRoomContext, flag);
					}
					return;
				}
				EndCoop(report: true);
				if (flag)
				{
					LocalGame game;
					GameWorld world;
					GamePlayerOwner owner;
					if (SinglePlayerPause.Paused)
					{
						SinglePlayerPause.End();
					}
					else if (MultiplayerInstalled)
					{
						Notify("当前 Fika 运行时尚未接入暂停同步，不能只暂停本机。");
					}
					else if (SinglePlayerPause.Context(out game, out world, out owner))
					{
						SinglePlayerPause.Begin(_mute.Value);
					}
				}
				if (SinglePlayerPause.Paused)
				{
					RaidPausePanel.Show("战局已暂停\n<size=18>按 " + KeyLabel + " 继续战局</size>", paused: true);
				}
				else
				{
					RaidPausePanel.Hide();
				}
			}
		}
		catch (Exception ex)
		{
			End(report: true);
			Plugin.Logger.LogError((object)("暂停操作已撤销，其他功能继续运行：" + ex));
			_retryAt = Time.unscaledTime + 2f;
			Notify("暂停未执行或已恢复：" + ex.Message);
		}
	}

	private static void CoopTick(RaidPauseCoopAdapter adapter, PauseRoomContext context, bool pressed)
	{
		if (_adapter != adapter || _context?.RoomId != context.RoomId || _context.HostId != context.HostId)
		{
			EndCoop(report: true);
			_adapter = adapter;
			_replica = new PauseReplica();
			_host = ((context.LocalId == context.HostId) ? new PauseVoting(context) : null);
			_sequence = 0L;
			_broadcastAt = 0.0;
			_broadcastRevision = 0L;
		}
		_context = context;
		if (_host != null)
		{
			_host.UpdateMembers(context.Members.Select((PauseMember m) => m.Id), context.HostTime);
		}
		for (int num = 0; num < 128; num++)
		{
			if (!RaidPauseCoopBridge.Read(out RaidPauseCoopBridge.Message message))
			{
				break;
			}
			if (message.Room != context.RoomId)
			{
				continue;
			}
			if (message.Failure)
			{
				if (_host != null)
				{
					_host.ResumeAfterFailure(message.Sender, context.HostTime);
				}
			}
			else if (message.State != null)
			{
				if (_host == null)
				{
					_replica.Accept(context, message.Sender, message.State);
				}
			}
			else if (_host != null)
			{
				_host.Press(message.Sender, message.Sequence, context.HostTime);
			}
		}
		if (pressed)
		{
			long num2 = ++_sequence;
			if (_host != null)
			{
				_host.Press(context.LocalId, num2, context.HostTime);
			}
			else
			{
				adapter.SendPressToHost(num2);
			}
		}
		if (_host != null)
		{
			_host.Tick(context.HostTime);
			PauseRoomState snapshot = _host.Snapshot;
			_replica.Accept(context, context.HostId, snapshot);
			if (snapshot.Revision != _broadcastRevision || context.HostTime >= _broadcastAt)
			{
				_broadcastAt = context.HostTime + 1.0;
				_broadcastRevision = snapshot.Revision;
				adapter.BroadcastFromHost(snapshot);
			}
		}
		PauseRoomState current = _replica.State;
		if (current == null)
		{
			return;
		}
		if (current.Paused != _coopPaused)
		{
			try
			{
				adapter.SetGameplayPaused(current.Paused);
				_coopPaused = current.Paused;
			}
			catch (Exception ex)
			{
				try
				{
					adapter.SetGameplayPaused(obj: false);
				}
				catch (Exception ex2)
				{
					Plugin.Logger.LogError((object)ex2);
				}
				try
				{
					adapter.ReportFailure("暂停状态应用失败：" + ex.Message);
				}
				catch (Exception ex3)
				{
					Plugin.Logger.LogError((object)ex3);
				}
				throw;
			}
		}
		string text = "";
		if (current.ResumeAt > 0.0)
		{
			text = "战局即将恢复\n<size=18>" + Math.Max(0.0, Math.Ceiling(current.ResumeAt - context.HostTime)) + " 秒后继续</size>";
		}
		else if (current.Requester.Length > 0)
		{
			string text2 = context.Members.First((PauseMember m) => m.Id == current.Requester).Name.Replace('<', '〈').Replace('>', '〉');
			string text3 = (current.RequestedPause ? "暂停" : "恢复");
			string text4 = ((context.LocalId == context.HostId) ? ("按 " + KeyLabel + " 直接" + text3) : (current.Agreed.Contains(context.LocalId) ? "已同意，等待确认" : ("按 " + KeyLabel + " 同意")));
			text = text2 + " 申请" + text3 + "战局\n<size=18>" + text4 + " · " + current.Agreed.Count + "/" + context.Members.Count + " · 剩余 " + Math.Max(0.0, Math.Ceiling(current.VoteEndsAt - context.HostTime)) + " 秒</size>";
		}
		else if (current.Paused)
		{
			text = "战局已暂停\n<size=18>按 " + KeyLabel + ((context.LocalId == context.HostId) ? " 恢复战局" : " 申请恢复战局") + "</size>";
		}
		else if (current.NoticeUntil > context.HostTime)
		{
			text = current.Notice;
		}
		if (text.Length > 0)
		{
			RaidPausePanel.Show(text, current.Paused);
		}
		else
		{
			RaidPausePanel.Hide();
		}
	}

	private static void EndCoop(bool report = false)
	{
		if (_adapter == null && _context == null && !_coopPaused && _host == null)
		{
			return;
		}
		bool flag = _adapter != null || _context != null;
		if (report && _coopPaused)
		{
			try
			{
				_adapter?.ReportFailure("暂停模块或房间状态发生变化，请统一恢复战局。");
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError((object)("联机暂停故障通知失败：" + ex));
			}
		}
		if (_adapter != null)
		{
			try
			{
				_adapter?.SetGameplayPaused(obj: false);
			}
			catch (Exception ex2)
			{
				Plugin.Logger.LogError((object)("联机暂停清理失败：" + ex2));
			}
		}
		_coopPaused = false;
		_adapter = null;
		_context = null;
		_host = null;
		_replica = new PauseReplica();
		if (flag)
		{
			RaidPausePanel.Hide();
		}
	}

	internal static void End(bool report = false)
	{
		SinglePlayerPause.End();
		EndCoop(report);
		RaidPausePanel.Hide();
	}

	internal static void Dispose()
	{
		End();
		RaidPausePanel.Dispose();
	}
}
