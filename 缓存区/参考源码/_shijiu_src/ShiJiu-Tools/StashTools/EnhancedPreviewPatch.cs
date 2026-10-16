using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(DraggedItemView), "Update")]
internal static class EnhancedPreviewPatch
{
	private static void Postfix(DraggedItemView __instance)
	{
		try
		{
			MovementEnhancements.Preview(__instance);
		}
		catch (Exception ex)
		{
			MovementEnhancements.PreviewFailed(ex);
		}
	}
}
