using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace TripwireKitFix
{
    /// <summary>
    /// Replaces ciallo.inftripwire's broken "no consumption" implementation with a spec-compliant one.
    ///
    /// WHY: the original mod returns a hand-built PlantTripwireResult that omits the PlantingKit's
    /// Discard entry. Fika's own modding guide (Modding-Fika.md, "Inventory Replication") states:
    ///   "Do not call the result directly! Use it to create the operation and run it on the controller."
    ///   "If you circumvent the intended way of executing inventory operations, you will inevitably cause desync!"
    /// The truncated result leaves PlantTripwireOperation half-executed, which leaks an entry into
    /// ItemController._activeEvents (RemoveActiveEvent compares by reference, so it can never be removed).
    /// The item is then permanently locked with PlayerIsBusyError: "... is currently being modified".
    ///
    /// THIS PLUGIN: lets the VANILLA path run untouched (grenade + kit both consumed, operation complete
    /// and Fika-synced), then returns the kit through a normal, spec-compliant Add operation routed via
    /// controller.RunNetworkTransaction - exactly the pattern Fika itself uses.
    /// </summary>
    [BepInPlugin("com.local.tripwirekitfix", "Tripwire Kit Fix", "1.0.0")]
    [BepInDependency("ciallo.inftripwire", BepInDependency.DependencyFlags.SoftDependency)]
    public class KitFixPlugin : BaseUnityPlugin
    {
        internal const string Tag = "[TripwireKitFix]";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Verbose;

        private static Harmony _harmony;

        private sealed class PendingKit
        {
            public PlantingKit Kit;
            public ItemAddress Address;
            public ItemController Controller;
            public int Frame;
        }

        // Recorded in the Execute prefix, moved to the queue by the Execute postfix.
        private static PendingKit _current;
        private static readonly Queue<PendingKit> _queue = new Queue<PendingKit>();

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Return the planting kit after a successful plant. Off = vanilla consumption.");
            Verbose = Config.Bind("General", "Verbose", false, "Log every step.");

            _harmony = new Harmony("com.local.tripwirekitfix");

            // --- 1) Undo the original mod's broken prefix -------------------------
            bool removed = false;
            try
            {
                var target = AccessTools.Method(typeof(ItemManipulator), nameof(ItemManipulator.SimulatePlantTripwire));
                if (target != null)
                {
                    _harmony.Unpatch(target, HarmonyPatchType.Prefix, "ciallo.inftripwire");
                    removed = true;
                }
            }
            catch (Exception ex) { Log.LogWarning(Tag + " unpatch failed: " + ex.Message); }

            // --- 2) Install our own hooks ----------------------------------------
            int ok = 0;
            try { _harmony.CreateClassProcessor(typeof(Patch_PlantTripwireExecute)).Patch(); ok++; }
            catch (Exception ex) { Log.LogError(Tag + " execute-hook failed: " + ex.Message); }

            Log.LogInfo(Tag + " v1.0.0 loaded. originalPrefixRemoved=" + removed +
                        ", hooks=" + ok + "/1, Enabled=" + Enabled.Value);
            if (!removed && Enabled.Value)
                Log.LogWarning(Tag + " could not remove ciallo.inftripwire's prefix - if that mod is " +
                               "installed, the lock-up bug is still active.");
        }

        private void Update()
        {
            if (!Enabled.Value) { _queue.Clear(); return; }
            if (_queue.Count == 0) return;

            // One per frame: the Add must not run while the plant operation is still unwinding.
            var p = _queue.Dequeue();
            try { ReturnKit(p); }
            catch (Exception ex) { Log.LogWarning(Tag + " return failed: " + ex.Message); }
        }

        private static void ReturnKit(PendingKit p)
        {
            if (p == null || p.Kit == null || p.Controller == null) return;

            // Only the locally controlled inventory. The host replays the client's operation into
            // its own controller; returning the kit there too would duplicate it.
            var tn = p.Controller.GetType().Name;
            bool isLocal = tn == "PlayerInventoryController" || tn == "ClientInventoryController";
            if (!isLocal)
            {
                if (Verbose.Value) Log.LogInfo(Tag + " skip, non-local controller: " + tn);
                return;
            }

            if (p.Kit.CurrentAddress != null)
            {
                if (Verbose.Value) Log.LogInfo(Tag + " kit already present, nothing to do.");
                return;
            }

            IOperationResult op = null;
            string how = null;

            // --- try 1: original address -----------------------------------------
            // Use Move, NOT Add. ItemController.ConvertOperationResultToOperation only
            // understands a fixed set of result types and AddResult is NOT one of them
            // (MoveResult is). Add throws "operationResult is of unexpected type".
            if (p.Address != null)
            {
                try
                {
                    var r = ItemManipulator.Move(p.Kit, p.Address, p.Controller, true);
                    if (r.Succeeded) { op = r.Value; how = "origin:" + p.Address.ContainerName; }
                    else if (Verbose.Value) Log.LogInfo(Tag + " move@origin failed: " + r.Error);
                }
                catch (Exception ex) { if (Verbose.Value) Log.LogInfo(Tag + " move@origin threw: " + ex.Message); }
            }

            // --- try 2: anywhere appropriate -------------------------------------
            if (op == null)
            {
                try
                {
                    var r2 = ItemManipulator.QuickFindAppropriatePlace(
                        p.Kit, p.Controller, new[] { p.Controller.Root },
                        ItemManipulator.EMoveItemOrder.PickUp, true);
                    if (r2.Succeeded) { op = r2.Value; how = "quickfind"; }
                    else if (Verbose.Value) Log.LogInfo(Tag + " quickfind failed: " + r2.Error);
                }
                catch (Exception ex) { Log.LogWarning(Tag + " quickfind threw: " + ex.Message); }
            }

            if (op == null)
            {
                Log.LogWarning(Tag + " could not return kit " + p.Kit.Id +
                               " - consumed as vanilla. (no free place found)");
                return;
            }

            // --- execute through the controller so Fika replicates it ------------
            // Mirrors Fika's own usage, e.g. ClientSharedQuestController:
            //     playerInventory.RunNetworkTransaction(pickupResult.Value);
            try
            {
                p.Controller.RunNetworkTransaction(op, null);
                Log.LogInfo(Tag + " returned kit " + p.Kit.Id + " via " + how +
                            " (" + tn + ") plantFrame=" + p.Frame + " nowFrame=" + Time.frameCount);
            }
            catch (Exception ex)
            {
                Log.LogWarning(Tag + " RunNetworkTransaction failed: " + ex.Message);
            }
        }

        /// <summary>Hooks PlantTripwireResult.Execute: record before, enqueue after.</summary>
        [HarmonyPatch(typeof(PlantTripwireResult), nameof(PlantTripwireResult.Execute))]
        internal static class Patch_PlantTripwireExecute
        {
            [HarmonyPrefix]
            internal static void Prefix(PlantTripwireResult __instance)
            {
                _current = null;
                if (!Enabled.Value || __instance == null) return;
                try
                {
                    var kit = __instance.PlantingKit;
                    if (kit == null) return;

                    // Record BEFORE consumption - afterwards the address is gone.
                    var addr = kit.CurrentAddress ?? kit.Parent;
                    IItemOwner owner = null;
                    if (addr != null)
                    {
                        try { owner = ContainerExtensions.GetOwner(addr); } catch { }
                    }

                    _current = new PendingKit
                    {
                        Kit = kit,
                        Address = addr,
                        Controller = owner as ItemController,
                        Frame = Time.frameCount
                    };
                    if (Verbose.Value)
                        Log.LogInfo(Tag + " plant execute: kit=" + kit.Id +
                                    " addr=" + (addr != null ? addr.ContainerName : "<null>") +
                                    " owner=" + (owner != null ? owner.GetType().Name : "<null>"));
                }
                catch (Exception ex) { if (Verbose.Value) Log.LogInfo(Tag + " prefix: " + ex.Message); }
            }

            [HarmonyPostfix]
            internal static void Postfix(ref OperationResult __result)
            {
                var p = _current;
                _current = null;
                if (p == null) return;

                if (!__result.Succeeded)
                {
                    if (Verbose.Value) Log.LogInfo(Tag + " plant failed, kit not returned.");
                    return;
                }
                if (p.Controller == null)
                {
                    Log.LogWarning(Tag + " could not resolve controller for kit " + p.Kit.Id +
                                   " - kit will be consumed.");
                    return;
                }
                _queue.Enqueue(p);
            }
        }
    }
}
