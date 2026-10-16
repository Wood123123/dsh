using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace StashTools;

internal static class ItemValuation
{
	internal static double Units(Item item)
	{
		Ammo? obj = item.TryCast<Ammo>();
		if (obj != null && obj.IsUsed)
		{
			return 0.0;
		}
		double num = 1.0;
		if (item.StackMaxSize <= 1)
		{
			MedKitComponent itemComponent = item.GetItemComponent<MedKitComponent>();
			FoodDrinkComponent itemComponent2 = item.GetItemComponent<FoodDrinkComponent>();
			ResourceComponent itemComponent3 = item.GetItemComponent<ResourceComponent>();
			KeyComponent itemComponent4 = item.GetItemComponent<KeyComponent>();
			RepairKitComponent itemComponent5 = item.GetItemComponent<RepairKitComponent>();
			if (itemComponent != null && itemComponent.MaxHpResource > 0)
			{
				num = itemComponent.HpResource / (float)itemComponent.MaxHpResource;
			}
			else if (itemComponent2 != null && itemComponent2.MaxResource > 0f)
			{
				num = itemComponent2.HpPercent / itemComponent2.MaxResource;
			}
			else if (itemComponent3 != null && itemComponent3.MaxResource > 0f)
			{
				num = itemComponent3.Value / itemComponent3.MaxResource;
			}
			else if (itemComponent4 != null && itemComponent4.Template.MaximumNumberOfUsage > 0)
			{
				num = 1.0 - (double)itemComponent4.NumberOfUsages / (double)itemComponent4.Template.MaximumNumberOfUsage;
			}
			else if (itemComponent5 != null && TradeAccess.RepairTemplate(itemComponent5).MaxRepairResource > 0)
			{
				num = itemComponent5.Resource / (float)TradeAccess.RepairTemplate(itemComponent5).MaxRepairResource;
			}
		}
		if (!double.IsFinite(num))
		{
			return 0.0;
		}
		return (double)Math.Max(0, item.StackObjectsCount) * Math.Clamp(num, 0.0, 1.0);
	}

	internal static void Walk(Item root, Action<Item> visit, bool includeRoot = true)
	{
		InventoryTraversal.Walk(root, (Item item) => item.Id, Children, visit, includeRoot);
	}

	internal static IEnumerable<Item> Children(Item item)
	{
		ContainerCollection val = item.TryCast<ContainerCollection>();
		if (val == null)
		{
			yield break;
		}
		IEnumerator<IContainer> containers = val.Containers.GetEnumerator();
		try
		{
			while (containers.MoveNext())
			{
				if (ArmorValuation.FixedSlot(containers.Current))
				{
					continue;
				}
				IEnumerator<Item> items = containers.Current.Items.GetEnumerator();
				try
				{
					while (items.MoveNext())
					{
						yield return items.Current;
					}
				}
				finally
				{
					items.Dispose();
				}
			}
		}
		finally
		{
			containers.Dispose();
		}
	}
}
