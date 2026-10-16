using System;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using Manimal.Icebreaker.Blowtorch;
using Manimal.Icebreaker.Keypad;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

internal class IceSyncHandler : IDisposable
{
	internal IceSyncHandler()
	{
		FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)OnManagerCreated);
		if (Singleton<IFikaNetworkManager>.Instantiated)
		{
			Register(Singleton<IFikaNetworkManager>.Instance);
		}
		IcebreakerChainDoor.WorldEvent += OnChainEvent;
		IcebreakerSealedDoors.SealEvent += OnSealEvent;
		IcebreakerHeliExfil.HeliCalled += OnHeliCalled;
		IcebreakerCrew.ProgressDoorUnlocked += OnProgressDoor;
		HatchMeltDriver.HatchStage += OnHatchStage;
		BlowtorchController.LocalTorchFiring += OnTorchFiring;
		IcebreakerCrew.CutsceneActivated += OnCutsceneActivated;
		IcebreakerTripwires.SeedRolled += OnTripwireSeed;
		Manimal.Icebreaker.Keypad.Keypad.UnlockCommitted += OnKeypadUnlock;
	}

	private void OnManagerCreated(FikaNetworkManagerCreatedEvent ev)
	{
		Register(ev.Manager);
	}

	private void Register(IFikaNetworkManager manager)
	{
		manager.RegisterPacket<IceWorldPacket>((Action<IceWorldPacket>)OnReceived);
		IceBodyDiag.Ensure();
		IceTorchSync.Ensure();
		FikaAddonPlugin.Log.LogInfo((object)"[IceSync] packet registered");
	}

	private void OnChainEvent(string ev)
	{
		Send(new IceWorldPacket
		{
			Kind = ((!(ev == "plant")) ? ((byte)1) : ((byte)0))
		}, (DeliveryMethod)2);
	}

	private void OnSealEvent(string doorId, bool sealing)
	{
		Send(new IceWorldPacket
		{
			Kind = 2,
			DoorId = doorId,
			Sealing = sealing
		}, (DeliveryMethod)2);
	}

	private void OnHeliCalled()
	{
		Send(new IceWorldPacket
		{
			Kind = 3
		}, (DeliveryMethod)2);
	}

	private void OnProgressDoor()
	{
		Send(new IceWorldPacket
		{
			Kind = 4
		}, (DeliveryMethod)2);
	}

	private void OnHatchStage(byte stage)
	{
		Send(new IceWorldPacket
		{
			Kind = (byte)(5 + stage)
		}, (DeliveryMethod)2);
	}

	private void OnTorchFiring(bool on)
	{
		Player obj = Singleton<GameWorld>.Instance?.MainPlayer;
		FikaPlayer val = (FikaPlayer)(object)((obj is FikaPlayer) ? obj : null);
		if (!((Object)(object)val == (Object)null))
		{
			Send(new IceWorldPacket
			{
				Kind = 8,
				DoorId = val.NetId.ToString(),
				Sealing = on
			}, (DeliveryMethod)2);
		}
	}

	private void OnCutsceneActivated(string profileId)
	{
		Send(new IceWorldPacket
		{
			Kind = 9,
			DoorId = profileId
		}, (DeliveryMethod)2);
	}

	private void OnTripwireSeed(int seed)
	{
		Send(new IceWorldPacket
		{
			Kind = 12,
			IntA = seed
		}, (DeliveryMethod)2);
	}

	private void OnKeypadUnlock(string doorId)
	{
		Send(new IceWorldPacket
		{
			Kind = 13,
			DoorId = doorId
		}, (DeliveryMethod)2);
	}

	internal static void Send(IceWorldPacket packet, DeliveryMethod delivery = (DeliveryMethod)2, bool quiet = false)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
		if (instance != null)
		{
			instance.SendData<IceWorldPacket>(ref packet, delivery, true);
			if (!quiet)
			{
				FikaAddonPlugin.Log.LogInfo((object)$"[IceSync] sent kind={packet.Kind} door='{packet.DoorId}'");
			}
		}
	}

	private void OnReceived(IceWorldPacket packet)
	{
		if (packet.Kind != 11)
		{
			FikaAddonPlugin.Log.LogInfo((object)$"[IceSync] received kind={packet.Kind} door='{packet.DoorId}'");
		}
		try
		{
			Dispatch(packet);
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogWarning((object)$"[IceSync] handler for kind {packet.Kind} failed: {ex.Message}");
		}
	}

	private void Dispatch(IceWorldPacket packet)
	{
		switch (packet.Kind)
		{
		case 0:
			IcebreakerChainDoor.ApplyRemote("plant");
			break;
		case 1:
			IcebreakerChainDoor.ApplyRemote("open");
			break;
		case 2:
			IcebreakerSealedDoors.ApplyRemote(packet.DoorId, packet.Sealing);
			break;
		case 3:
			IcebreakerHeliExfil.ApplyRemoteCall();
			break;
		case 4:
			IcebreakerCrew.ApplyRemoteProgressDoor();
			break;
		case 5:
			HatchMeltDriver.ApplyRemoteStage(0);
			break;
		case 6:
			HatchMeltDriver.ApplyRemoteStage(1);
			break;
		case 7:
			HatchMeltDriver.ApplyRemoteStage(2);
			break;
		case 8:
		{
			if (int.TryParse(packet.DoorId, out var result))
			{
				IceTorchSync.SetRemoteFlame(result, packet.Sealing);
			}
			break;
		}
		case 9:
			IcebreakerCrew.ApplyRemoteCutsceneActivation(packet.DoorId);
			break;
		case 12:
			IcebreakerTripwires.ApplyRemoteSeed(packet.IntA);
			break;
		case 13:
			Manimal.Icebreaker.Keypad.Keypad.ApplyRemoteUnlock(packet.DoorId);
			break;
		default:
			FikaAddonPlugin.Log.LogWarning((object)$"[IceSync] unknown event kind {packet.Kind}");
			break;
		}
	}

	public void Dispose()
	{
		IcebreakerChainDoor.WorldEvent -= OnChainEvent;
		IcebreakerSealedDoors.SealEvent -= OnSealEvent;
		IcebreakerHeliExfil.HeliCalled -= OnHeliCalled;
		IcebreakerCrew.ProgressDoorUnlocked -= OnProgressDoor;
		HatchMeltDriver.HatchStage -= OnHatchStage;
		BlowtorchController.LocalTorchFiring -= OnTorchFiring;
		IcebreakerCrew.CutsceneActivated -= OnCutsceneActivated;
		IcebreakerTripwires.SeedRolled -= OnTripwireSeed;
		Manimal.Icebreaker.Keypad.Keypad.UnlockCommitted -= OnKeypadUnlock;
		FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>((Action<FikaNetworkManagerCreatedEvent>)OnManagerCreated);
		try
		{
			IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
			if (instance != null)
			{
				object? obj = AccessTools.Field(((object)instance).GetType(), "_packetProcessor")?.GetValue(instance);
				object? obj2 = ((obj is NetPacketProcessor) ? obj : null);
				if (obj2 != null)
				{
					((NetPacketProcessor)obj2).RemoveSubscription<IceWorldPacket>();
				}
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = FikaAddonPlugin.Log;
			if (log != null)
			{
				log.LogWarning((object)("[IceSync] unregister failed: " + ex.Message));
			}
		}
	}
}
