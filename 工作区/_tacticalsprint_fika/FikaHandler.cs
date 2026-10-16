using System;
using Comfort.Common;
using Fika.Core.Main.Components;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using FromHell.TacticalSprint;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TacticalSprint.Fika
{
    /// <summary>
    /// 参照 tarkin.ladders.fika 的同步套路：
    ///   注册包 → 订阅主 mod 的战术冲刺状态变化 → 发包；
    ///   收包 → 找到 observed 玩家 → 挂控制器写动画参数。
    /// </summary>
    internal class FikaHandler : IDisposable
    {
        public FikaHandler()
        {
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnFikaNetworkCreated);
            if (Singleton<IFikaNetworkManager>.Instantiated)
            {
                RegisterPackets(Singleton<IFikaNetworkManager>.Instance);
            }

            FromHell.TacticalSprint.Plugin.OnTacticalSprintChanged += OnTacticalSprintChanged;
        }

        private void OnFikaNetworkCreated(FikaNetworkManagerCreatedEvent fikaEvent)
        {
            RegisterPackets(fikaEvent.Manager);
        }

        private void RegisterPackets(IFikaNetworkManager manager)
        {
            manager.RegisterPacket<TacticalSprintPacket>(OnPacketReceived);
        }

        private void OnTacticalSprintChanged(bool active)
        {
            try
            {
                if (!CoopHandler.TryGetCoopHandler(out var coopHandler))
                {
                    return;
                }
                if ((Object)(object)coopHandler.MyPlayer == (Object)null)
                {
                    return;
                }

                TacticalSprintPacket packet = new TacticalSprintPacket
                {
                    PlayerId = coopHandler.MyPlayer.NetId,
                    Active = active
                };
                Singleton<IFikaNetworkManager>.Instance?.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 发包失败: " + e.Message));
            }
        }

        private void OnPacketReceived(TacticalSprintPacket packet)
        {
            try
            {
                if (!CoopHandler.TryGetCoopHandler(out var coopHandler))
                {
                    return;
                }
                if (!coopHandler.Players.TryGetValue(packet.PlayerId, out var player))
                {
                    return;
                }
                if (player.IsYourPlayer)
                {
                    return;
                }

                ObservedPlayerTacticalSprintController controller = player.GetComponent<ObservedPlayerTacticalSprintController>();
                if ((Object)(object)controller == (Object)null)
                {
                    controller = player.gameObject.AddComponent<ObservedPlayerTacticalSprintController>();
                }
                controller.SetActive(packet.Active);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 收包失败: " + e.Message));
            }
        }

        public void Dispose()
        {
            FromHell.TacticalSprint.Plugin.OnTacticalSprintChanged -= OnTacticalSprintChanged;
            FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>(OnFikaNetworkCreated);
            if (Singleton<IFikaNetworkManager>.Instantiated)
            {
                Singleton<IFikaNetworkManager>.Instance.UnregisterPacket<TacticalSprintPacket>();
            }
        }
    }
}
