using System;
using System.Reflection;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.Patches;

public class ProduceViewLoadedPatch : ModulePatch
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
	private static void PatchPostfix(ProduceView __instance, HideoutItemViewFactory ____resultItemIconViewFactory)
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		ExtendedRecipeLoader instance = ExtendedRecipeLoader.Instance;
		string id = ((BaseHideoutScheme)((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).Scheme)._id;
		ExtendedProductionScheme extendedScheme = instance.GetExtendedScheme(id);
		if (extendedScheme != null)
		{
			RecipeResultStack firstResult = extendedScheme.FirstResult;
			____resultItemIconViewFactory.Show(firstResult.Item, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).InventoryController, ((ProduceViewBase<ItemsProducerBase, ProductionScheme>)(object)__instance).ItemUiContext);
			if (firstResult.MinStackCount >= 1 && firstResult.MaxStackCount > firstResult.MinStackCount)
			{
				____resultItemIconViewFactory.SetCounterText(StackCountDisplayHelper.GetShortBalance(firstResult.MinStackCount.Value, ((object)firstResult.Item.TemplateId/*cast due to constrained. prefix*/).ToString()) + " - " + StackCountDisplayHelper.GetShortBalance(firstResult.MaxStackCount.Value, ((object)firstResult.Item.TemplateId/*cast due to constrained. prefix*/).ToString()));
				____resultItemIconViewFactory.ShowInfo(true, false);
			}
			else
			{
				____resultItemIconViewFactory.SetCounterText(extendedScheme.FirstResult.Count.ToString());
				____resultItemIconViewFactory.ShowInfo(extendedScheme.FirstResult.Count > 1, false);
			}
		}
	}
}
