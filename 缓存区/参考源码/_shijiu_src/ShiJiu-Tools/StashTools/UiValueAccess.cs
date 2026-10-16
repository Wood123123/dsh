using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

internal static class UiValueAccess
{
	private static readonly FieldRef<OverallScreen, Profile> PmcField = AccessTools.FieldRefAccess<OverallScreen, Profile>("_pmcProfile");

	private static readonly FieldRef<OtherPlayerProfileScreen, OtherPlayerProfile> OtherField = AccessTools.FieldRefAccess<OtherPlayerProfileScreen, OtherPlayerProfile>("_profile");

	private static readonly FieldRef<EquipmentTab, InventoryEquipment> EquipmentField = AccessTools.FieldRefAccess<EquipmentTab, InventoryEquipment>("_equipment");

	private static readonly FieldRef<ContainersPanel, Dictionary<EquipmentSlot, SlotView>> SlotsField = AccessTools.FieldRefAccess<ContainersPanel, Dictionary<EquipmentSlot, SlotView>>("_slotViews");

	private static readonly FieldRef<Tab, bool> SelectedField = AccessTools.FieldRefAccess<Tab, bool>("_selected");

	private static readonly FieldRef<Tab, bool> UiSelectedField = AccessTools.FieldRefAccess<Tab, bool>("_uiSelected");

	private static readonly FieldRef<Tab, Sprite> NormalField = AccessTools.FieldRefAccess<Tab, Sprite>("_normalSprite");

	private static readonly FieldRef<LocalGame, LocalRaidSettings> RaidField = AccessTools.FieldRefAccess<LocalGame, LocalRaidSettings>("_raidSettings");

	internal static Profile? Pmc(OverallScreen screen)
	{
		return PmcField.Invoke(screen);
	}

	internal static OtherPlayerProfile? Other(OtherPlayerProfileScreen screen)
	{
		return OtherField.Invoke(screen);
	}

	internal static InventoryEquipment? Equipment(EquipmentTab? tab)
	{
		if (!((Object)(object)tab == (Object)null))
		{
			return EquipmentField.Invoke(tab);
		}
		return null;
	}

	internal static Dictionary<EquipmentSlot, SlotView>? Slots(ContainersPanel? panel)
	{
		if (!((Object)(object)panel == (Object)null))
		{
			return SlotsField.Invoke(panel);
		}
		return null;
	}

	internal static bool Selected(Tab tab)
	{
		return SelectedField.Invoke(tab);
	}

	internal static bool UiSelected(Tab tab)
	{
		return UiSelectedField.Invoke(tab);
	}

	internal static Sprite? NormalSprite(Tab tab)
	{
		return NormalField.Invoke(tab);
	}

	internal static LocalRaidSettings? Raid(LocalGame game)
	{
		return RaidField.Invoke(game);
	}

	internal static bool GearActive(InventoryScreen screen)
	{
		if (screen._tabDictionary != null && screen._tabDictionary.TryGetValue((EInventoryTab)1, out var value) && (Object)(object)value != (Object)null)
		{
			return Selected(value);
		}
		return false;
	}
}
