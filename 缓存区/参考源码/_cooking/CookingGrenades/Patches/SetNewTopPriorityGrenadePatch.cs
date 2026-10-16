using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class SetNewTopPriorityGrenadePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(FastAccessGrenadeItemView), "SetNewTopPriorityGrenade", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void PatchPostfix(FastAccessGrenadeItemView __instance)
	{
		try
		{
			if (string.IsNullOrEmpty(GrenadeWheel.PreferredTemplateId) || !ConfigManager.EnableGrenadeWheel.Value)
			{
				return;
			}
			InventoryController value = Traverse.Create((object)__instance).Field("InventoryController").GetValue<InventoryController>();
			if (value == null)
			{
				return;
			}
			InventoryEquipment val = value.Inventory?.Equipment;
			if (val == null)
			{
				return;
			}
			List<ThrowWeap> throwablePriorityGrenadesList = InventoryExtension.GetThrowablePriorityGrenadesList(value);
			if (throwablePriorityGrenadesList == null || throwablePriorityGrenadesList.Count == 0)
			{
				return;
			}
			ThrowWeap val2 = throwablePriorityGrenadesList.FirstOrDefault((ThrowWeap tw) => ((Item)tw).StringTemplateId == GrenadeWheel.PreferredTemplateId);
			if (val2 != null && val.TopPriorityGrenade != val2)
			{
				val.TopPriorityGrenade = val2;
				Plugin.log.LogInfo((object)("[SetNewTopPriorityGrenadePatch] 投雷后重新锁定偏好模板: " + LocalizationExtensions.Localized(((Item)val2).ShortName, (string)null)));
				ItemUiContext value2 = Traverse.Create((object)__instance).Field("ItemUiContext").GetValue<ItemUiContext>();
				if ((Object)(object)value2 != (Object)null)
				{
					((QuickSlotView)__instance).RemoveItemView();
					((QuickSlotView)__instance).SetItem((Item)(object)val2, value, value2);
					((QuickSlotView)__instance).ShowInfoPanel((Item)(object)val2);
				}
				Traverse.Create((object)__instance).Field("Item").SetValue((object)val2);
			}
		}
		catch (Exception ex)
		{
			Plugin.log.LogError((object)("[SetNewTopPriorityGrenadePatch] " + ex.Message));
		}
	}
}
