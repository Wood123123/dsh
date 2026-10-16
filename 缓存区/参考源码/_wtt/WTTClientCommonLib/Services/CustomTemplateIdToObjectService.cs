using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Services;

public abstract class CustomTemplateIdToObjectService
{
	public static void AddNewTemplateIdToObjectMapping(List<TemplateIdToObjectType> mappings)
	{
		Type typeFromHandle = typeof(JsonTypes);
		foreach (TemplateIdToObjectType mapping in mappings)
		{
			FieldInfo field = typeFromHandle.GetField("TypeTable", BindingFlags.Static | BindingFlags.Public);
			if (field != null)
			{
				Dictionary<string, Type> dictionary = (Dictionary<string, Type>)field.GetValue(null);
				if (!dictionary.ContainsKey(mapping.TemplateId) && mapping.ItemType != null)
				{
					dictionary.Add(mapping.TemplateId, mapping.ItemType);
					LogHelper.LogDebug("Added " + mapping.ItemType.Name + " to TypeTable.");
				}
			}
			FieldInfo field2 = typeFromHandle.GetField("TemplateTypeTable", BindingFlags.Static | BindingFlags.Public);
			if (field2 != null)
			{
				Dictionary<string, Type> dictionary2 = (Dictionary<string, Type>)field2.GetValue(null);
				if (!dictionary2.ContainsKey(mapping.TemplateId))
				{
					dictionary2.Add(mapping.TemplateId, mapping.TemplateType);
					LogHelper.LogDebug("Added " + mapping.TemplateType.Name + " to TemplateTypeTable.");
				}
			}
			if (!(mapping.ItemType != null))
			{
				continue;
			}
			FieldInfo field3 = typeFromHandle.GetField("ItemConstructors", BindingFlags.Static | BindingFlags.Public);
			if (field3 != null)
			{
				Dictionary<string, Func<string, object, Item>> dictionary3 = (Dictionary<string, Func<string, object, Item>>)field3.GetValue(null);
				if (!dictionary3.ContainsKey(mapping.TemplateId))
				{
					dictionary3.Add(mapping.TemplateId, mapping.Constructor);
					LogHelper.LogDebug("Added " + mapping.ItemType.Name + " constructor to ItemConstructors.");
				}
			}
		}
	}
}
