using System;
using EFT;
using Fika.Core.Main.Players;
using UnityEngine;

namespace Fika.Core.Main.Utils
{
    /// <summary>
    /// 每帧检查本地玩家是否卡在拾取/交互状态里，超时就释放。
    ///
    /// 为什么不能只在"操作失败"时释放：状态可能比一个**报告成功**的操作活得更久。
    /// 所以必须独立看门，观察超时而不是观察失败。
    ///
    /// 频率与开销：
    ///   · 入口每帧调用，但只读几个属性（CurrentState / CanInteract / AnimatedInteractions）
    ///   · 正常状态下立即 return，不做任何分配
    ///   · 真正的释放动作只在"卡住 + 超过阈值 + 冷却过后"执行
    ///
    /// 阈值来自 Fika 3.0.1：拾取动画不足 2 秒，给到 5 秒；放任务物品（Plant）合法地更久，给到 20 秒。
    /// </summary>
    internal static class PickupStuckWatchdog
    {
        /// <summary>拾取/掠夺类状态：正常几帧就离开，给 5 秒已经很宽松。</summary>
        private const float LootStateTimeout = 8f;   // ★ 5 秒→8 秒，给正常拾取动画留余量

        /// <summary>放任务物品/绊线的动画本来就长，给 20 秒。</summary>
        // [已停用] Plant 不再由看门狗处理 —— 放置任务是主动长操作，不该被判定卡死。
        private const float PlantStateTimeout = 20f;

        /// <summary>开门/破门动画也有正常的长度，给 8 秒。</summary>
        private const float DoorStateTimeout = 8f;

        /// <summary>两次修复之间的最小间隔，避免反复进入死循环。</summary>
        private const float RepairCooldown = 8f;

        /// <summary>
        /// ★ dsh-fix: proceed 回调的最长等待。
        /// 一次正常的手部切换（主机校验 + 回包）远在这个时间之内；
        /// 超过它还没回包，就认定主机的回复丢了。
        /// </summary>
        private const float ProceedCallbackTimeout = 5f;

        /// <summary>检查节流：每秒一次就够，拾取动画本来就在秒级。</summary>
        private const float CheckInterval = 1f;

        private static float _nextCheckTime;
        private static float _stuckSince;
        private static float _nextRepairTime;
        private static int _repairCount;
        private static int _proceedExpiredCount;

        /// <summary>诊断用：累计释放次数。</summary>
        internal static int RepairCount => _repairCount;

