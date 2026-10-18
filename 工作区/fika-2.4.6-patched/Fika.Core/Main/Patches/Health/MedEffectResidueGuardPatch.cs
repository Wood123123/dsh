using System;
using EFT.HealthSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Fika.Core.Main.Patches.Health
{
    /// <summary>
    /// ★ dsh-fix: 防止「喝完/吃完东西后药效状态机卡死 ⇒ 不能跑」。
    ///
    /// 现象（客户端日志）：
    ///   [Error : Unity Log] Exception: Trying to get parent of an item that doesn't have a parent: 牛奶
    ///     EFT.InventoryLogic.Item.get_Parent()
    ///     EFT.HealthSystem.ActiveHealthController+MedEffect.Residue()
    ///     EFT.HealthSystem.ActiveHealthController+Effect.NextState()
    ///     EFT.HealthSystem.ActiveHealthController+Effect.ManualUpdate()
    ///     Fika.Core.Main.Players.FikaPlayer:HealthControllerUpdate()
    /// 然后玩家卡在「正在使用药物」（HandsController = MedsController）⇒ 不能跑，按 End 也救不回来。
    ///
    /// 根因（读 IL 确认）：
    ///   Item.get_Parent() 的实现是
    ///       var addr = CurrentAddress;
    ///       if (addr != null) return addr;
    ///       throw new Exception("Trying to get parent of an item that doesn't have a parent: " + ShortName);
    ///   ⇒ 只要物品不在任何容器/槽位里，读 Parent 必然抛，不是空引用。
    ///
    ///   而 MedEffect.Residue() 的**末尾**必然调用：
    ///       var owner = MedItem.Parent.GetOwner();
    ///       owner.RaiseEvent(new DrainItemEventArgs(MedItem, 1, owner));
    ///   ⇒ EFT 假设「结算完成那一刻，食物还在背包里」。
    ///
    ///   但在联机下会打破这个假设：
    ///       Residue 里每降一点资源都调 NetworkSyncEffectMedResource 通知主机
    ///       ⇒ 主机把见底的物品丢弃
    ///       ⇒ 描述符回传客户端
    ///       ⇒ 客户端本地把物品移出背包
    ///       ⇒ 而本地 Residue() 还没执行到 get_Parent()
    ///       ⇒ 时序竞态 ⇒ 抛异常 ⇒ Effect 状态机中断 ⇒ MedsController 收不了尾
    ///
    /// 修法：Prefix 检查 MedItem 是否还挂在某个地址上。
    ///   已经不在 ⇒ 直接跳过整个 Residue。物品都没了，本来也没什么可结算的
    ///   （最后那点能量/水分、刺激素 buff 本来就该随物品消失）。
    ///
    /// 影响面：
    ///   · 只在「物品已被移除」这个异常路径上跳过，正常吃喝结算完全不受影响
    ///   · 不改任何网络包、不改 inventory 数据
    ///   · 单人/离线根本不触发（离线时物品不会提前消失）
    /// </summary>
    internal sealed class MedEffectResidueGuardPatch : ModulePatch
    {
        protected override System.Reflection.MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(ActiveHealthController.MedEffect),
                nameof(ActiveHealthController.MedEffect.Residue));
        }

        [PatchPrefix]
        private static bool PatchPrefix(ActiveHealthController.MedEffect __instance)
        {
            // 拿不到实例就放行，让原逻辑自己处理（不引入新的崩溃点）
            if (__instance == null)
            {
                return true;
            }

            try
            {
                var item = __instance.MedItem;
                if (item == null)
                {
                    return false;
                }

                // ★ 关键判定：CurrentAddress 为 null ⇒ 物品已被移出背包
                //   ⇒ get_Parent() 必然 throw ⇒ 跳过 Residue
                if (item.CurrentAddress == null)
                {
                    return false;
                }
            }
            catch
            {
                // 读属性本身出异常（极罕见）⇒ 保守跳过，也不让它冒到 Effect 状态机
                return false;
            }

            return true;
        }
    }
}
