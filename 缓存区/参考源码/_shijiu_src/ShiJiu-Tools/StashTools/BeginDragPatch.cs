using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(ItemView), "OnBeginDrag")]
internal static class BeginDragPatch
{
	private static void Postfix(ItemView __instance)
	{
		try
		{
			DragPreview.Begin(__instance);
			MovementEnhancements.Begin(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("拖动预览初始化失败：" + ex));
		}
	}

	private static bool Prefix(ItemView __instance)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (!StashBehaviour.Active)
		{
			return true;
		}
		if (ChineseSettings.IsOpen || Batch.Busy || StashBehaviour.Drawing || (StashBehaviour.CanSell && Input.GetKey(Plugin.SellKey.Value)))
		{
			return false;
		}
		if (!Selection.Contains(__instance.Item))
		{
			Selection.Clear();
		}
		return true;
	}
}
