using System;
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
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		try
		{
			CoopHandler val = default(CoopHandler);
			if (CoopHandler.TryGetCoopHandler(ref val) && !((Object)val.MyPlayer == (Object)null))
			{
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
			Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 发包失败: " + ex.Message));
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
			Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 收包失败: " + ex.Message));
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
