using System;
using System.Reflection;
using CookingGrenades.Utils;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class PlayerGrenadeHandsControllerHandleFireInputPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GrenadeHandsController), "HandleFireInput", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void PatchPostfix(GrenadeHandsController __instance)
	{
		ThrowWeap item = __instance.Item;
		if (item != null && !string.IsNullOrEmpty(((Item)item).StringTemplateId))
		{
			GrenadeWheel.PreferredTemplateId = ((Item)item).StringTemplateId;
		}
		IAnimator animator = ((ObjectInHandsAnimator)((AbstractHandsController)__instance).FirearmsAnimator).Animator;
		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();
		Plugin.log.LogInfo((object)$"[HandleFireInput] WaitingForLowThrow={__instance.WaitingForLowThrow}, IsCooking={cookingTimer.IsCooking}, AnimPullRingDone={AnimationUtils.IsRemovePullRingCompleted(animator)}");
		if (__instance.WaitingForLowThrow && !cookingTimer.IsCooking && AnimationUtils.IsRemovePullRingCompleted(animator))
		{
			Plugin.log.LogInfo((object)"[HandleFireInput] 开始温雷（低抛）");
			GrenadeCookingHelper.StartCookingWithLeverSound(__instance);
		}
	}
}
