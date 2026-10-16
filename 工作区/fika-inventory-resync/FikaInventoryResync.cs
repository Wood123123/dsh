using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.ObservedClasses.HandsControllers;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Communication;
using Fika.Core.Networking.Packets.Generic;
using HarmonyLib;
using UnityEngine;

namespace FikaInventoryResync
{
    /// <summary>
    /// Fixes two gaps in Fika 2.4.4's inventory-id ("CurrentId") resync flow.
    ///
    /// Gap 1 (client side): FikaClient.HandleInventoryPacket logs and returns when it cannot
    /// replay another player's operation descriptor, without asking anyone to correct the mirror.
    /// Patch "client request" fires at the actual failure point
    /// (ObservedInventoryController.CreateOperationFromDescriptor) and sends an empty
    /// ResyncInventoryIdPacket(netId) with broadcast=true. The host relays it, the owner of that
    /// netId answers with its own authoritative CurrentId, the host relays that back, and every
    /// mirror of that player gets corrected.
    ///
    /// Gap 2 (both sides): OnResyncInventoryIdPacketReceived calls SetNewID(packet.MongoId)
    /// unconditionally. An empty MongoId means "tell me your id" (that is what the host sends in
    /// its failure branches), so writing it into a mirror would set _currentId to default and make
    /// the drift worse. The two guard patches stop that, while deliberately still letting the
    /// client answer a request aimed at its own player.
    /// </summary>
    [BepInPlugin(Guid, "Fika Inventory Resync", "1.3.2")]
    [BepInDependency("com.fika.core")]
    public class FikaInventoryResyncPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.local.fikainventoryresync";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> CooldownSeconds;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<bool> FixStuckLock;
        internal static ConfigEntry<float> LockCheckInterval;

        /// <summary>看门狗上一次检查的时间（realtimeSinceStartup）。</summary>
        private float _nextLockCheck;

        /// <summary>已经报告过的残留锁状态，避免每帧刷日志。</summary>
        private static string _lastLockReport = "";

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch for all three patches.");
            CooldownSeconds = Config.Bind("General", "RequestCooldownSeconds", 1.0f,
                "Minimum seconds between two resync requests for the same netId. Prevents flooding when a mirror is badly drifted.");
            Verbose = Config.Bind("General", "Verbose", true,
                "Log every resync request that is actually sent (cooldown-suppressed ones are counted, not logged).");

            // ---------------------------------------------------------------
            // ★ 残留物品栏锁看门狗
            //
            //   反编译实证的锁机制：
            //     MovementContext::set_StateLocksInventory(bool) → InventoryController.Locked = value
            //     只有三个移动状态会设它：ApproachState / DoorInteractState / StationaryPlayerState
            //     Locked == true 时 ItemController::CheckItemAction 抛 PlayerIsBusyError：
            //       "Cannot apply {item} because {target} is currently being modified"
            //
            //   如果那三个状态的 Exit 没被调用（交互被打断），锁就一直挂着，
            //   之后该玩家的一切物品操作都失败 —— 表现就是"装备手雷失败、手部卡在空手"。
            //   而且这个锁【不会】被 resync 修掉：resync 换的是 inventory 数据，
            //   锁是 InventoryController 上的运行时标志。
            // ---------------------------------------------------------------
            FixStuckLock = Config.Bind("InventoryLock", "FixStuckInventoryLock", true,
                "Detect a leftover inventory lock (InventoryController.Locked == true while the player is NOT in "
              + "Approach/DoorInteract/Stationary state) and clear it. Without this, one interrupted interaction "
              + "leaves the whole inventory unusable until the raid ends.");
            LockCheckInterval = Config.Bind("InventoryLock", "CheckIntervalSeconds", 1.0f,
                "How often to run the leftover-lock check.");

            Log.LogInfo("FikaInventoryResync v1.3.2 loaded. Enabled=" + Enabled.Value
                        + " Cooldown=" + CooldownSeconds.Value.ToString("0.##") + "s"
                        + " FixStuckLock=" + FixStuckLock.Value);

