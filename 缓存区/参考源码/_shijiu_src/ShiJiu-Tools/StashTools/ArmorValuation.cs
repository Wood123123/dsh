using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;

namespace StashTools;

internal static class ArmorValuation
{
	internal static string? PlateTemplate(ItemFilter filter)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		ref MongoID? plate = ref filter.Plate;
		if (!plate.HasValue)
		{
			return null;
		}
		return ((object)plate.GetValueOrDefault()/*cast due to constrained. prefix*/).ToString();
	}

	internal static void PrepareBodySample(Item sample, Func<string, Item> create)
	{
		Slot[] array = sample.TryCast<CompoundItem>()?.Slots;
		if (array == null)
		{
			return;
		}
		Slot[] array2 = array;
		foreach (Slot val in array2)
		{
			if (val.Filters == null)
			{
				continue;
			}
			ItemFilter[] filters = val.Filters;
			foreach (ItemFilter val2 in filters)
			{
				if (val2 == null)
				{
					continue;
				}
				string text = PlateTemplate(val2);
				if (text == null)
				{
					continue;
				}
				if (val2.locked)
				{
					if (val.ContainedItem == null)
					{
						val.ContainedItem = create(text);
					}
				}
				else if (val.ContainedItem != null)
				{
					val.ContainedItem = null;
				}
				break;
			}
		}
	}

	internal static bool FixedSlot(IContainer container)
	{
		Slot val = container.TryCast<Slot>();
		if (((val != null) ? val.Filters : null) == null)
		{
			return false;
		}
		ItemFilter[] filters = val.Filters;
		foreach (ItemFilter val2 in filters)
		{
			if (val2 != null && val2.locked && PlateTemplate(val2) != null)
			{
				return true;
			}
		}
		return false;
	}

	internal static HashSet<string> RemovablePlateIds(Item item)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Slot[] array = item.TryCast<CompoundItem>()?.Slots;
		if (array == null)
		{
			return hashSet;
		}
		Slot[] array2 = array;
		foreach (Slot val in array2)
		{
			if (val.ContainedItem == null || val.Filters == null || FixedSlot((IContainer)(object)val))
			{
				continue;
			}
			ItemFilter[] filters = val.Filters;
			foreach (ItemFilter val2 in filters)
			{
				if (val2 != null && PlateTemplate(val2) != null)
				{
					hashSet.Add(val.ContainedItem.Id);
					break;
				}
			}
		}
		return hashSet;
	}
}
