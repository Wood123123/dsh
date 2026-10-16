using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using HarmonyLib;

namespace infiniteTripwire;

[BepInPlugin("ciallo.inftripwire", "Tripwire Grenade Modify", "1.0.0")]
public class NoTripwirePlugin : BaseUnityPlugin
{
	[HarmonyPatch(typeof(ItemManipulator), "SimulatePlantTripwire")]
	private class PatchSimulatePlantTripwire
	{
		private static bool Prefix(InventoryController controller, ThrowWeap tripwire, PlantingKit plantingKit, ref OperationResult<PlantTripwireResult> __result)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Expected O, but got Unknown
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			if (!noConsumption.Value)
			{
				return true;
			}
			OperationResult<DiscardResult> val = ItemManipulator.Discard((Item)(object)tripwire, (ItemController)(object)controller, true);
			if (val.Failed)
			{
				__result = OperationResult<PlantTripwireResult>.op_Implicit(val.Error);
				return false;
			}
			List<ISyncOperationResult> list = new List<ISyncOperationResult> { (ISyncOperationResult)(object)val.Value };
			__result = OperationResult<PlantTripwireResult>.op_Implicit(new PlantTripwireResult(tripwire, plantingKit, new CombinedOperationResult(list.ToArray())));
			return false;
		}
	}

	[HarmonyPatch(typeof(TripwireSynchronizableObject), "SetupStakes")]
	private class PatchTripwireInertSeconds
	{
		private static void Prefix(TripwireSynchronizableObject __instance)
		{
			__instance.TripwiresSettings.InertSeconds = effSecond.Value;
		}
	}

	[HarmonyPatch(typeof(TripwireSynchronizableObject), "SetupStakes")]
	private class PatchTripwireSettings
	{
		private static void Prefix()
		{
			TripwiresGlobalSettings tripwiresSettings = Singleton<GlobalConfiguration>.Instance.TripwiresSettings;
			tripwiresSettings.MaxLength = maxTrip.Value;
			tripwiresSettings.MinLength = 0.1f;
			tripwiresSettings.MaxHeightDifference = maxHeightDiff.Value;
		}
	}

	[HarmonyPatch(typeof(TripwireSynchronizableObject), "ValidateTripwirePositionAI")]
	private class PatchValidateTripwirePositionAI
	{
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Expected O, but got Unknown
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Expected O, but got Unknown
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float num && Math.Abs(num - 0.2f) < 0.0001f)
				{
					list[i] = new CodeInstruction(OpCodes.Ldsfld, (object)AccessTools.Field(typeof(NoTripwirePlugin), "maxHeightDiff"));
					list.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.PropertyGetter(typeof(ConfigEntry<float>), "Value")));
				}
			}
			return list;
		}
	}

	[HarmonyPatch(typeof(TripwireVisualPlacer), "Init")]
	private class PatchTripwireVisualPlacerInit
	{
		private static void Prefix()
		{
			TripwiresGlobalSettings tripwiresSettings = Singleton<GlobalConfiguration>.Instance.TripwiresSettings;
			tripwiresSettings.MaxPreviewLength = maxTrip.Value;
			tripwiresSettings.MaxLength = maxTrip.Value;
			tripwiresSettings.MinLength = 0.1f;
			tripwiresSettings.MaxHeightDifference = maxHeightDiff.Value;
			tripwiresSettings.GroundDotProductTolerance = 0.10000000149011612;
		}
	}

	[HarmonyPatch(typeof(TripwireSynchronizableObject), "ActivateGrenade")]
	private class PatchMethod6
	{
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Expected O, but got Unknown
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Expected O, but got Unknown
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			for (int i = 0; i < list.Count; i++)
			{
				if (i == 23 && list[i].opcode == OpCodes.Ldc_R4)
				{
					list[i] = new CodeInstruction(OpCodes.Ldsfld, (object)AccessTools.Field(typeof(NoTripwirePlugin), "boomDelayReduce"));
					list.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.PropertyGetter(typeof(ConfigEntry<float>), "Value")));
				}
			}
			return list;
		}
	}

	private static ConfigEntry<bool> noConsumption;

	private static ConfigEntry<float> boomDelayReduce;

	private static ConfigEntry<int> effSecond;

	private static ConfigEntry<float> maxHeightDiff;

	private static ConfigEntry<float> maxTrip;

	private void Awake()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		noConsumption = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Infinite Tripwire", true, (ConfigDescription)null);
		boomDelayReduce = ((BaseUnityPlugin)this).Config.Bind<float>("General", "Reduce Explosion Delay (?)", 0f, new ConfigDescription("Decrease time (in second) before boom. Higher than grenade's fuse value will make instant explosion.\nWarning: Partisan's F-1 trap is also changed (=3.5s - this)", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 5f), Array.Empty<object>()));
		effSecond = ((BaseUnityPlugin)this).Config.Bind<int>("General", "Effective Seconds", 3600, (ConfigDescription)null);
		maxHeightDiff = ((BaseUnityPlugin)this).Config.Bind<float>("General", "Max Height Diff", 1.5f, new ConfigDescription("", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.2f, 3f), Array.Empty<object>()));
		maxTrip = ((BaseUnityPlugin)this).Config.Bind<float>("General", "Max Length", 5f, new ConfigDescription("", (AcceptableValueBase)(object)new AcceptableValueRange<float>(3f, 10f), Array.Empty<object>()));
		new Harmony("ciallo.inftripwire").PatchAll();
	}
}
