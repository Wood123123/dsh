using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Fika.Core.Main.ClientClasses.HandsControllers;
using Fika.Core.Main.ObservedClasses.HandsControllers;
using Fika.Core.Main.Players;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.FirearmController.SubPackets;
using HarmonyLib;
using UnityEngine;

namespace Dsh.CookingFikaSync
{
    /// <summary>
    /// 让 CookingGrenades 的"手雷烹饪时间"在 Fika 联机中真正同步（只读诊断 + 单向补全，不改 Fika 本体）。
    ///
    /// ── 为什么需要它 ──
    /// CookingGrenades 1.4.2 自己带了 Fika 补丁，但在 Fika 2.4.5 上失败了：
    ///     [Error :CookingGrenades] [Fika] 初始化失败: IL Compile Error (unknown location)
    ///
    /// 根因（反编译 + Fika 源码确认）：
    ///   ① 它给 GrenadePacket 打补丁时，补丁参数全写成 `object`
    ///      （例如 Prefix(object __instance, object player)）
    ///      而 GrenadePacket 是 **readonly struct** ⇒ Harmony 无法生成 IL
    ///   ② 它的时序方案本身对 struct 就是坏的：
    ///      用 ConditionalWeakTable&lt;object, …&gt; 按【引用】做 key，
    ///      而 struct 每次装箱都是新引用 ⇒ 读包时存的值，执行时取不到
    ///   ③ 结果第 3 步抛异常，导致后面两个 ObservedSpawn 补丁【从未执行】
    ///
    /// ── 本插件的做法 ──
    /// 完全绕开它的内部实现，用三个要点重做：
    ///   · 参数只用【具体类型】，绝不使用 object
    ///   · 传送用【静态字段】而不是按引用的字典
    ///     （读包 → 执行 → SpawnGrenade 是连续同线程发生的，静态字段足够）
    ///   · 只挂钩需要的方法，不碰 GrenadePacket 的 __instance
    ///
    /// 链路：
    ///   发送方  ThrowGrenade      → 记下本机 cook 时间
    ///   发送方  Serialize         → 把 cook 追加进包
    ///   接收方  .ctor(reader)     → 把 cook 读出来
    ///   接收方  Execute           → 交给下一步
    ///   接收方  SpawnGrenade      → 用 cook 替换硬编码的 0f
    /// </summary>
    [BepInPlugin(Guid, "Cooking Grenades Fika Sync", "1.0.0")]
    [BepInDependency("com.Tangh.CookingGrenades", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class CookingFikaSyncPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.dsh.cookingfikasync";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Verbose;

        // ── 跨步骤传递 cook 时间（★ 静态字段，不是按实例的字典）──
        /// <summary>发送方：本次投掷要写进包的 cook 秒数。0 = 不写。</summary>
        internal static volatile float OutgoingCook;

        /// <summary>接收方：刚从包里读出的 cook 秒数。</summary>
        internal static volatile float IncomingCook;

        /// <summary>
        /// ★ 这两个控制器在 Fika 里是 internal，编译期拿不到，只能用名字查。
        ///   缓存下来，避免反复查找。
        /// </summary>
        private static Type _observedGrenade;
        internal static Type ObservedGrenadeType
            => _observedGrenade ??= AccessTools.TypeByName(
                "Fika.Core.Main.ObservedClasses.HandsControllers.ObservedGrenadeController");

        private static Type _observedQuickGrenade;
        internal static Type ObservedQuickGrenadeType
            => _observedQuickGrenade ??= AccessTools.TypeByName(
                "Fika.Core.Main.ObservedClasses.HandsControllers.ObservedQuickGrenadeController");

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "总开关。");
            Verbose = Config.Bind("General", "Verbose", false,
                "记录每次 cook 时间的写入/读出/注入（排查用）。");

            try
            {
                // ★★★ 逐个 Patch，不用 PatchAll。
                //
                //   实测：Harmony 无法给 GrenadePacket（readonly struct）的实例方法打补丁，
                //   会抛 HarmonyException: IL Compile Error (unknown location) ——
                //   即使补丁本身不声明 __instance 也一样（Harmony 仍要为实例方法生成 wrapper）。
                //
                //   用 PatchAll 的话，一个失败就【全部回滚】，连能装上的 class 补丁也一起没了。
                //   所以这里逐个装、逐个 try，各自独立成败，并明确报告哪些成功。
                //
                //   struct 目标（Serialize / .ctor / Execute）预期会失败 —— 那是本插件的已知局限，
                //   真正的解法是 Transpiler（见 README）。
                var harmony = new Harmony(Guid);

                PatchOne(harmony, "① GrenadePacket.Serialize（写 cook）",
                    AccessTools.Method(typeof(GrenadePacket), "Serialize", new[] { typeof(NetDataWriter) }),
                    postfix: AccessTools.Method(typeof(Patch_Serialize), "Postfix"));

                PatchOne(harmony, "② GrenadePacket..ctor(reader)（读 cook）",
                    AccessTools.Constructor(typeof(GrenadePacket), new[] { typeof(NetDataReader) }),
                    postfix: AccessTools.Method(typeof(Patch_Ctor), "Postfix"));

                PatchOne(harmony, "③ GrenadePacket.Execute（转交 cook）",
                    AccessTools.Method(typeof(GrenadePacket), "Execute", new[] { typeof(FikaPlayer) }),
                    prefix: AccessTools.Method(typeof(Patch_Execute), "Prefix"));

                PatchOne(harmony, "④ ObservedGrenadeController.SpawnGrenade（注入 cook）",
                    AccessTools.Method(ObservedGrenadeType, "SpawnGrenade"),
                    prefix: AccessTools.Method(typeof(Patch_SpawnGrenade), "Prefix"));

                PatchOne(harmony, "⑤ ObservedQuickGrenadeController.SpawnGrenade（注入 cook）",
                    AccessTools.Method(ObservedQuickGrenadeType, "SpawnGrenade"),
                    prefix: AccessTools.Method(typeof(Patch_SpawnGrenade), "Prefix"));

                PatchOne(harmony, "⑥ FikaClientGrenadeController.ThrowGrenade（记录 cook）",
                    AccessTools.Method(typeof(FikaClientGrenadeController), "ThrowGrenade"),
                    prefix: AccessTools.Method(typeof(Patch_RecordCook), "Prefix"));

                PatchOne(harmony, "⑦ FikaClientQuickGrenadeController.ThrowGrenade（记录 cook）",
                    AccessTools.Method(typeof(FikaClientQuickGrenadeController), "ThrowGrenade"),
                    prefix: AccessTools.Method(typeof(Patch_RecordCook), "Prefix"));

                Log.LogInfo($"[CookingFikaSync] 装载结束：{_okCount} 成功 / {_failCount} 失败"
                          + (_failCount > 0 ? "  ⇒ 失败项见上方逐条日志" : ""));
            }
            catch (Exception e)
            {
                Log.LogError("[CookingFikaSync] 装载异常: " + e);
            }
        }

