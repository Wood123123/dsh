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

            // ── ① 残留的物品栏锁 ───────────────────────────────────────────
            // ItemController.Locked 只被三个移动状态设置、在它们的 Exit 里清除：
            //   ApproachState          （接近交互物）
            //   DoorInteractState      （开门）
            //   StationaryPlayerState  （原地蹲/趴，还有一个 Spawned 设置点）
            // 交互被打断（掉线、切图、死亡动画、主机卡顿）时 Exit 不会被调用，锁就永久挂着。
            // 之后 ItemController.CheckItemAction 会抛
            //   PlayerIsBusyError: Cannot apply {item} because {target} is currently being modified
            // ⇒ 该玩家之后的一切物品操作全部失败，直到本局结束。
            // 注意：这个锁【不会】被"重发库存"修掉 —— 重发换的是 inventory 数据，
            //       锁是运行时标志，两回事。
            TryClearStuckInventoryLock(player);

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
            _lastLockReport = EPlayerState.None;
        }

        private static int _lockRepairCount;
        private static EPlayerState _lastLockReport;

        /// <summary>
        /// 清掉残留的物品栏锁。
        ///
        /// 判据用的是 EFT 自己的 <c>MovementContext.StateLocksInventory</c>（由那三个状态在
        /// Enter/Exit/Spawned 里维护）**加上**对当前状态名的二次确认 —— 两条都说不该锁，才动手。
        /// 这样即使 StateLocksInventory 本身也烂了，只要状态名不对就仍能救回来。
        ///
        /// 状态为 null（战局加载中 / 卸载中）时不动：那种时刻 Locked 可能是引擎的正常中间态，
        /// 贸然解锁会破坏加载流程。真正的残留锁场景里玩家一定在正常移动，状态必然有值。
        /// </summary>
        private static void TryClearStuckInventoryLock(Player player)
        {
            try
            {
                var controller = player.InventoryController;
                if (controller == null || !controller.Locked)
                {
                    _lastLockReport = EPlayerState.None;
                    return;
                }

                var movementContext = player.MovementContext;
                var currentState = movementContext?.CurrentState;
                if (currentState == null)
                {
                    // 状态还没就绪，别动
                    return;
                }

                var stateName = currentState.Name;

                // 状态确实要求锁 → 正常，别动它。
                // 注意：MovementContext.StateLocksInventory 是【只写】属性（没有 getter），
                // 所以这里只能用状态名判定 —— 那三个状态的 Enter/Exit/Spawned 才是它唯一的写入方。
                if (IsInventoryLockState(stateName))
                {
                    _lastLockReport = EPlayerState.None;
                    return;
                }

                controller.Locked = false;
                _lockRepairCount++;

                if (_lastLockReport != stateName)
                {
                    _lastLockReport = stateName;
                    FikaGlobals.LogWarning($"[InventoryLockFix] detected and cleared a leftover inventory lock " +
                                           $"(state={stateName}, cleared=#{_lockRepairCount}) — " +
                                           $"this is what causes \"equip fails / hands stuck\"，已自动恢复。");
                }
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[InventoryLockFix] failed: {exception.Message}");
            }
        }

        /// <summary>这三个状态会合法地锁住物品栏。</summary>
        private static bool IsInventoryLockState(EPlayerState state)
        {
            switch (state)
            {
                case EPlayerState.Approach:
                case EPlayerState.DoorInteraction:
                case EPlayerState.Stationary:
                    return true;
                default:
                    return false;
            }
        }
    }
}
