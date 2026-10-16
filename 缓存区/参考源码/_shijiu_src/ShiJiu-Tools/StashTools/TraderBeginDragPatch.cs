using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(TradingItemView), "OnBeginDrag")]
internal static class TraderBeginDragPatch
{
	private static bool Prefix()
	{
		if (StashBehaviour.Active)
		{
			if (!ChineseSettings.IsOpen && !StashBehaviour.Drawing)
			{
				return !Batch.Busy;
			}
			return false;
		}
		return true;
	}

	private static void Postfix(TradingItemView __instance)
	{
		try
		{
			DragPreview.Begin((ItemView)(object)__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("商人拖动预览初始化失败：" + ex));
		}
	}
}
