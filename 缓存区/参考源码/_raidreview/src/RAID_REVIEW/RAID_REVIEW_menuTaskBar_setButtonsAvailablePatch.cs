using System;
using System.Reflection;
using EFT.UI;
using SPT.Reflection.Patching;

namespace RAID_REVIEW;

internal class RAID_REVIEW_menuTaskBar_setButtonsAvailablePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MenuTaskBar).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	[PatchPostfix]
	private static void PatchPostFix(ref MenuTaskBar __instance)
	{
		try
		{
			if (RAID_REVIEW.InsertMenuItem.Value && MenuTaskbarMod.Insert(__instance))
			{
				ModulePatch.Logger.LogInfo((object)"RAID_REVIEW :::: Inserted Menu Item");
			}
		}
		catch (Exception ex)
		{
			ModulePatch.Logger.LogError((object)(ex.Message ?? ""));
		}
	}
}
