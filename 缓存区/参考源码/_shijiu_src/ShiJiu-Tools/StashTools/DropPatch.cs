using System;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(GridView), "AcceptItem")]
internal static class DropPatch
{
	private static bool Prefix(GridView __instance, DragItemContext itemContext, ItemContext targetItemContext, ref Task __result)
	{
		if ((Object)(object)__instance.TryCast<TradingTableGridView>() != (Object)null)
		{
			return true;
		}
		if (!StashBehaviour.Active || Selection.Count < 2 || !Selection.Contains(((ItemContext)itemContext).Item))
		{
			return true;
		}
		__result = Task.CompletedTask;
		try
		{
			if (Batch.Busy)
			{
				return false;
			}
			if (targetItemContext != null)
			{
				CompoundItem val = targetItemContext.Item.TryCast<CompoundItem>();
				if (val != null && val.Grids != null && val.Grids.Length != 0)
				{
					Moves.PackInto(Selection.ItemsFor(((ItemContext)itemContext).Item), val.Grids.ToArray());
				}
				else
				{
					StashBehaviour.Notify("请把所选物品拖到空白格子或容器上。");
				}
			}
			else
			{
				Moves.Drop(Selection.ItemsFor(((ItemContext)itemContext).Item), itemContext, __instance);
			}
			DragPreview.Clear();
			((ItemContext)itemContext).DragCancelled();
		}
		catch (Exception ex)
		{
			StashBehaviour.Error("批量拖动失败", ex);
		}
		return false;
	}
}
