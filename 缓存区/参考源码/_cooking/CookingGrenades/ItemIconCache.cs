using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace CookingGrenades;

public static class ItemIconCache
{
	private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

	public static async Task<Sprite> GetOrLoadAsync(Item item, string templateId)
	{
		if (!string.IsNullOrEmpty(templateId) && _cache.TryGetValue(templateId, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (item == null)
		{
			return null;
		}
		try
		{
			Sprite val = await ItemViewFactory.GetItemSpriteAsync(item, 1);
			if ((Object)(object)val != (Object)null && !string.IsNullOrEmpty(templateId))
			{
				_cache[templateId] = val;
			}
			return val;
		}
		catch (Exception ex)
		{
			Plugin.log.LogWarning((object)("[ItemIconCache] 图标加载失败 " + templateId + ": " + ex.Message));
			return null;
		}
	}
}