            try
            {
                ApplyPatches();
            }
            catch (Exception ex)
            {
                Log.LogError("ApplyPatches failed: " + ex);
            }
        }

        /// <summary>
        /// ★ 残留物品栏锁看门狗。
        ///
        /// 挂在插件的 Update 上而不是 UpdateLockedStatus 的 Postfix 上：
        /// 后者只在 ProcessStatus 变化或被显式赋值时调用，
        /// 锁已经卡住的情况下它根本不会再跑，看门狗也就永远不会被执行。
        /// </summary>
        private void Update()
        {
            if (!Enabled.Value || !FixStuckLock.Value) return;

            var now = Time.realtimeSinceStartup;
            if (now < _nextLockCheck) return;
            _nextLockCheck = now + Math.Max(0.2f, LockCheckInterval.Value);

            try
            {
                ClearStuckInventoryLock();
            }
            catch (Exception ex)
            {
                if (Verbose.Value) Log.LogWarning("lock watchdog: " + ex.Message);
            }
        }

        /// <summary>
        /// 锁物品栏的三个移动状态类型，缓存下来。
        ///
        /// ★ 不用 state.GetType().Name 做字符串比较：Name 每次都会分配一个新字符串，
        ///   每秒跑一次也白白制造 GC 压力。直接缓存 Type 对象做引用比较，零分配。
        /// </summary>
        private static Type[] _lockStateTypes;

        private static Type[] LockStateTypes
        {
            get
            {
                if (_lockStateTypes != null) return _lockStateTypes;

                var list = new List<Type>(3);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        if (t.Name == "ApproachState" ||
                            t.Name == "DoorInteractState" ||
                            t.Name == "StationaryPlayerState")
                        {
                            list.Add(t);
                        }
                    }
                    if (list.Count >= 3) break;   // 找齐就不用再扫其它程序集
                }

                _lockStateTypes = list.ToArray();
                return _lockStateTypes;
            }
        }

        /// <summary>当前移动状态是否"有资格"锁住物品栏。</summary>
        private static bool StateMayLockInventory(BaseMovementState state)
        {
            if (state == null) return false;

            // 用类型对象【引用比较】，不走 is-a：
            // StationaryPlayerState 继承 IdlePlayerState，用 is 判断会误伤同族其它状态。
            var t = state.GetType();
            var arr = LockStateTypes;
            for (var i = 0; i < arr.Length; i++)
            {
                if (ReferenceEquals(arr[i], t)) return true;
            }
            return false;
        }

        private static void ClearStuckInventoryLock()
        {
            var gw = Singleton<GameWorld>.Instance;
            if (gw == null) return;

            var player = gw.MainPlayer;
            if (player == null) return;

            var ctrl = player.InventoryController;
            if (ctrl == null || !ctrl.Locked) { _lastLockReport = ""; return; }

            var state = player.CurrentState;

            // ★ 状态还没就绪（战局加载中 / 正在卸载）→ 不动它。
            //   那种时刻 Locked 可能是引擎的正常中间态，贸然解锁会破坏加载流程。
            //   真正的残留锁场景里玩家一定在正常移动，state 必然有值。
            if (state == null) return;

            // 状态确实要求锁 → 这是正常的，别动它
            if (StateMayLockInventory(state)) { _lastLockReport = ""; return; }

            var stateName = state.GetType().Name;

            // ★ 锁在，但当前状态不该锁 → 残留锁，清掉
            ctrl.Locked = false;

            var key = stateName;
            if (key != _lastLockReport)
            {
                _lastLockReport = key;
                Log.LogWarning("[LockFix] ★ 检测到残留的物品栏锁并已解除"
                             + "（玩家=" + (string.IsNullOrEmpty(player.ProfileId) ? "?" : player.ProfileId)
                             + " 当前移动状态=" + stateName + "）"
                             + " —— 这是「装备失败 / 手部卡住」的直接原因，已自动恢复。");
            }
        }

        private static void ApplyPatches()
        {
            var harmony = new Harmony(Guid);

            // ---------------------------------------------------------------
            // Patch 1: client asks for a resync when it cannot replay an op.
            // ---------------------------------------------------------------
            var ctrlType = AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.ObservedInventoryController");
            if (ctrlType == null)
            {
                Log.LogError("Patch1 SKIPPED: ObservedInventoryController type not found.");
            }
            else
            {
                // The method is an explicit interface implementation, so its IL name is
                // "EFT.InventoryLogic.IOperationHandler.CreateOperationFromDescriptor" and it is
                // private/final/virtual -> look it up among declared methods, not by short name.
                MethodInfo target = null;
                foreach (var m in ctrlType.GetMethods(BindingFlags.Instance | BindingFlags.Public
                                                      | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.Name.EndsWith("CreateOperationFromDescriptor", StringComparison.Ordinal))
                    {
                        target = m;
                        break;
                    }
                }

                if (target == null)
                {
                    Log.LogError("Patch1 SKIPPED: CreateOperationFromDescriptor not found on " + ctrlType.FullName);
                }
                else
                {
                    try
                    {
                        _fikaPlayerRef = AccessTools.FieldRefAccess<ObservedInventoryController, FikaPlayer>("_fikaPlayer");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning("Patch1: _fikaPlayer FieldRef unavailable (" + ex.Message + "), falling back to reflection.");
                        _fikaPlayerField = AccessTools.Field(ctrlType, "_fikaPlayer");
                    }

                    harmony.Patch(target, postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(CreateOperationFromDescriptor_Postfix))));
                    Log.LogInfo("Patch1 OK: client resync request hooked on " + target.DeclaringType.FullName + "::" + target.Name);
                }
            }

            // ---------------------------------------------------------------
            // Patch 2a: host must never write an empty id into its own mirror.
            // ---------------------------------------------------------------
            var serverTarget = AccessTools.Method(typeof(FikaServer), "OnResyncInventoryIdPacketReceived");
            if (serverTarget == null)
            {
                Log.LogError("Patch2a SKIPPED: FikaServer.OnResyncInventoryIdPacketReceived not found.");
            }
            else
            {
                harmony.Patch(serverTarget, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(ServerResyncGuard_Prefix))));
                Log.LogInfo("Patch2a OK: host resync guard on FikaServer::OnResyncInventoryIdPacketReceived");
            }

            // ---------------------------------------------------------------
            // Patch 2b: client must not clobber someone's mirror with an empty id,
            //           but must still answer a request aimed at its own player.
            // ---------------------------------------------------------------
            var clientTarget = AccessTools.Method(typeof(FikaClient), "OnResyncInventoryIdPacketReceived");
            if (clientTarget == null)
            {
                Log.LogError("Patch2b SKIPPED: FikaClient.OnResyncInventoryIdPacketReceived not found.");
            }
            else
            {
                harmony.Patch(clientTarget, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(ClientResyncGuard_Prefix))));
                Log.LogInfo("Patch2b OK: client resync guard on FikaClient::OnResyncInventoryIdPacketReceived");
            }

            // ---------------------------------------------------------------
            // Patch 3 (THIS IS THE ROOT-CAUSE FIX) -- host only.
            //
            // FikaClient.SendGenericPacket writes the broadcast byte; FikaServer.SendGenericPacket
            // does NOT. FikaServer.OnNetworkReceive relays any packet whose broadcast byte is 1
            // straight to the other peers, and *then* dispatches it. For InventoryOperation that
            // dispatch reaches OnInventoryPacketReceived, which broadcasts the very same
            // descriptor a second time. Clients read no broadcast byte, so both copies have an
            // identical layout and each one is applied -> every inventory operation is replayed
            // TWICE on every non-owner client.
            //
            // Re-applying an already-applied operation is precisely what produces
            //   "item X is not located at <source>. It's at <dest>"   (already moved)
            //   "Not enough space in target"                          (cell already taken)
            //   "Could not find item owner with id: X"                (parent already changed)
            // and, for idempotent operations, silent item duplication.
            //
            // Suppressing the raw relay for InventoryOperation leaves exactly one delivery path
            // (the post-validation re-broadcast) and additionally stops clients from applying
            // operations the host REJECTED -- the failure branch never re-broadcasts.
            // ---------------------------------------------------------------
            var recvTarget = AccessTools.Method(typeof(FikaServer), "OnNetworkReceive");
            if (recvTarget == null)
            {
                Log.LogError("Patch3 SKIPPED: FikaServer.OnNetworkReceive not found.");
            }
            else
            {
                harmony.Patch(recvTarget, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(OnNetworkReceive_Transpiler))));
                Log.LogInfo("Patch3 OK: host inventory-operation de-duplication transpiler on FikaServer::OnNetworkReceive");
            }

            // ---------------------------------------------------------------
            // Patch 4 -- every peer. Restores the item-id counter for replayed splits.
            //
            // EFT has two IDatabaseIdGenerator implementations and they behave differently:
            //
            //   ItemController.get_NextId          (live path)
            //       var id = _currentId; _currentId = MongoID.op_Increment(_currentId); return id;
            //
            //   SplitOperationDescriptor.SplitIdGenerator.get_NextId   (descriptor-replay path)
            //       return _nextId;                    // never advances; RollBack() is a no-op
            //
            // ItemManipulator.Split -> ItemExtensions.CloneItem -> CloneItemInternal calls
            // NextId once for the item AND once for every contained item (StackSlot / Grid / Slot).
            // A descriptor replay therefore hands out the SAME id repeatedly while the original
            // run handed out CloneId, CloneId+1, CloneId+2 ...
            //
            // EFT's own guard then fires:
            //   "Cloned item ID desync. Expected ID: 6ac630c9147f66469b000059, real ID: 6ac630c9147f66469b000058"
            // (both of those lines are present in the user's logs, off by 1 and off by 6).
            //
            // Making the replay generator advance exactly like ItemController.NextId makes the
            // replaying side allocate the same id sequence as the original side.
            // ---------------------------------------------------------------
            var descType = AccessTools.TypeByName("EFT.SplitOperationDescriptor");
            var genType = descType == null
                ? null
                : descType.GetNestedType("SplitIdGenerator", BindingFlags.Public | BindingFlags.NonPublic);

            if (genType == null)
            {
                Log.LogError("Patch4 SKIPPED: EFT.SplitOperationDescriptor/SplitIdGenerator not found.");
            }
            else
            {
                _splitIdNextField = AccessTools.Field(genType, "_nextId");
                var getNextId = AccessTools.Method(genType, "get_NextId");

                if (getNextId == null || _splitIdNextField == null)
                {
                    Log.LogError("Patch4 SKIPPED: get_NextId/_nextId not found on " + genType.FullName);
                }
                else
                {
                    harmony.Patch(getNextId, prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(SplitIdGenerator_Prefix))));
                    Log.LogInfo("Patch4 OK: split replay id generator now advances (was returning a constant) on "
                                + genType.FullName + "::get_NextId");
                }
            }

            // ---------------------------------------------------------------
            // Patch 5 (DIAGNOSTIC) + Patch 6 (mitigation) -- observed weapons.
            //
            // Symptom in the field log:
            //   [PlayerTick]: [[Tadikas|0|...]] tick operation exception: NullReferenceException
            //     at ObservedFirearmController.IEventsConsumerOnShellEject () [0x00036]
            // repeated for every shell-eject animation event, forever.
            //
            // IL of IEventsConsumerOnShellEject:
            //   001C: ldfld  _weaponManager
            //   0037: callvirt Firearms::StartSpawnShell(...)   <-- 0x36 is this call
            // so _weaponManager is null.
            //
            // _weaponManager has EXACTLY ONE writer in the whole assembly:
            //   ObservedFirearmController.Start()  @0x001D
            //   0007: callvirt FirearmsAnimator::SetAiming(false)   <- throws here => nothing after runs
            //   001D: stfld _weaponManager
            // Therefore Start() either never ran or threw before 0x001D.
            //
            // Unity logs MonoBehaviour exceptions into the BSG application.log, NOT into BepInEx's
            // LogOutput.log -- which is why the real cause has been invisible so far.
            // Patch 5 surfaces it; Patch 6 stops the spam and the aborted animation-event chain.
            // ---------------------------------------------------------------
            var obsCtrlType = AccessTools.TypeByName("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedFirearmController");
            if (obsCtrlType == null)
            {
                Log.LogError("Patch5/6 SKIPPED: ObservedFirearmController not found.");
            }
            else
            {
                _weaponManagerField = AccessTools.Field(obsCtrlType, "_weaponManager");
                _objectInHandsAnimatorField = AccessTools.Field(obsCtrlType, "_objectInHandsAnimator")
                                             ?? AccessTools.Field(obsCtrlType.BaseType, "_objectInHandsAnimator");
                _weaponPrefabField = AccessTools.Field(obsCtrlType, "_weaponPrefab")
                                    ?? AccessTools.Field(obsCtrlType.BaseType, "_weaponPrefab");

                var startMi = AccessTools.Method(obsCtrlType, "Start");
                if (startMi == null)
                {
                    Log.LogError("Patch5 SKIPPED: ObservedFirearmController.Start not found.");
                }
                else
                {
                    // ---------------------------------------------------------------
                    // Patch 5 REMOVED in v1.3.1 -- deliberately not installed.
                    //
                    // It hooked ObservedFirearmController.Start() with a postfix + finalizer
                    // purely to surface a NullReferenceException that Unity writes to BSG's
                    // application.log instead of BepInEx's LogOutput.log.
                    //
                    // The finalizer returned null, i.e. it swallowed the exception before
                    // Unity's own MonoBehaviour lifecycle could act on it. That is a real
                    // behaviour change: the object was left half-initialised (_weaponManager
                    // stays null because the stfld sits after the throwing call) while Unity
                    // no longer saw the failure at all.
                    //
                    // On the headless the process started dying hard right after
                    // "Headless client is ready" -- exactly when the first observed firearm
                    // controllers initialise -- leaving no managed exception and no dump.
                    //
                    // Removed so Start() behaves exactly as it does with this plugin absent.
                    // Patch 6 is kept: it only skips a method whose _weaponManager is
                    // provably null, and touches nothing else.
                    // ---------------------------------------------------------------
                }

                var shellMi = AccessTools.Method(obsCtrlType, "IEventsConsumerOnShellEject");
                if (shellMi == null)
                {
                    Log.LogError("Patch6 SKIPPED: IEventsConsumerOnShellEject not found.");
                }
                else
                {
                    harmony.Patch(shellMi, prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(ShellEject_Prefix))));
                    Log.LogInfo("Patch6 OK: shell-eject is skipped when _weaponManager is null "
                                + "(stops the NullReferenceException spam and the aborted animation-event chain).");
                }
            }
        }

        // ===================================================================
        // Patch 5/6 bodies
        // ===================================================================

        private static FieldInfo _weaponManagerField;
        private static FieldInfo _objectInHandsAnimatorField;
        private static FieldInfo _weaponPrefabField;

        private static readonly HashSet<int> _nullManagerReported = new HashSet<int>();
        private static int _shellEjectSkipped;

        private static Exception FirearmStart_Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                // Swallow so Unity's lifecycle continues, but make the cause visible.
                Log.LogError("[diag] ObservedFirearmController.Start() THREW -- _weaponManager will stay null and "
                             + "every shell-eject event will NullReferenceException from now on. Cause: " + __exception);
            }
            return null;
        }

        private static void FirearmStart_Postfix(ObservedFirearmController __instance)
        {
            if (!Enabled.Value || _weaponManagerField == null)
            {
                return;
            }

            try
            {
                if (_weaponManagerField.GetValue(__instance) != null)
                {
                    return;
                }

                int id = __instance.GetInstanceID();
                if (_nullManagerReported.Count > 500 || !_nullManagerReported.Add(id))
                {
                    return;
                }

                var anim = _objectInHandsAnimatorField?.GetValue(__instance);
                var prefab = _weaponPrefabField?.GetValue(__instance);

                string objectInHands = "<n/a>";
                if (prefab != null)
                {
                    var f = prefab.GetType().GetField("ObjectInHands",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var v = f?.GetValue(prefab);
                    objectInHands = v == null ? "NULL" : v.GetType().Name;
                }

                string tpl = "?";
                try
                {
                    var w = __instance.Weapon;
                    if (w != null)
                    {
                        tpl = w.TemplateId.ToString();
                    }
                }
                catch { }

                Log.LogError("[diag] _weaponManager is NULL after Start() -- this is the root of the "
                             + "IEventsConsumerOnShellEject NullReferenceException. weaponTemplate=" + tpl
                             + "  _objectInHandsAnimator=" + (anim == null ? "NULL" : "ok")
                             + "  _weaponPrefab=" + (prefab == null ? "NULL" : "ok")
                             + "  ObjectInHands=" + objectInHands);
            }
            catch (Exception ex)
            {
                Log.LogError("[diag] FirearmStart_Postfix error: " + ex.Message);
            }
        }

        private static bool ShellEject_Prefix(ObservedFirearmController __instance)
        {
            if (!Enabled.Value || _weaponManagerField == null)
            {
                return true;
            }

            try
            {
                if (_weaponManagerField.GetValue(__instance) != null)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            _shellEjectSkipped++;
            if (Verbose.Value && (_shellEjectSkipped == 1 || _shellEjectSkipped % 500 == 0))
            {
                Log.LogWarning("[diag] skipped " + _shellEjectSkipped
                               + " shell-eject events whose _weaponManager was null (these were the repeating "
                               + "NullReferenceException entries in the log).");
            }
            return false;
        }

        // ===================================================================
        // Patch 4 body: mirror of ItemController.get_NextId -- return the current value,
        // store the incremented one.
        // ===================================================================

        private static FieldInfo _splitIdNextField;

        private static bool SplitIdGenerator_Prefix(object __instance, ref MongoID __result)
        {
            if (!Enabled.Value || _splitIdNextField == null)
            {
                return true;
            }

            try
            {
                var current = (MongoID)_splitIdNextField.GetValue(__instance);
                var next = current;
                next++;
                _splitIdNextField.SetValue(__instance, next);
                __result = current;
                return false;
            }
            catch (Exception ex)
            {
                Log.LogError("Patch4 prefix error: " + ex.Message);
                return true;
            }
        }

        // ===================================================================
        // Patch 3 body (host): replace the single NetDataReader.GetByte() inside
        // FikaServer.OnNetworkReceive -- the broadcast-flag read -- with a helper that
        // reports "not a broadcast" when the packet is a Generic/InventoryOperation.
        // The helper consumes exactly the one byte the original would have consumed,
        // so the rest of the method parses unchanged.
        // ===================================================================

        internal static IEnumerable<CodeInstruction> OnNetworkReceive_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var getByte = AccessTools.Method(typeof(NetDataReader), nameof(NetDataReader.GetByte));
            var helper = AccessTools.Method(typeof(FikaInventoryResyncPlugin), nameof(ReadBroadcastFlagForRelay));

            var list = new List<CodeInstruction>(instructions);
            int replaced = 0;

            foreach (var ins in list)
            {
                if (ins.operand is MethodInfo mi && mi == getByte)
                {
                    // Only the first one is the broadcast-flag read at the top of the method.
                    if (replaced == 0)
                    {
                        ins.opcode = OpCodes.Call;
                        ins.operand = helper;
                    }
                    replaced++;
                }
            }

            if (replaced == 1)
            {
                Log.LogInfo("Patch3: replaced the broadcast-flag read (1 call site, as expected).");
            }
            else
            {
                Log.LogError("Patch3: expected exactly 1 NetDataReader.GetByte() call site in "
                             + "FikaServer.OnNetworkReceive but found " + replaced + " -- patch left inert.");
            }

            return list;
        }

        /// <summary>
        /// Behaves exactly like NetDataReader.GetByte() for the broadcast flag, except that it
        /// returns 0 (=> "do not relay") when the packet is a Generic/InventoryOperation, because
        /// FikaServer.OnInventoryPacketReceived already broadcasts those after validating them.
        /// </summary>
        internal static byte ReadBroadcastFlagForRelay(NetDataReader reader)
        {
            byte flag = reader.GetByte();
            if (flag != 1 || !Enabled.Value)
            {
                return flag;
            }

            int saved = reader.Position;
            try
            {
                if (reader.GetEnum<NetworkUtils.EPacketType>() == NetworkUtils.EPacketType.Generic
                    && reader.GetEnum<EGenericPacketType>() == EGenericPacketType.InventoryOperation)
                {
                    return 0;
                }
            }
            catch
            {
                // Malformed / truncated packet: fall through and let the original relay logic run.
            }
            finally
            {
                reader.SetPosition(saved);
            }

            return flag;
        }

        // ===================================================================
        // Patch 1 body
        // ===================================================================

        private static AccessTools.FieldRef<ObservedInventoryController, FikaPlayer> _fikaPlayerRef;
        private static FieldInfo _fikaPlayerField;

        private static readonly Dictionary<int, float> _lastRequestAt = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> _suppressed = new Dictionary<int, int>();

        private static FikaPlayer GetFikaPlayer(ObservedInventoryController ctrl)
        {
            if (_fikaPlayerRef != null)
            {
                return _fikaPlayerRef(ctrl);
            }
            if (_fikaPlayerField != null)
            {
                return _fikaPlayerField.GetValue(ctrl) as FikaPlayer;
            }
            return null;
        }

        private static void CreateOperationFromDescriptor_Postfix(ObservedInventoryController __instance,
                                                                  OperationCreationResult __result)
        {
            try
            {
                if (!Enabled.Value)
                {
                    return;
                }

                // Only react to an actual failure to build the operation against our mirror.
                if (!__result.Failed)
                {
                    return;
                }

                // The host runs the very same method for its mirrors of remote players; it already
                // sends its own resync in that case, so only clients request here.
                if (!Singleton<IFikaNetworkManager>.Instantiated)
                {
                    return;
                }
                var client = Singleton<IFikaNetworkManager>.Instance as FikaClient;
                if (client == null)
                {
                    return;
                }

                var player = GetFikaPlayer(__instance);
                if (player == null)
                {
                    return;
                }

                int netId = player.NetId;

                float now = Time.realtimeSinceStartup;
                float last;
                if (_lastRequestAt.TryGetValue(netId, out last))
                {
                    if (now - last < CooldownSeconds.Value)
                    {
                        int n;
                        _suppressed.TryGetValue(netId, out n);
                        _suppressed[netId] = n + 1;
                        return;
                    }
                }
                _lastRequestAt[netId] = now;

                int suppressedCount;
                _suppressed.TryGetValue(netId, out suppressedCount);
                _suppressed[netId] = 0;

                // Empty MongoId == "please report your authoritative CurrentId".
                ResyncInventoryIdPacket request = new ResyncInventoryIdPacket(netId);
                client.SendData(ref request, DeliveryMethod.ReliableOrdered, true);

                if (Verbose.Value)
                {
                    Log.LogInfo("sent resync request netId=" + netId
                                + ", error: " + __result.Error
                                + (suppressedCount > 0 ? ", suppressed " + suppressedCount + " since last request" : ""));
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Patch1 postfix error: " + ex.Message);
            }
        }

        // ===================================================================
        // Patch 2a body (host)
        // ===================================================================

        private static bool ServerResyncGuard_Prefix(ResyncInventoryIdPacket packet)
        {
            if (!Enabled.Value)
            {
                return true;
            }

            // MongoID.Equals only compares _timeStamp/_counter, and Put/GetMongoID only carry those
            // two fields, so this is an exact "is empty" test for a round-tripped default.
            if (packet.MongoId.Equals(default(MongoID)))
            {
                if (Verbose.Value)
                {
                    Log.LogInfo("host dropped empty resync for netId=" + packet.NetId
                                + " (request semantics, not an id assignment)");
                }
                return false;
            }

            return true;
        }

        // ===================================================================
        // Patch 2b body (client)
        // ===================================================================

        private static bool ClientResyncGuard_Prefix(FikaClient __instance, ResyncInventoryIdPacket packet)
        {
            if (!Enabled.Value)
            {
                return true;
            }

            // A real id: let Fika apply it to the mirror, as originally intended.
            if (!packet.MongoId.Equals(default(MongoID)))
            {
                return true;
            }

            // Empty id. Fika's own handler would either clobber an ObservedPlayer mirror with
            // default(MongoID), or - when the netId is our own player - answer with our CurrentId.
            // We must suppress the former and preserve the latter.
            var coop = __instance.CoopHandler;
            if (coop == null || coop.Players == null)
            {
                return false;
            }

            FikaPlayer player;
            if (!coop.Players.TryGetValue(packet.NetId, out player) || player == null)
            {
                return false;
            }

            if (player.IsYourPlayer)
            {
                // This is the request path: let Fika reply with our authoritative CurrentId.
                if (Verbose.Value)
                {
                    Log.LogInfo("answering resync request for our own netId=" + packet.NetId);
                }
                return true;
            }

            if (Verbose.Value)
            {
                Log.LogInfo("client dropped empty resync for netId=" + packet.NetId
                            + " (would have cleared someone's mirror)");
            }
            return false;
        }
    }
}