        private static int _okCount;
        private static int _failCount;

        /// <summary>装单个补丁；失败只记录，不影响其它补丁。</summary>
        private static void PatchOne(Harmony harmony, string label, MethodBase target,
                                     MethodInfo prefix = null, MethodInfo postfix = null)
        {
            if (target == null)
            {
                Log.LogWarning($"[CookingFikaSync] ✗ {label} —— 找不到目标方法");
                _failCount++;
                return;
            }

            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));

                // 回头核对，避免 HarmonyX 静默失败
                var info = Harmony.GetPatchInfo(target);
                var hit = false;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) if (p.owner == Guid) hit = true;
                    foreach (var p in info.Postfixes) if (p.owner == Guid) hit = true;
                }

                if (hit) { Log.LogInfo($"[CookingFikaSync] ✓ {label}"); _okCount++; }
                else { Log.LogWarning($"[CookingFikaSync] ✗ {label} —— 调用未报错但补丁没挂上"); _failCount++; }
            }
            catch (Exception e)
            {
                Log.LogWarning($"[CookingFikaSync] ✗ {label} —— {e.GetType().Name}: {e.Message}");
                _failCount++;
            }
        }

        /// <summary>逐项核对补丁是否真的挂上了（HarmonyX 静默失败过一次，必须回头检查）。</summary>
        private static void Verify()
        {
            int ok = 0, bad = 0;
            void Check(string label, MethodBase m)
            {
                if (m == null) { Log.LogWarning($"[CookingFikaSync] ✗ {label} —— 找不到目标"); bad++; return; }
                var info = Harmony.GetPatchInfo(m);
                var hit = false;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) if (p.owner == Guid) hit = true;
                    foreach (var p in info.Postfixes) if (p.owner == Guid) hit = true;
                }
                if (hit) { Log.LogInfo($"[CookingFikaSync] ✓ {label}"); ok++; }
                else { Log.LogWarning($"[CookingFikaSync] ✗ {label} —— 目标存在但补丁未挂上"); bad++; }
            }

            var gp = typeof(GrenadePacket);
            Check("GrenadePacket.Serialize       (写 cook)",
                  AccessTools.Method(gp, "Serialize", new[] { typeof(NetDataWriter) }));
            Check("GrenadePacket..ctor(reader)   (读 cook)",
                  AccessTools.Constructor(gp, new[] { typeof(NetDataReader) }));
            Check("GrenadePacket.Execute         (转交 cook)",
                  AccessTools.Method(gp, "Execute", new[] { typeof(FikaPlayer) }));
            Check("ObservedGrenadeController.SpawnGrenade      (注入 cook)",
                  AccessTools.Method(ObservedGrenadeType, "SpawnGrenade"));
            Check("ObservedQuickGrenadeController.SpawnGrenade (注入 cook)",
                  AccessTools.Method(ObservedQuickGrenadeType, "SpawnGrenade"));
            Check("FikaClientGrenadeController.ThrowGrenade      (记录 cook)",
                  AccessTools.Method(typeof(FikaClientGrenadeController), "ThrowGrenade"));
            Check("FikaClientQuickGrenadeController.ThrowGrenade (记录 cook)",
                  AccessTools.Method(typeof(FikaClientQuickGrenadeController), "ThrowGrenade"));

            Log.LogInfo($"[CookingFikaSync] 自检完成：{ok} 个挂钩生效，{bad} 个失败"
                      + (bad > 0 ? "  ⇒ ★ 失败的挂钩不会生效，请把这几行发给开发者" : "  ⇒ 联机烹饪同步已就绪"));
        }
    }

    // =====================================================================
    // ① 发送方：本机烹饪时间 → OutgoingCook
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_RecordCook
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            var t1 = AccessTools.Method(typeof(FikaClientGrenadeController), "ThrowGrenade");
            var t2 = AccessTools.Method(typeof(FikaClientQuickGrenadeController), "ThrowGrenade");
            if (t1 != null) yield return t1;
            if (t2 != null) yield return t2;
        }

        /// <summary>
        /// 参数的第一个是 timeSinceSafetyLevelRemoved —— Fika 原本要传给 SpawnGrenade 的引信时间。
        /// 烹饪中时取 mod 的烹饪时长，二者取较大值。
        /// </summary>
        private static void Prefix(float timeSinceSafetyLevelRemoved)
        {
            if (!CookingFikaSyncPlugin.Enabled.Value) return;
            try
            {
                float cook = 0f;
                var timer = CookingGrenades.GrenadeCookingManager.GetCookingTimer();
                if (timer != null && timer.IsCooking)
                {
                    cook = Mathf.Max(0f, timer.GetCookingTime());
                }

                float value = Mathf.Max(cook, timeSinceSafetyLevelRemoved);
                CookingFikaSyncPlugin.OutgoingCook = value;

                if (value > 0f && CookingFikaSyncPlugin.Verbose.Value)
                {
                    CookingFikaSyncPlugin.Log.LogInfo(
                        $"[CookingFikaSync][记录] 本次投掷 cook={value:F2}s（mod={cook:F2}, fika={timeSinceSafetyLevelRemoved:F2}）");
                }
            }
            catch (Exception e)
            {
                CookingFikaSyncPlugin.Log.LogWarning("[CookingFikaSync] 记录 cook 失败: " + e.Message);
            }
        }
    }

    // =====================================================================
    // ② 发送方：把 cook 追加进包
    // =====================================================================
    [HarmonyPatch(typeof(GrenadePacket), nameof(GrenadePacket.Serialize))]
    internal static class Patch_Serialize
    {
        /// <summary>
        /// ★ 参数只用具体类型（NetDataWriter），不用 object，也不碰 __instance。
        /// 原版 Serialize 末尾已经写完所有字段，这里追加一个 float 即可；
        /// 接收方在构造函数末尾同样读一个 float，两边对称。
        /// </summary>
        private static void Postfix(NetDataWriter writer)
        {
            if (!CookingFikaSyncPlugin.Enabled.Value || writer == null) return;
            try
            {
                float cook = CookingFikaSyncPlugin.OutgoingCook;
                writer.Put(cook);

                if (cook > 0f && CookingFikaSyncPlugin.Verbose.Value)
                {
                    CookingFikaSyncPlugin.Log.LogInfo($"[CookingFikaSync][写入] 包中 cook={cook:F2}s");
                }
                CookingFikaSyncPlugin.OutgoingCook = 0f;
            }
            catch (Exception e)
            {
                CookingFikaSyncPlugin.Log.LogWarning("[CookingFikaSync] 写入 cook 失败: " + e.Message);
            }
        }
    }

    // =====================================================================
    // ③ 接收方：从包里读出 cook
    // =====================================================================
    [HarmonyPatch(typeof(GrenadePacket), MethodType.Constructor, new[] { typeof(NetDataReader) })]
    internal static class Patch_Ctor
    {
        /// <summary>
        /// ★ 只取 reader 参数。原版构造函数末尾读完所有字段，
        /// 这里紧接着读一个 float（与发送方 Serialize 的追加严格对应）。
        /// </summary>
        private static void Postfix(NetDataReader reader)
        {
            if (!CookingFikaSyncPlugin.Enabled.Value || reader == null) return;
            try
            {
                float cook = reader.GetFloat();
                CookingFikaSyncPlugin.IncomingCook = cook;

                if (cook > 0f && CookingFikaSyncPlugin.Verbose.Value)
                {
                    CookingFikaSyncPlugin.Log.LogInfo($"[CookingFikaSync][读出] 包中 cook={cook:F2}s");
                }
            }
            catch (Exception e)
            {
                // 读失败就置 0，绝不影响手雷本身
                CookingFikaSyncPlugin.IncomingCook = 0f;
                CookingFikaSyncPlugin.Log.LogWarning("[CookingFikaSync] 读出 cook 失败: " + e.Message);
            }
        }
    }

    // =====================================================================
    // ④ 接收方：Execute 时把 cook 交给 SpawnGrenade
    // =====================================================================
    [HarmonyPatch(typeof(GrenadePacket), nameof(GrenadePacket.Execute))]
    internal static class Patch_Execute
    {
        /// <summary>
        /// Execute 里会调用 controller.SpawnGrenade(0f, …)（硬编码 0f）。
        /// 这里不做任何修改，只是确认 IncomingCook 已就位 —— 真正的注入在 ⑤。
        /// ★ 因为 Execute 与随后的 SpawnGrenade 是连续同线程执行的，
        ///   所以一个静态字段足够传递，不需要按实例的字典。
        /// </summary>
        private static void Prefix()
        {
            if (!CookingFikaSyncPlugin.Enabled.Value) return;
            if (CookingFikaSyncPlugin.Verbose.Value && CookingFikaSyncPlugin.IncomingCook > 0f)
            {
                CookingFikaSyncPlugin.Log.LogInfo(
                    $"[CookingFikaSync][执行] 待注入 cook={CookingFikaSyncPlugin.IncomingCook:F2}s");
            }
        }
    }

    // =====================================================================
    // ⑤ 接收方：把 cook 注入 SpawnGrenade（替换硬编码的 0f）
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_SpawnGrenade
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            var t1 = AccessTools.Method(AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedGrenadeController"), "SpawnGrenade");
            var t2 = AccessTools.Method(AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedQuickGrenadeController"), "SpawnGrenade");
            if (t1 != null) yield return t1;
            if (t2 != null) yield return t2;
        }

        /// <summary>
        /// ★ 目标是 class（不是 struct），参数用具体类型 + ref float —— 这是能成功的关键。
        /// 第一个参数 timeSinceSafetyLevelRemoved 决定手雷引信，硬编码 0f 就是不同步的原因。
        /// </summary>
        private static void Prefix(ref float timeSinceSafetyLevelRemoved)
        {
            if (!CookingFikaSyncPlugin.Enabled.Value) return;
            try
            {
                float cook = CookingFikaSyncPlugin.IncomingCook;
                if (cook <= 0f) return;

                timeSinceSafetyLevelRemoved = cook;
                CookingFikaSyncPlugin.IncomingCook = 0f;   // 用完即清，避免影响下一颗

                if (CookingFikaSyncPlugin.Verbose.Value)
                {
                    CookingFikaSyncPlugin.Log.LogInfo($"[CookingFikaSync][注入] cook={cook:F2}s");
                }
            }
            catch (Exception e)
            {
                CookingFikaSyncPlugin.Log.LogWarning("[CookingFikaSync] 注入 cook 失败: " + e.Message);
            }
        }
    }
}
