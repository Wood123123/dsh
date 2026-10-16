using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using WTTClientCommonLib.Attributes;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Services;

public class CustomItemParentLoader
{
	public static CustomItemParentLoader Instance;

	private Dictionary<string, ItemTemplate> _customParents = new Dictionary<string, ItemTemplate>();

	private bool _parentsRegistered;

	public CustomItemParentLoader()
	{
		if (Instance == null)
		{
			Instance = this;
		}
	}

	public void FetchCustomParentsFromServer()
	{
		try
		{
			Dictionary<string, ItemTemplate> dictionary = Utils.Get<Dictionary<string, ItemTemplate>>("/wttcommonlib/parents/custom/get");
			if (dictionary != null)
			{
				_customParents = dictionary;
			}
			else
			{
				LogHelper.LogError("Failed to fetch custom parent data from server");
			}
		}
		catch (Exception ex)
		{
			LogHelper.LogError(ex.ToString());
			throw;
		}
	}

	public async Task RegisterCustomParents()
	{
		if (_parentsRegistered)
		{
			return;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		List<CustomParent> list = new List<CustomParent>();
		List<TemplateIdToObjectType> list2 = new List<TemplateIdToObjectType>();
		Assembly[] array = assemblies;
		for (int i = 0; i < array.Length; i++)
		{
			Type[] types = array[i].GetTypes();
			for (int j = 0; j < types.Length; j++)
			{
				object[] customAttributes = types[j].GetCustomAttributes(typeof(CustomParent), inherit: true);
				for (int k = 0; k < customAttributes.Length; k++)
				{
					CustomParent item = (CustomParent)customAttributes[k];
					list.Add(item);
				}
			}
		}
		foreach (CustomParent parent in list)
		{
			if (parent.Item == null)
			{
				list2.Add(new TemplateIdToObjectType(parent.ParentId, null, parent.Template, null));
				continue;
			}
			ConstructorInfo ctor = ((parent.Template == null) ? parent.Item.GetConstructor(new Type[1] { typeof(string) }) : parent.Item.GetConstructor(new Type[2]
			{
				typeof(string),
				parent.Template
			}));
			if (ctor == null)
			{
				LogHelper.LogError($"Could not find valid constructor for {parent.Item}");
				continue;
			}
			list2.Add(new TemplateIdToObjectType(parent.ParentId, parent.Item, parent.Template, delegate(string id, object tpl)
			{
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_0049: Expected O, but got Unknown
				return (Item)((parent.Template == null) ? ctor.Invoke(new object[1] { id }) : ctor.Invoke(new object[2] { id, tpl }));
			}));
		}
		CustomTemplateIdToObjectService.AddNewTemplateIdToObjectMapping(list2);
		_parentsRegistered = true;
	}
}
