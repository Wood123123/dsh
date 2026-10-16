using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace StashTools;

[HarmonyPatch(typeof(TradingItemView), "OnClick")]
internal static class TraderClickPatch
{
	private static bool Prefix(TradingItemView __instance, InputButton button)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.Enabled.Value || !TraderSelection.Active || ChineseSettings.IsOpen || !Selection.Allowed((GridItemView?)(object)__instance))
		{
			return true;
		}
		if ((int)button != 0)
		{
			return true;
		}
		if (StashBehaviour.SuppressClick || StashBehaviour.Drawing)
		{
			return false;
		}
		if (StashBehaviour.Shift)
		{
			Selection.Toggle(((ItemView)__instance).Item);
			return false;
		}
		if (TraderSelection.SaleMode && StashBehaviour.Control && Selection.Contains(((ItemView)__instance).Item) && Selection.Count > 1)
		{
			try
			{
				TraderSelection.Prepare(InventoryAccess.Assortment(__instance), ((ItemView)__instance).Item);
			}
			catch (Exception ex)
			{
				StashBehaviour.Error("放入商人出售栏失败", ex);
			}
			return false;
		}
		return true;
	}
}
