using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using JsonType;
using Newtonsoft.Json;
using WTTClientCommonLib.Converters;

namespace WTTClientCommonLib.Models;

public class ExtendedProductionScheme : BaseHideoutScheme
{
	private bool _itemsLoaded;

	public Dictionary<string, RecipeResultStack> ResultItemStacks = new Dictionary<string, RecipeResultStack>();

	public RecipeResultStack FirstResult;

	[JsonProperty("_id")]
	public string _id { get; set; }

	[JsonProperty("endProductItems")]
	public Dictionary<string, CustomCraftResult> EndProductItems { get; set; }

	[JsonProperty("requirements")]
	[JsonConverter(typeof(RequirementArrayConverter))]
	public Requirement[] requirements { get; set; }

	public void LoadResultItems()
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (_itemsLoaded)
		{
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, CustomCraftResult> endProductItem in EndProductItems)
		{
			endProductItem.Deconstruct(out var key, out var value);
			string key2 = key;
			CustomCraftResult customCraftResult = value;
			FlatItem[] items = customCraftResult.Items;
			foreach (FlatItem val in items)
			{
				if (string.IsNullOrEmpty(val.slotId))
				{
					dictionary.Add(key2, MongoID.op_Implicit(val._tpl));
					break;
				}
			}
			foreach (KeyValuePair<string, Item> item2 in Singleton<ItemFactory>.Instance.FlatItemsToTree(customCraftResult.Items, false, (Dictionary<string, Item>)null).Items)
			{
				item2.Deconstruct(out key, out var value2);
				Item item = value2;
				if (dictionary.TryGetValue(key2, out key) && !ResultItemStacks.TryGetValue(key2, out RecipeResultStack _))
				{
					RecipeResultStack recipeResultStack = ((!customCraftResult.MinStackCount.HasValue || !customCraftResult.MaxStackCount.HasValue) ? new RecipeResultStack
					{
						Item = item,
						Count = customCraftResult.Count
					} : new RecipeResultStack
					{
						Item = item,
						Count = customCraftResult.Count,
						MinStackCount = customCraftResult.MinStackCount.Value,
						MaxStackCount = customCraftResult.MaxStackCount.Value
					});
					ResultItemStacks.Add(key2, recipeResultStack);
					if (FirstResult == null)
					{
						FirstResult = recipeResultStack;
					}
					break;
				}
			}
		}
		_itemsLoaded = true;
	}
}