        internal static void Tick(Player player)
        {
            if (player == null)
            {
                _stuckSince = 0f;
                return;
            }

            // 死亡 / 倒地 / 等待复活期间状态本来就是乱的，别动它
            try
            {
                if (player.HealthController == null || !player.HealthController.IsAlive)
                {
                    _stuckSince = 0f;
                    return;
                }
            }
            catch
            {
                _stuckSince = 0f;
                return;
            }

            if (Time.time < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = Time.time + CheckInterval;

            // ── ⓪ 丢弃主机永远没回复的 proceed 回调 ──────────────────────
            // 主机不回复时条目永久留在 _proceedCallbacks ⇒ WaitingForCallback 永真
            // ⇒ FikaClientFirearmController 的 CanPressTrigger / CanStartReload 永远 false
            // ⇒「开不了枪 / 手部卡住」直到本局结束。
            // 逐条按年龄过期（不是一刀切），见 FikaPlayer.ExpireStaleProceedCallbacks 的说明。
            if (player is FikaPlayer fikaPlayer)
            {
                try
                {
                    var dropped = fikaPlayer.ExpireStaleProceedCallbacks(ProceedCallbackTimeout);
                    if (dropped > 0)
                    {
                        _proceedExpiredCount += dropped;
                    }
                }
                catch (Exception exception)
                {
                    FikaGlobals.LogWarning($"[ProceedExpiry] failed: {exception.Message}");
                }
            }

            // ── ① 残留的物品栏锁 —— ★ 已改为【手动】触发，不再每秒自动清 ──────
            // 原先是自动清：只要 Locked 为 true 且状态名不在白名单里就立刻解。
            // 实测误伤 55 次（state=Idle/Run/Sprint），每次都打断正在进行中的物品操作：
            //   [LootDiag] ★ 本地被拒 ... 原因: Cannot apply item weapon...
            // ⇒ 玩家表现为「装备失败 / 手卡住 / 放置被打断」。
            //
            // 根因：ItemController.Locked 在【一切正常物品操作期间】本来就是 true（几百毫秒），
            //       而 Plant（放任务道具）合法持锁可达 60 秒，Loot（搜容器）也可能很久。
            //       任何「发现就清」的自动逻辑都必然误伤，阈值调到多大都不安全。
            //
            // 现在改为：默认完全不动；玩家按 End（可配）时手动调用 ReleaseNow()。
            // 零误伤，因为只在你确认卡住时才执行。

            // ── ② 卡住的拾取/交互状态 ─────────────────────────────────────
            if (!NativeInteractionRelease.IsStuck(player, out var detail))
            {
                _stuckSince = 0f;
                return;
            }

            if (_stuckSince == 0f)
            {
                _stuckSince = Time.time;
                return;
            }

            // ★ 已删除 Plant / DoorInteraction / BreachDoor 的独立阈值。
            // IsStuck 现在只放行 Pickup / Loot，其它状态根本走不到这里，所以统一用 LootStateTimeout。
            var timeout = LootStateTimeout;

            var stuckFor = Time.time - _stuckSince;
            if (stuckFor < timeout || Time.time < _nextRepairTime)
            {
                return;
            }

            _stuckSince = 0f;
            _nextRepairTime = Time.time + RepairCooldown;
            _repairCount++;

            FikaGlobals.LogWarning($"[PickupStuckWatchdog] stuck for {stuckFor:F1}s ({detail}) — releasing (#{_repairCount})");
            NativeInteractionRelease.Release(player, "stuck-pickup-state");
        }

        /// <summary>每局开始时重置，避免跨局残留的时间戳。</summary>
        internal static void Reset()
        {
            _nextCheckTime = 0f;
            _stuckSince = 0f;
            _nextRepairTime = 0f;
            _repairCount = 0;
            _lockRepairCount = 0;
            _proceedExpiredCount = 0;
        }

        private static int _lockRepairCount;

        /// <summary>
        /// ★ 手动解卡：玩家按 End（可配）时调用。
        ///
        /// 一次做三件事，顺序有讲究：
        ///   ① 丢弃主机永远没回复的 proceed 回调 —— 不清它，WaitingForCallback 永真，
        ///      手部控制器会一直拒绝开火/换弹，后面两步白做。
        ///   ② 释放卡住的拾取/交互状态 —— 回到 Idle，让动画机和交互系统重新起步。
        ///   ③ 清残留的物品栏锁 —— 放最后，因为前两步的 Exit 流程可能自己会动它；
        ///      清早了会被 Exit 再设回去。
        ///
        /// 不看当前状态名、不加阈值、不做冷却 —— 因为这是玩家主动按的键。
        /// 这样 Plant（合法持锁 60 秒）、Loot（搜容器）等长操作永远不会被误伤。
        /// </summary>
        internal static void ReleaseNow(Player player)
        {
            if (player == null)
            {
                return;
            }

            var stateText = "unknown";
            var movementContext = player.MovementContext;
            if (movementContext?.CurrentState != null)
            {
                stateText = movementContext.CurrentState.Name.ToString();
            }

            var lockedText = "n/a";
            try
            {
                lockedText = player.InventoryController != null && player.InventoryController.Locked
                    ? "locked"
                    : "unlocked";
            }
            catch
            {
                // 忽略
            }

            // ① proceed 回调（全量清，不看年龄 —— 手动触发就是要立刻恢复）
            var dropped = 0;
            if (player is FikaPlayer fikaPlayer)
            {
                try
                {
                    dropped = fikaPlayer.ExpireStaleProceedCallbacks(0f);
                }
                catch (Exception exception)
                {
                    FikaGlobals.LogWarning($"[ManualRelease] proceed expiry failed: {exception.Message}");
                }
            }

            // ② 交互/拾取状态
            var released = false;
            try
            {
                NativeInteractionRelease.Release(player, "manual-End-key");
                released = true;
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[ManualRelease] state release failed: {exception.Message}");
            }

            // ③ 物品栏锁
            var unlocked = false;
            try
            {
                var controller = player.InventoryController;
                if (controller != null && controller.Locked)
                {
                    controller.Locked = false;
                    unlocked = true;
                    _lockRepairCount++;
                }
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[ManualRelease] unlock failed: {exception.Message}");
            }

            FikaGlobals.LogWarning(
                $"[ManualRelease] End 键手动解卡 (state={stateText}, inventory={lockedText}) → " +
                $"proceed 清理 {dropped} 条, 状态释放 {(released ? "是" : "否")}, " +
                $"物品栏锁 {(unlocked ? "已解" : "未锁")}（累计手动解锁 #{_lockRepairCount}）");
        }

        /// <summary>这三个状态会合法地锁住物品栏（仅供文档参考，手动解卡不再使用）。</summary>
        private static bool IsInventoryLockState(EPlayerState state)
        {
            switch (state)
            {
                case EPlayerState.Approach:
                case EPlayerState.DoorInteraction:
                case EPlayerState.Stationary:
                case EPlayerState.Plant:
                case EPlayerState.Loot:
                case EPlayerState.Pickup:
                    return true;
                default:
                    return false;
            }
        }
    }
}
