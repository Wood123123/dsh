using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Fika.Core.Main.Patches.World
{
    /// <summary>
    /// 客户端从地面直接拾取，绕开原生交互状态机。
    ///
    /// 原生拾取跑在 <c>EFT.PickUpState</c> 里，当它的完成回调丢失时（联机下常见：
    /// 库存操作被拒绝 / 重复投递 / 主机不回包），玩家会**永久留在那个状态**：
    ///   · 移动状态机拒绝视角旋转、移动、蹲下
    ///   · <c>MovementContext.CanInteract</c> 一直返回 "Can't interact in Pickup"
    ///   · ⇒ 交互提示不再出现 ⇒ 本局剩下的时间里地面战利品再也捡不起来
    ///
    /// 拦截拾取动作入口，直接执行同样的库存操作（ItemManipulator 放置 +
    /// ConvertOperationResultToOperation + Execute），从而**根本不进入那个状态**。
    /// 主机仍然权威：操作照样走 ClientInventoryController，战利品无法本地复制。
    ///
    /// 移植自 Fika 3.0.1 的 <c>InteractionContextHelper_PickupAction_Patch</c>。
    /// EFT 0.16.9 的差异：
    ///   · 方法名被混淆成 smethod_10，且少了尾部 Action completionCallback 参数
    ///     ⇒ 按**形状**匹配（static + 4 参数 Player/IItemOperationResult/Item/IPlayer），不硬编码名字
    ///   · Il2CppSystem.List / .Pointer / TryCast / GetIl2CppType 全部换成 Mono 等价物
    /// </summary>
    internal sealed class InteractionContextHelper_PickupAction_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var helperType = AccessTools.TypeByName("EFT.InteractionContextHelper");
            if (helperType == null)
            {
                throw new MissingMemberException("EFT.InteractionContextHelper not found");
            }

            // ★ 形状匹配：static、4 参数、签名 (Player, IItemOperationResult, Item, IPlayer)
            //   不依赖那个被混淆的名字，EFT 改标号也不影响。
            var target = helperType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(x =>
                {
                    if (x.GetParameters().Length != 4)
                    {
                        return false;
                    }

                    var p = x.GetParameters();
                    return p[0].ParameterType == typeof(Player)
                        && typeof(IItemOperationResult).IsAssignableFrom(p[1].ParameterType)
                        && p[2].ParameterType == typeof(Item)
                        && typeof(IPlayer).IsAssignableFrom(p[3].ParameterType);
                });

            if (target == null)
            {
                throw new MissingMethodException(
                    "InteractionContextHelper.<pickup>(Player, IItemOperationResult, Item, IPlayer) not found — " +
                    "EFT changed the pickup entry shape, the bypass will be skipped");
            }

            FikaGlobals.LogInfo($"[PickupBypass] hooked {helperType.Name}.{target.Name} " +
                                $"(params: {string.Join(", ", target.GetParameters().Select(x => x.ParameterType.Name))})");
            return target;
        }

        [PatchPrefix]
        public static bool Prefix(Player __0, Item __2)
        {
            // 只对客户端生效。主机是权威方，不需要绕过，也不该因为绕过而改变行为。
            if (__0 == null || __2 == null || !FikaBackendUtils.IsClient)
            {
                return true;
            }

            try
            {
                if (GroundLootPickup.TryTake(__0, __2))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                FikaGlobals.LogError($"[PickupBypass] failed for {__2.Id}: {exception}");
            }

            // 自己处理不了就回退原生 —— 至少不会因为我们的异常让人捡不起东西。
            return true;
        }
    }

    /// <summary>
    /// 把装备类战利品放进对应的装备槽。护甲、头盔、胸挂、背包、耳机只能存在于装备槽，
    /// 而通用的容器放置路径从不提供这些槽位，以前这些物品会被留在地上。
    ///
    /// 走的是已验证的原生路径：InventoryEquipment.GetSlot + Slot.CreateItemAddress +
    /// ItemManipulator.Move（dry run）+ ConvertOperationResultToOperation + Execute，
    /// 所以主机仍然权威。
    /// </summary>
    internal static class GroundLootEquip
    {
        private static readonly EquipmentSlot[] CandidateSlots =
        [
            EquipmentSlot.Headwear, EquipmentSlot.Earpiece, EquipmentSlot.ArmorVest,
            EquipmentSlot.TacticalVest, EquipmentSlot.Backpack
        ];

        internal static bool TryEquipFromGround(Player player, Item item, out string error)
        {
            error = null;
            if (player == null || item == null)
            {
                error = "no player or item";
                return false;
            }

            var controller = player.InventoryController;
            var equipment = player.Equipment;
            if (controller == null || equipment == null)
            {
                error = "no inventory controller or equipment";
                return false;
            }

            // 优先试该物品天生该去的槽，再退到其余候选槽
            var ordered = new List<EquipmentSlot>();
            var preferred = PreferredSlot(item);
            if (preferred != null)
            {
                ordered.Add(preferred.Value);
            }

            foreach (var candidate in CandidateSlots)
            {
                if (preferred == null || candidate != preferred.Value)
                {
                    ordered.Add(candidate);
                }
            }

            var reasons = new List<string>();
            foreach (var slotName in ordered)
            {
                Slot slot;
                try
                {
                    slot = equipment.GetSlot(slotName);
                }
                catch (Exception slotException)
                {
                    reasons.Add($"{slotName}: lookup failed ({slotException.Message})");
                    continue;
                }

                if (slot == null || slot.ContainedItem != null)
                {
                    continue;
                }

                if (slot.Locked)
                {
                    reasons.Add($"{slotName}: locked");
                    continue;
                }

                bool compatible;
                try
                {
                    compatible = slot.CheckCompatibility(item);
                }
                catch (Exception compatibilityException)
                {
                    reasons.Add($"{slotName}: compatibility check failed ({compatibilityException.Message})");
                    continue;
                }

                if (!compatible)
                {
                    continue;
                }

                try
                {
                    var placement = ItemManipulator.Move(item, slot.CreateItemAddress(), controller, true);
                    if (placement.Failed)
                    {
                        reasons.Add($"{slotName}: {(placement.Error == null ? "rejected" : placement.Error.ToString())}");
                        continue;
                    }

                    var operation = controller.ConvertOperationResultToOperation(placement.Value);
                    if (operation == null)
                    {
                        reasons.Add($"{slotName}: no operation from the placement result");
                        continue;
                    }

                    var capturedSlot = slotName;
                    controller.Execute(operation, new Callback(result =>
                    {
                        try
                        {
                            if (result?.Failed == true)
                            {
                                FikaGlobals.LogWarning($"[PickupBypass] equipping {item.Id} into {capturedSlot} failed: {result.Error}");
                            }

                            player.ForceInteractionsChanged();
                        }
                        catch (Exception callbackException)
                        {
                            FikaGlobals.LogError($"[PickupBypass] equipment pickup callback failed: {callbackException}");
                        }
                    }));
                    return true;
                }
                catch (Exception moveException)
                {
                    reasons.Add($"{slotName}: {moveException.Message}");
                }
            }

            error = reasons.Count == 0 ? "no equipment slot accepted the item" : string.Join("; ", reasons);
            return false;
        }

        private static EquipmentSlot? PreferredSlot(Item item)
        {
            // 类型探测绝不能把拾取流程带崩
            try
            {
                if (item is Armor) return EquipmentSlot.ArmorVest;
                if (item is Headwear) return EquipmentSlot.Headwear;
                if (item is Vest) return EquipmentSlot.TacticalVest;
                if (item is Backpack) return EquipmentSlot.Backpack;
                if (item is Headphones) return EquipmentSlot.Earpiece;
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }

    /// <summary>被绕过补丁直接调用的地面拾取实现。</summary>
    internal static class GroundLootPickup
    {
        /// <summary>
        /// 原生交互动作在**按住键**时会重复触发，以前每次调用都会派发一个独立的操作。
        /// 第二个会被主机拒绝（"Could not find item owner"），而那个拒绝会触发
        /// **权威库存替换**，导致背包界面整局空掉。这里对同一物品在 1.5 秒内只处理一次；
        /// 表有上限且自动过期，所以一次失败的拾取不会永久挡住该物品。
        /// </summary>
        private static readonly Dictionary<string, float> RecentTakes = [];

        private static bool IsRepeatedTake(string itemId)
        {
            lock (RecentTakes)
            {
                var now = UnityEngine.Time.realtimeSinceStartup;
                if (RecentTakes.TryGetValue(itemId, out var last) && now - last < 1.5f)
                {
                    return true;
                }

                if (RecentTakes.Count > 256)
                {
                    RecentTakes.Clear();
                }

                RecentTakes[itemId] = now;
                return false;
            }
        }

        internal static bool TryTake(Player player, Item item)
        {
            var controller = player.InventoryController;
            var localRoot = controller?.RootItem as CompoundItem;
            if (controller == null || localRoot == null)
            {
                return false;
            }

            var itemKey = item.Id.ToString();
            if (IsRepeatedTake(itemKey))
            {
                FikaGlobals.LogInfo($"[PickupBypass] {itemKey} is already being taken; ignoring the repeated action");
                return true;
            }

            // 护甲、头盔、胸挂只能进装备槽；而口袋里、胸挂里、背包里、安全箱里是**独立的
            // compound item**，所以要搜遍玩家携带的每一个容器，而不是只看库存根。
            // 否则放置会失败，东西明明放得下却留在地上。
            var targets = new List<CompoundItem>();
            var seen = new List<CompoundItem>();

            void AddTarget(CompoundItem candidate)
            {
                if (candidate == null)
                {
                    return;
                }

                // Mono 没有 Il2Cpp 的 Pointer，用引用相等去重（Entity 可能重写 Equals，不能用 Contains）
                foreach (var existing in seen)
                {
                    if (ReferenceEquals(existing, candidate))
                    {
                        return;
                    }
                }

                seen.Add(candidate);
                targets.Add(candidate);
            }

            AddTarget(localRoot);
            AddTarget(player.Equipment as CompoundItem);

            try
            {
                var carried = controller.Inventory?.GetPlayerItems(EPlayerItems.NonQuestItems);
                if (carried != null)
                {
                    foreach (var carriedItem in carried)
                    {
                        if (carriedItem is CompoundItem compound)
                        {
                            AddTarget(compound);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[PickupBypass] could not enumerate the carried containers: {exception.Message}");
            }

            // 装备（护甲/头盔/胸挂/背包/耳机）只能进装备槽，通用容器搜索永远不提供，
            // 所以先试专用槽位。原生路径已验证：Slot.CreateItemAddress +
            // ItemManipulator.Move(dry run) + ConvertOperationResultToOperation + Execute。
            if (GroundLootEquip.TryEquipFromGround(player, item, out var equipError))
            {
                FikaGlobals.LogInfo($"[PickupBypass] equip {item.Id} into its equipment slot (native pickup state skipped)");
                return true;
            }

            var placement = ItemManipulator.QuickFindAppropriatePlace(item, controller, targets,
                ItemManipulator.EMoveItemOrder.IgnoreItemParent | ItemManipulator.EMoveItemOrder.TryEquip, true);

            if (placement.Failed)
            {
                // ★ 绝不回退到原生拾取动作：那条路会进入 EFT.PickUpState 并把玩家锁死整局。
                //   宁可把东西留在地上，也不要锁死玩家。
                var reason = placement.Error == null ? "no error object" : placement.Error.ToString();
                FikaGlobals.LogWarning($"[PickupBypass] cannot place {item.Id} template={item.TemplateId} " +
                                       $"class={item.GetType().Name}: {reason}; " +
                                       $"slot attempt: {equipError ?? "not equipment"}; " +
                                       "keeping it on the ground instead of entering the native pickup state");
                player.ForceInteractionsChanged();
                return true;
            }

            var operation = controller.ConvertOperationResultToOperation(placement.Value);
            if (operation == null)
            {
                FikaGlobals.LogWarning($"[PickupBypass] no operation for {item.Id}");
                return false;
            }

            FikaGlobals.LogInfo($"[PickupBypass] taking {item.Id} through {operation.GetType().Name} " +
                                "(native pickup state skipped)");

            controller.Execute(operation, new Callback(result =>
            {
                try
                {
                    if (result?.Failed == true)
                    {
                        FikaGlobals.LogWarning($"[PickupBypass] pickup of {item.Id} failed: {result.Error}");
                    }

                    player.ForceInteractionsChanged();
                }
                catch (Exception exception)
                {
                    FikaGlobals.LogError($"[PickupBypass] callback failed: {exception}");
                }
            }));

            return true;
        }
    }
}
