using System;
using EFT;
using EFT.InventoryLogic;

namespace Fika.Core.Main.Utils
{
    /// <summary>
    /// Releases the native presentation state a ground loot pickup can leave behind.
    ///
    /// EFT 的拾取走 <c>EFT.PickUpState</c>（<see cref="EPlayerState.Pickup"/>）+ 动画交互队列。
    /// 当驱动它的库存操作**始终没有回报终态**时（联机下重复投递 / 被拒绝 / 回调丢失都会这样），
    /// 状态机就永久停在 PickUpState 里，玩家会保留它的限制：
    ///
    ///   · 视角不能旋转
    ///   · 不能移动
    ///   · 不能蹲下
    ///   · <c>MovementContext.CanInteract</c> 一直返回 "Can't interact in Pickup"
    ///     ⇒ 交互提示不再出现
    ///   · 库存和开火反而还能用（所以看起来"人能动"但"捡不起东西"）
    ///
    /// 临时趴下能让状态改变从而清掉它，但排队的 TakeItem 交互之后会拒绝本局内所有后续拾取。
    ///
    /// 本类只恢复**表现层**状态，绝不触碰物品、地址或操作 ——
    /// 所以它不会导致战利品被复制或回滚。
    ///
    /// 来源：Fika 3.0.1（SPT 5.0）的 <c>NativeInteractionRelease</c>，
    /// 按 EFT 0.16.9 / SPT 4.1.6 的实际签名移植（Mono 版）。
    /// </summary>
    internal static class NativeInteractionRelease
    {
        /// <summary>这些状态一旦"超时"就判定为卡死。</summary>
        private static bool IsStuckState(EPlayerState state)
        {
            // ★ 只认真正会「卡死整局」的两个状态。
            // 已移除 DoorInteraction / BreachDoor / Plant：
            //   · Plant 是玩家主动进行的长操作（走位找点 + 按住 + 等动画），
            //     实测 20 秒阈值会把正常放置打断，日志里出现过 4 次 (state=Plant) 强制取消。
            //   · DoorInteraction / BreachDoor 的动画本来就有正常的长时长。
            // 保留 Pickup / Loot：这两个才是「完成回调丢失 ⇒ 永久锁死整局捡不起东西」的场景。
            return state == EPlayerState.Pickup || state == EPlayerState.Loot;
        }

        /// <summary>
        /// 只有当玩家**真的**卡在拾取/交互状态里才动作。
        /// 一次健康的操作失败不能把合法的换弹 / 开门 / 检视 / 投掷动画打断。
        /// </summary>
        internal static bool IsStuck(Player player, out string detail)
        {
            detail = null;

            var movementContext = player?.MovementContext;
            if (movementContext == null)
            {
                return false;
            }

            var currentState = movementContext.CurrentState;
            var stuckState = currentState != null && IsStuckState(currentState.Name);

            var blockedInteractions = false;
            try
            {
                // Error 是 class：非 null 表示"当前不能交互"，ToString() 就是那句 "Can't interact in Pickup"
                blockedInteractions = movementContext.CanInteract != null;
            }
            catch
            {
                // 已销毁的 player 或抛异常的 native 访问器不能把每帧钩子带崩
            }

            var animatorStuck = false;
            try
            {
                var animated = movementContext.PlayerAnimator?.AnimatedInteractions;
                animatorStuck = animated != null && (!animated.CanInteract || animated.IsScheduleDenied);
            }
            catch
            {
            }

            // ★ 只认 stuckState。blockedInteractions 已从判定中剔除 ——
            // MovementContext.CanInteract 在 Sprint / Run / Idle 等几乎所有非交互状态下
            // 都返回非 null（"当前状态不能交互"），拿它当独立判据会把正常跑步判成卡死。
            // 实测误伤 62 次 (state=Sprint) + 2 次 (state=Run)。
            // animatorStuck 同样降级为仅用于日志描述，不参与判定。
            if (!stuckState)
            {
                return false;
            }

            detail = $"state={currentState?.Name}, blocked={blockedInteractions}, animatorStuck={animatorStuck}";
            return true;
        }

        /// <summary>
        /// 释放卡住的交互状态。每一步都单独守：任意一步失败都不影响其余步骤。
        /// </summary>
        internal static void Release(Player player, string reason)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                var movementContext = player.MovementContext;
                if (movementContext == null)
                {
                    return;
                }

                var currentState = movementContext.CurrentState;

                FikaGlobals.LogWarning($"[NativeInteractionRelease/{reason}] releasing the interaction state " +
                                       $"(state={currentState?.Name})");

                Try(reason, "ForceStopInteractions", () =>
                    movementContext.PlayerAnimator?.AnimatedInteractions?.ForceStopInteractions());

                Try(reason, "StopAnyInteractions", () =>
                    movementContext.StopAnyInteractions());

                Try(reason, "state cancel + force Idle", () =>
                {
                    var state = movementContext.CurrentState;
                    if (state == null || !IsStuckState(state.Name))
                    {
                        return;
                    }

                    // Cancel() 只在它自己的回调里才会真正离开状态；
                    // 当那个回调丢失时，玩家会永久锁死（不能转向、不能移动、没有交互提示）。
                    // 所以直接把状态机推回 Idle —— 和"换个姿势"做的是同一件事。
                    (state as MovementState)?.Cancel();

                    var states = movementContext.States;
                    if (states == null)
                    {
                        return;
                    }

                    if (movementContext.CurrentState?.Name == EPlayerState.Idle)
                    {
                        return;
                    }

                    if (!states.TryGetValue(EPlayerState.Idle, out var idleState) || idleState == null)
                    {
                        return;
                    }

                    var stuckName = movementContext.CurrentState?.Name;
                    Try(reason, "state exit", () => state.Exit(false));
                    movementContext.ProcessStateEnter(idleState);

                    FikaGlobals.LogWarning($"[NativeInteractionRelease/{reason}] forced the movement state from {stuckName} to Idle");
                });

                Try(reason, "SetInteractInHands", () =>
                    movementContext.SetInteractInHands(EInteraction.None));

                Try(reason, "PlayerAnimatorSetInteraction", () =>
                    movementContext.PlayerAnimatorSetInteraction(false, false));

                Try(reason, "ToggleBlockInputPlayerRotation", () =>
                    movementContext.ToggleBlockInputPlayerRotation(false));
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[NativeInteractionRelease/{reason}] movement context release failed: {exception.Message}");
            }

            Try(reason, "interaction refresh", () =>
            {
                player.InteractableObject = null;
                player.ForceInteractionsChanged();
            });
        }

        private static void Try(string reason, string step, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                FikaGlobals.LogWarning($"[NativeInteractionRelease/{reason}] {step} failed: {exception.Message}");
            }
        }
    }
}
