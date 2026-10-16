using System.Collections.Generic;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

internal static class InventoryAccess
{
	private static readonly FieldRef<ItemUiContext, ItemController> UiControllerField;

	private static readonly FieldRef<InventoryScreen, InventoryController> ScreenControllerField;

	private static readonly FieldRef<GridView, ItemController> GridControllerField;

	private static readonly FieldRef<ItemView, ItemController> ItemControllerField;

	private static readonly FieldRef<GridItemView, bool> InRaidField;

	private static readonly FieldRef<ItemView, bool> KilledField;

	private static readonly FieldRef<Tab, bool> SelectedField;

	private static readonly FieldRef<DraggedItemView, IContainerView> ContainerField;

	private static readonly FieldRef<DraggedItemView, ItemContext> TargetField;

	private static readonly FieldRef<TraderDealScreen, Trader> TraderField;

	private static readonly FieldRef<TradingItemView, Assortment> TradingAssortmentField;

	private static readonly FieldRef<TradingTableGridView, Assortment> TableAssortmentField;

	private static readonly FieldRef<SlotView, ItemView> SlotItemViewField;

	private static readonly FieldRef<SlotView, ItemController> SlotControllerField;

	private static readonly FieldRef<SlotView, bool> SlotHighlightField;

	private static readonly Color GridSwap;

	private static readonly Color SlotSwap;

	internal const float CellSize = 63f;

	internal static Color GridSwapColor
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			return GridSwap;
		}
	}

	internal static Color SlotSwapColor
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			return SlotSwap;
		}
	}

	internal static ItemController Controller(ItemUiContext ui)
	{
		return UiControllerField.Invoke(ui);
	}

	internal static InventoryController? Controller(InventoryScreen? screen)
	{
		if (!((Object)(object)screen == (Object)null))
		{
			return ScreenControllerField.Invoke(screen);
		}
		return null;
	}

	internal static ItemController Controller(GridView grid)
	{
		return GridControllerField.Invoke(grid);
	}

	internal static ItemController Controller(ItemView view)
	{
		return ItemControllerField.Invoke(view);
	}

	internal static ItemController Controller(SlotView view)
	{
		return SlotControllerField.Invoke(view);
	}

	internal static bool InRaid(GridItemView view)
	{
		return InRaidField.Invoke(view);
	}

	internal static bool Killed(ItemView view)
	{
		return KilledField.Invoke(view);
	}

	internal static bool InventoryTabActive(InventoryScreen screen)
	{
		IReadOnlyDictionary<EInventoryTab, Tab> tabDictionary = screen._tabDictionary;
		if (tabDictionary != null)
		{
			if (!tabDictionary.TryGetValue((EInventoryTab)1, out var value) || !((Object)(object)value != (Object)null) || !SelectedField.Invoke(value))
			{
				if (tabDictionary.TryGetValue((EInventoryTab)0, out var value2) && (Object)(object)value2 != (Object)null)
				{
					return SelectedField.Invoke(value2);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static IContainerView? Container(DraggedItemView? drag)
	{
		if (!((Object)(object)drag == (Object)null))
		{
			return ContainerField.Invoke(drag);
		}
		return null;
	}

	internal static ItemContext? Target(DraggedItemView drag)
	{
		return TargetField.Invoke(drag);
	}

	internal static Trader? Trader(TraderDealScreen? screen)
	{
		if (!((Object)(object)screen == (Object)null))
		{
			return TraderField.Invoke(screen);
		}
		return null;
	}

	internal static Assortment Assortment(TradingItemView view)
	{
		return TradingAssortmentField.Invoke(view);
	}

	internal static Assortment Assortment(TradingTableGridView grid)
	{
		return TableAssortmentField.Invoke(grid);
	}

	internal static ItemView? ItemView(SlotView slot)
	{
		return SlotItemViewField.Invoke(slot);
	}

	internal static void HighlightSlot(SlotView slot)
	{
		SlotHighlightField.Invoke(slot) = true;
	}

	static InventoryAccess()
	{
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		UiControllerField = AccessTools.FieldRefAccess<ItemUiContext, ItemController>("_itemController");
		ScreenControllerField = AccessTools.FieldRefAccess<InventoryScreen, InventoryController>("_inventoryController");
		GridControllerField = AccessTools.FieldRefAccess<GridView, ItemController>("_itemController");
		ItemControllerField = AccessTools.FieldRefAccess<ItemView, ItemController>("ItemController");
		InRaidField = AccessTools.FieldRefAccess<GridItemView, bool>("_inRaid");
		KilledField = AccessTools.FieldRefAccess<ItemView, bool>("IsKilled");
		SelectedField = AccessTools.FieldRefAccess<Tab, bool>("_selected");
		ContainerField = AccessTools.FieldRefAccess<DraggedItemView, IContainerView>("_currentContainerUnderCursor");
		TargetField = AccessTools.FieldRefAccess<DraggedItemView, ItemContext>("_currentTargetItemContext");
		TraderField = AccessTools.FieldRefAccess<TraderDealScreen, Trader>("_trader");
		TradingAssortmentField = AccessTools.FieldRefAccess<TradingItemView, Assortment>("_traderAssortment");
		TableAssortmentField = AccessTools.FieldRefAccess<TradingTableGridView, Assortment>("_traderAssortment");
		SlotItemViewField = AccessTools.FieldRefAccess<SlotView, ItemView>("ItemView");
		SlotControllerField = AccessTools.FieldRefAccess<SlotView, ItemController>("ItemController");
		SlotHighlightField = AccessTools.FieldRefAccess<SlotView, bool>("HighlightedGlobally");
		GridSwap = (Color)AccessTools.Field(typeof(GridView), "_swapColor").GetValue(null);
		SlotSwap = (Color)AccessTools.Field(typeof(SlotView), "_swapColor").GetValue(null);
	}
}
