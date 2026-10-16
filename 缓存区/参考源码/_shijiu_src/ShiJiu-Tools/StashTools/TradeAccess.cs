using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using HarmonyLib;

namespace StashTools;

internal static class TradeAccess
{
	private static readonly FieldRef<Trader, SupplyData> SupplyField = AccessTools.FieldRefAccess<Trader, SupplyData>("_supplyData");

	private static readonly FieldRef<ItemUiContext, Profile> ProfileField = AccessTools.FieldRefAccess<ItemUiContext, Profile>("_profile");

	private static readonly FieldRef<RepairKitComponent, RepairKitTemplate> RepairField = AccessTools.FieldRefAccess<RepairKitComponent, RepairKitTemplate>("_template");

	internal static SupplyData? Supply(Trader trader)
	{
		return SupplyField.Invoke(trader);
	}

	internal static void SetSupply(Trader trader, SupplyData value)
	{
		SupplyField.Invoke(trader) = value;
	}

	internal static Profile Profile(ItemUiContext ui)
	{
		return ProfileField.Invoke(ui);
	}

	internal static RepairKitTemplate RepairTemplate(RepairKitComponent kit)
	{
		return RepairField.Invoke(kit);
	}
}
