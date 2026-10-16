using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TacticalSprint.Fika
{
    /// <summary>
    /// 挂在 observed 玩家上，把战术冲刺的"举枪动画"写进第三方视角的动画器。
    ///
    /// 原理（反编译确认）：
    ///   ObservedPlayerHandsController::method_2 里会执行
    ///     _playerAnimator.SetFloat(WEAPON_SIZE_MODIFIER_PARAM_HASH, WeaponSizeModifier(item))
    ///   战术冲刺 = 把这个值临时改成 2f（原 mod 的本地做法）。
    ///
    /// 关键：退出时**不能用算出来的值**还原（observed hands 的属性叫 ItemInHands 而不是 Item，
    /// 取不到就会写成 1，动画变"拿刀跑"）。改为进入时用 IAnimator.GetFloat 把原值存下来，
    /// 退出时原样写回。
    /// </summary>
    public class ObservedPlayerTacticalSprintController : MonoBehaviour
    {
        private Player player;
        private bool active;
        private object animator;
        private MethodInfo setFloat;
        private MethodInfo getFloat;

        private bool hasSaved;
        private float savedSize;

        private void Awake()
        {
            player = GetComponent<Player>();
        }

        public void SetActive(bool value)
        {
            if (active == value)
            {
                return;
            }
            active = value;

            ResolveAnimator();
            if (animator == null)
            {
                return;
            }

            if (value)
            {
                SaveCurrent();
                Plugin.Log.LogInfo((object)("[TacticalSprint.Fika] 进入战术冲刺，保存原值 "
                    + (hasSaved ? savedSize.ToString("F2") : "取不到(动画器未就绪)，退出时用武器尺寸兜底")));
            }
            else
            {
                float v = hasSaved ? savedSize : GetNativeSize();
                Write(v);
                Plugin.Log.LogInfo((object)("[TacticalSprint.Fika] 退出战术冲刺，还原 " + v.ToString("F2")
                    + (hasSaved ? "（存的原值）" : "（武器尺寸兜底）")));
            }
        }

        private void LateUpdate()
        {
            // 只在激活期间每帧覆写，防止游戏自身写入覆盖
            if (!active)
            {
                return;
            }
            try
            {
                ResolveAnimator();
                Write(2f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 写动画参数失败: " + e.Message));
            }
        }

        /// <summary>进入战术冲刺前，把动画器里当前的 WEAPON_SIZE_MODIFIER 原值存下来。</summary>
        private void SaveCurrent()
        {
            hasSaved = false;
            if (getFloat == null)
            {
                return;
            }
            try
            {
                object r = getFloat.Invoke(animator, new object[] { PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH });
                // 只接受合理值：动画器未初始化时 GetFloat 会返回 0，
                // 把 0 存下来会在退出时写出"拿刀跑"的姿势
                if (r is float f && f >= 1f)
                {
                    savedSize = f;
                    hasSaved = true;
                }
            }
            catch
            {
            }
        }

        private void Write(float size)
        {
            if (animator == null || setFloat == null)
            {
                return;
            }
            object[] args = new object[] { PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH, size };
            setFloat.Invoke(animator, args);
        }

        private void ResolveAnimator()
        {
            try
            {
                if (animator != null)
                {
                    return;
                }
                if ((Object)(object)player == (Object)null)
                {
                    return;
                }

                // 首选：observed hands controller 上的 _playerAnimator
                //（ObservedPlayerHandsController::method_2 用的就是它）
                object hands = player.HandsController;
                if (hands != null)
                {
                    FieldInfo f = AccessTools.Field(hands.GetType(), "_playerAnimator");
                    if (f != null)
                    {
                        object a = f.GetValue(hands);
                        if (a != null && SetAnimator(a))
                        {
                            return;
                        }
                    }
                }

                // 兜底：Player.BodyAnimatorCommon
                MethodInfo getter = AccessTools.PropertyGetter(typeof(Player), "BodyAnimatorCommon");
                if (getter != null)
                {
                    object a = getter.Invoke(player, null);
                    if (a != null)
                    {
                        SetAnimator(a);
                    }
                }
            }
            catch
            {
            }
        }

        private bool SetAnimator(object a)
        {
            MethodInfo s = AccessTools.Method(a.GetType(), "SetFloat", new Type[] { typeof(int), typeof(float) });
            if (s == null)
            {
                return false;
            }
            animator = a;
            setFloat = s;
            getFloat = AccessTools.Method(a.GetType(), "GetFloat", new Type[] { typeof(int) });
            return true;
        }

        /// <summary>
        /// 兜底恢复值（只在存原值失败时用）。
        /// observed hands 的属性名是 ItemInHands（不是 Item），失败就退回 3f —— 绝不返回 1，
        /// 那会让远端动画变成"拿刀跑"。
        /// </summary>
        private float GetNativeSize()
        {
            try
            {
                if ((Object)(object)player == (Object)null)
                {
                    return 3f;
                }
                object hands = player.HandsController;
                if (hands == null)
                {
                    return 3f;
                }

                object item = null;
                foreach (string name in new string[] { "ItemInHands", "Item", "Weapon" })
                {
                    PropertyInfo p = AccessTools.Property(hands.GetType(), name);
                    if (p != null)
                    {
                        item = p.GetValue(hands, null);
                        if (item != null)
                        {
                            break;
                        }
                    }
                }

                Weapon weapon = item as Weapon;
                if (weapon != null)
                {
                    return Mathf.Max(3f, weapon.CalculateCellSize().X);
                }
            }
            catch
            {
            }
            return 3f;
        }
    }
}
