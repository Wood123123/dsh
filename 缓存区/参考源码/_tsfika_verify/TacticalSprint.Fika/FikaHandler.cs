using System;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using FromHell.TacticalSprint;
using UnityEngine;

namespace TacticalSprint.Fika;

internal class FikaHandler : IDisposable
{
	private const float SettleSeconds = 0.15f;

	private const int SuppressLogThreshold = 5;

	private bool _hasSent;

	private bool _lastSentActive;

	private bool _hasPending;

	private bool _pendingActive;

	private float _pendingSince;

	private int _suppressed;

	public FikaHandler()
	{
		FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)OnFikaNetworkCreated);
		if (Singleton<IFikaNetworkManager>.Instantiated)
		{
			RegisterPackets(Singleton<IFikaNetworkManager>.Instance);
		}
		Plugin.OnTacticalSprintChanged += OnTacticalSprintChanged;
	}

	private void OnFikaNetworkCreated(FikaNetworkManagerCreatedEvent fikaEvent)
	{
		RegisterPackets(fikaEvent.Manager);
	}

	private void RegisterPackets(IFikaNetworkManager manager)
	{
		manager.RegisterPacket<TacticalSprintPacket>((Action<TacticalSprintPacket>)OnPacketReceived);
	}

	private void OnTacticalSprintChanged(bool active)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		try
		{
			CoopHandler val = default(CoopHandler);
			if (!CoopHandler.TryGetCoopHandler(ref val) || (Object)val.MyPlayer == (Object)null)
			{
				return;
			}
			if (_hasSent && _lastSentActive == active)
			{
				if (_hasPending)
				{
					_hasPending = false;
				}
				_suppressed++;
			}
			else if (!_hasPending || _pendingActive != active)
			{
				_pendingActive = active;
				_pendingSince = Time.unscaledTime;
				_hasPending = true;
			}
			else
			{
				_suppressed++;
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = Plugin.Log;
			if (log != null)
			{
				log.LogWarning((object)("[TacticalSprint.Fika] 状态登记失败: " + ex.Message));
			}
		}
	}

	internal void Tick()
	{
		if (!_hasPending || Time.unscaledTime - _pendingSince < 0.15f)
		{
			return;
		}
		bool pendingActive = _pendingActive;
		_hasPending = false;
		if (_suppressed >= 5)
		{
			ManualLogSource log = Plugin.Log;
			if (log != null)
			{
				log.LogInfo((object)("[TacticalSprint.Fika] 已抑制 " + _suppressed + " 次抖动发包，只发送稳定后的最终值: " + (pendingActive ? "进入" : "退出") + "战术冲刺"));
			}
		}
		_suppressed = 0;
		Send(pendingActive);
	}

	private void Send(bool active)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		try
		{
			CoopHandler val = default(CoopHandler);
			if (CoopHandler.TryGetCoopHandler(ref val) && !((Object)val.MyPlayer == (Object)null))
			{
				_lastSentActive = active;
				_hasSent = true;
				TacticalSprintPacket tacticalSprintPacket = new TacticalSprintPacket
				{
					PlayerId = val.MyPlayer.NetId,
					Active = active
				};
				IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
				if (instance != null)
				{
					instance.SendData<TacticalSprintPacket>(ref tacticalSprintPacket, (DeliveryMethod)2, true);
				}
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = Plugin.Log;
			if (log != null)
			{
				log.LogWarning((object)("[TacticalSprint.Fika] 发包失败: " + ex.Message));
			}
		}
	}

	private void OnPacketReceived(TacticalSprintPacket packet)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		try
		{
			CoopHandler val = default(CoopHandler);
			if (CoopHandler.TryGetCoopHandler(ref val) && val.Players.TryGetValue(packet.PlayerId, out var value) && !((Player)value).IsYourPlayer)
			{
				ObservedPlayerTacticalSprintController observedPlayerTacticalSprintController = ((Component)value).GetComponent<ObservedPlayerTacticalSprintController>();
				if ((Object)observedPlayerTacticalSprintController == (Object)null)
				{
					observedPlayerTacticalSprintController = ((Component)value).gameObject.AddComponent<ObservedPlayerTacticalSprintController>();
				}
				observedPlayerTacticalSprintController.SetActive(packet.Active);
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = Plugin.Log;
			if (log != null)
			{
				log.LogWarning((object)("[TacticalSprint.Fika] 收包失败: " + ex.Message));
			}
		}
	}

	public void Dispose()
	{
		Plugin.OnTacticalSprintChanged -= OnTacticalSprintChanged;
		FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)OnFikaNetworkCreated);
		if (Singleton<IFikaNetworkManager>.Instantiated)
		{
			Singleton<IFikaNetworkManager>.Instance.UnregisterPacket<TacticalSprintPacket>();
		}
	}
}
