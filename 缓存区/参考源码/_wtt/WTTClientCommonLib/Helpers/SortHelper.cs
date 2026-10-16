using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace WTTClientCommonLib.Helpers;

public static class SortHelper
{
	public static void AddToSortOrder(this Type itemType, Type itemTypeToInsertAfter, int offset = 1)
	{
		InsertAfter(itemType, itemTypeToInsertAfter, offset);
	}

	public static int GetSortOrder(this Type itemType)
	{
		return ItemSorter._itemSuccessors.IndexOf(itemType);
	}

	public static void Insert(int sortIndex, Type itemType)
	{
		ItemSorter._itemSuccessors.Insert(sortIndex, itemType);
	}

	public static void SetIndex(int newIndex, Type itemType)
	{
		List<Type> itemSuccessors = ItemSorter._itemSuccessors;
		int num = itemSuccessors.IndexOf(itemType);
		if (num < 0)
		{
			LogHelper.LogWarn($"failed to set sort index for item {itemType} because it does not exist in sort order list");
			return;
		}
		newIndex = Math.Clamp(newIndex, 0, itemSuccessors.Count - 1);
		itemSuccessors.RemoveAt(num);
		if (newIndex > num)
		{
			newIndex--;
		}
		itemSuccessors.Insert(newIndex, itemType);
	}

	public static void InsertAfter(Type itemType, Type itemToInsertAfter, int offset = 1)
	{
		int num = ItemSorter.IndexOf(itemToInsertAfter);
		if (num < 0)
		{
			LogHelper.LogWarn($"failed to add item type {itemType} to sort order because item type {itemToInsertAfter} doesn't exist in sort order list");
		}
		else
		{
			ItemSorter._itemSuccessors.Insert(num + offset, itemType);
		}
	}
}
