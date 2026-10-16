using System;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StashTools;

[HarmonyPatch(typeof(GridItemView), "OnClick")]
internal static class ClickPatch
{
	private static bool Prefix(GridItemView __instance, InputButton button, Vector2 position, bool doubleClick)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Invalid comparison between Unknown and I4
		try
		{
			if (!StashBehaviour.Active || ChineseSettings.IsOpen || !Selection.Allowed(__instance))
			{
				return true;
			}
			Item item = ((ItemView)__instance).Item;
			if (StashBehaviour.CanSell && Input.GetKey(Plugin.SellKey.Value))
			{
				if (!Batch.Busy)
				{
					SellMode mode = (((int)button == 0) ? SellMode.仅商人 : (((int)button == 1) ? SellMode.仅跳蚤 : Plugin.MiddleMode.Value));
					Sales.Start(Selection.ItemsFor(item), mode);
				}
				return false;
			}
			if ((int)button != 0)
			{
				return true;
			}
			if (Batch.Busy)
			{
				return false;
			}
			if (StashBehaviour.SuppressClick)
			{
				return false;
			}
			if (StashBehaviour.Shift)
			{
				Selection.Toggle(item);
				return false;
			}
			if (StashBehaviour.Control && Selection.Contains(item) && Selection.Count > 1)
			{
				if (TraderSelection.Active)
				{
					if (TraderSelection.SaleMode)
					{
						TraderSelection.Prepare(InventoryAccess.Trader(TraderSelection.Screen).CurrentAssortment, item);
					}
				}
				else
				{
					Moves.QuickMove(Selection.ItemsFor(item));
				}
				return false;
			}
			if (StashBehaviour.Control && MovementEnhancements.Active && MovementEnhancements.EquipEnabled)
			{
				Moves.QuickMove((Item[])(object)new Item[1] { item });
				return false;
			}
			if (!Selection.Contains(item))
			{
				Selection.Clear();
			}
		}
		catch (Exception ex)
		{
			StashBehaviour.Error("点击处理失败", ex);
		}
		return true;
	}
}
