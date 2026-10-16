using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.UI;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.Patches;

public class ProduceViewShowPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ProduceView), "Show", new Type[7]
		{
			typeof(ItemUiContext),
			typeof(InventoryController),
			typeof(ProductionScheme),
			typeof(ItemsProducerBase),
			typeof(Action<string>),
			typeof(Action<string>),
			typeof(bool)
		}, (Type[])null);
	}

	[PatchPostfix]
	private static void PatchPostfix(ProduceView __instance, ProductionScheme scheme, HideoutItemViewFactory ____resultItemIconViewFactory)
	{
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Expected O, but got Unknown
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		ExtendedRecipeLoader instance = ExtendedRecipeLoader.Instance;
		string id = ((BaseHideoutScheme)scheme)._id;
		ExtendedProductionScheme extendedScheme = instance.GetExtendedScheme(id);
		if (extendedScheme == null)
		{
			return;
		}
		int num = 0;
		List<RecipeResultStack> list = new List<RecipeResultStack>();
		foreach (RecipeResultStack value in extendedScheme.ResultItemStacks.Values)
		{
			list.Add(value);
		}
		RecipeResultStack firstResult = extendedScheme.FirstResult;
		____resultItemIconViewFactory.Show(firstResult.Item, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).InventoryController, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).ItemUiContext);
		if (firstResult.MinStackCount >= 1 && firstResult.MinStackCount < firstResult.MaxStackCount)
		{
			____resultItemIconViewFactory.SetCounterText(StackCountDisplayHelper.GetShortBalance(firstResult.MinStackCount.Value, ((object)firstResult.Item.TemplateId/*cast due to constrained. prefix*/).ToString()) + " - " + StackCountDisplayHelper.GetShortBalance(firstResult.MaxStackCount.Value, ((object)firstResult.Item.TemplateId/*cast due to constrained. prefix*/).ToString()));
			____resultItemIconViewFactory.ShowInfo(true, false);
		}
		else if (firstResult.Count > 1)
		{
			____resultItemIconViewFactory.SetCounterText(extendedScheme.FirstResult.Count.ToString());
			____resultItemIconViewFactory.ShowInfo(true, false);
		}
		else
		{
			____resultItemIconViewFactory.ShowInfo(false, false);
		}
		num++;
		int count = extendedScheme.EndProductItems.Count;
		if (num == count)
		{
			return;
		}
		int num2 = count - num;
		((HorizontalOrVerticalLayoutGroup)((Component)__instance).gameObject.GetComponent<HorizontalLayoutGroup>()).childScaleHeight = true;
		GameObject val = new GameObject("CraftResults");
		val.transform.parent = ((Component)__instance).transform;
		val.transform.SetSiblingIndex(val.transform.GetSiblingIndex() - 1);
		HorizontalLayoutGroup obj = val.AddComponent<HorizontalLayoutGroup>();
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = false;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = false;
		((HorizontalOrVerticalLayoutGroup)obj).spacing = 10f;
		((Component)____resultItemIconViewFactory).transform.parent = val.transform;
		List<GameObject> list2 = new List<GameObject>();
		for (int i = 0; i < num2; i++)
		{
			GameObject item = Object.Instantiate<GameObject>(((Component)____resultItemIconViewFactory).gameObject, val.transform, true);
			list2.Add(item);
		}
		foreach (GameObject item2 in list2)
		{
			HideoutItemViewFactory component = item2.GetComponent<HideoutItemViewFactory>();
			foreach (Transform child in GameObjectExtensions.GetChildren(((Component)component).transform.Find("ItemViewContainer")))
			{
				if (((Object)((Component)child).gameObject).name.Contains("hideout_layout"))
				{
					Object.Destroy((Object)(object)((Component)child).gameObject);
				}
			}
			RecipeResultStack recipeResultStack = list[num];
			component.Show(recipeResultStack.Item, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).InventoryController, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).ItemUiContext);
			if (recipeResultStack.MinStackCount >= 1 && recipeResultStack.MaxStackCount > recipeResultStack.MinStackCount)
			{
				____resultItemIconViewFactory.SetCounterText(StackCountDisplayHelper.GetShortBalance(recipeResultStack.MinStackCount.Value, ((object)recipeResultStack.Item.TemplateId/*cast due to constrained. prefix*/).ToString()) + " - " + StackCountDisplayHelper.GetShortBalance(recipeResultStack.MaxStackCount.Value, ((object)recipeResultStack.Item.TemplateId/*cast due to constrained. prefix*/).ToString()));
				component.ShowInfo(true, false);
			}
			else if (recipeResultStack.Count > 1)
			{
				component.SetCounterText(recipeResultStack.Count.ToString());
				component.ShowInfo(true, false);
			}
			else
			{
				component.ShowInfo(false, false);
			}
			num++;
		}
	}
}
