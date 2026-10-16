using System.Collections.Generic;
using System.Linq;
using Diz.Binding;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace StashTools;

internal static class Selection
{
	internal static readonly Dictionary<string, Item> Items = new Dictionary<string, Item>();

	internal static int Count => Items.Count;

	internal static bool Contains(Item? i)
	{
		if (i != null)
		{
			return Items.ContainsKey(i.Id);
		}
		return false;
	}

	internal static void Clear()
	{
		Items.Clear();
	}

	internal static void Toggle(Item i)
	{
		if (!Items.Remove(i.Id))
		{
			Items[i.Id] = i;
		}
	}

	internal static Item[] ItemsFor(Item i)
	{
		if (!Contains(i))
		{
			return (Item[])(object)new Item[1] { i };
		}
		return Items.Values.ToArray();
	}

	internal static bool Owned(Item? i)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		if (i != null && i.Owner != null)
		{
			return (int)i.Owner.OwnerType == 0;
		}
		return false;
	}

	internal static bool Accessible(Item? i)
	{
		if (i != null && i.Owner != null)
		{
			if (!Owned(i))
			{
				return StashBehaviour.InRaid;
			}
			return true;
		}
		return false;
	}

	internal static bool Allowed(GridItemView? v)
	{
		if ((Object)(object)v != (Object)null && !InventoryAccess.Killed((ItemView)(object)v) && ((Component)v).gameObject.activeInHierarchy && ((ItemView)v).IsSearched && ((ItemView)v).IsInteractable && TraderSelection.Selectable(v) && Accessible(((ItemView)v).Item) && ((ItemView)v).ItemContext != null && (((ItemView)v).RemoveError == null || ((BaseBindable<Error>)(object)((ItemView)v).RemoveError).Value == null) && (!InventoryAccess.InRaid(v) || Plugin.SelectInRaid.Value))
		{
			return StashBehaviour.InInventoryArea(((Component)v).gameObject);
		}
		return false;
	}
}
