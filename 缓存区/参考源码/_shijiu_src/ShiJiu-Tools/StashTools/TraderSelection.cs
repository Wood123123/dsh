using System.Linq;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace StashTools;

internal static class TraderSelection
{
	internal static TraderDealScreen? Screen;

	internal static bool Active
	{
		get
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Invalid comparison between Unknown and I4
			if ((Object)(object)Screen != (Object)null && ((Component)Screen).gameObject.activeInHierarchy)
			{
				ItemUiContext instance = ItemUiContext.Instance;
				if (instance != null && (int)instance.ContextType == 3)
				{
					Trader? obj = InventoryAccess.Trader(Screen);
					return ((obj != null) ? obj.CurrentAssortment : null) != null;
				}
			}
			return false;
		}
	}

	internal static bool SaleMode
	{
		get
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Invalid comparison between Unknown and I4
			if (Active)
			{
				return (int)Screen.TradeMode == 1;
			}
			return false;
		}
	}

	internal static bool InArea(GameObject target)
	{
		if ((Object)(object)Screen != (Object)null)
		{
			if (!target.transform.IsChildOf(((Component)Screen).transform))
			{
				return (Object)(object)target.GetComponentInParent<GridWindow>() != (Object)null;
			}
			return true;
		}
		return false;
	}

	internal static bool Selectable(GridItemView view)
	{
		TradingItemView val = view.TryCast<TradingItemView>();
		if ((Object)(object)val == (Object)null)
		{
			return true;
		}
		if (Active && (Object)(object)view.TryCast<TradingPlayerItemView>() != (Object)null && InventoryAccess.Assortment(val) != null)
		{
			return !InventoryAccess.Assortment(val).IsBeingSold(((ItemView)view).Item);
		}
		return false;
	}

	internal static void Reset()
	{
		Batch.Cancel("交易界面已切换，停止剩余操作。");
		Selection.Clear();
		SelectionVisuals.Clear();
		DragPreview.Clear();
		StashBehaviour.Drawing = false;
		Screen = null;
	}

	internal static void Prepare(Assortment assortment, Item lead)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		if (!SaleMode || assortment.Identity() != InventoryAccess.Trader(Screen).CurrentAssortment.Identity() || assortment.TransactionInProgress || Batch.Busy)
		{
			return;
		}
		Item[] array = (from i in Selection.ItemsFor(lead)
			orderby i.Id == lead.Id descending
			select i).ToArray();
		int num = 0;
		int num2 = 0;
		Item[] array2 = array;
		foreach (Item val in array2)
		{
			if (!Selection.Owned(val) || val.QuestItem || (int)val.PinLockState != 0 || assortment.IsBeingSold(val) || !assortment.CanPrepareItemToSell(val))
			{
				num2++;
				continue;
			}
			LocationInGrid val2 = assortment.QuickFindTradingLocation(val)?.LocationInGrid;
			if (val2 == (LocationInGrid)null)
			{
				num2++;
				continue;
			}
			assortment.PrepareToSell(val, val2);
			if (assortment.IsBeingSold(val))
			{
				num++;
				Selection.Items.Remove(val.Id);
			}
			else
			{
				num2++;
			}
		}
		SelectionVisuals.Clear();
		string message = $"已放入当前商人出售栏 {num} 件" + ((num2 > 0) ? $"，{num2} 件未放入。" : "。") + "确认后点击交易。";
		if (num2 > 0)
		{
			StashBehaviour.Notify(message);
		}
		else
		{
			StashBehaviour.Completed(message);
		}
	}
}
