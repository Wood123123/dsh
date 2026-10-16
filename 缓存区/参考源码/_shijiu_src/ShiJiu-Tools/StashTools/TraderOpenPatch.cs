using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(TraderDealScreen), "Show")]
internal static class TraderOpenPatch
{
	private static void Postfix(TraderDealScreen __instance)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		MarketQuotes.Resume();
		TraderSelection.Reset();
		TraderSelection.Screen = __instance;
		ChineseSettings.FindManager();
		Plugin.Logger.LogInfo((object)$"商人界面已打开：商人={InventoryAccess.Trader(__instance)?.Id}，模式={__instance.TradeMode}");
	}
}
