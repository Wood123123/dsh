using System;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(TradingTableGridView), "AcceptItem")]
internal static class TraderDropPatch
{
	private static bool Prefix(TradingTableGridView __instance, DragItemContext itemContext, ItemContext targetItemContext, ref Task __result)
	{
		if (!Plugin.Enabled.Value || !TraderSelection.Active || !Selection.Contains(((ItemContext)itemContext).Item) || Selection.Count < 2)
		{
			return true;
		}
		__result = Task.CompletedTask;
		try
		{
			TraderSelection.Prepare(InventoryAccess.Assortment(__instance), ((ItemContext)itemContext).Item);
		}
		catch (Exception ex)
		{
			StashBehaviour.Error("批量放入商人出售栏失败", ex);
		}
		((ItemContext)itemContext).DragCancelled();
		return false;
	}
}
