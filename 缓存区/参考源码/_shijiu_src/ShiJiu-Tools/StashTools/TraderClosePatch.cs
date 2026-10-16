using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(TraderDealScreen), "Close")]
internal static class TraderClosePatch
{
	private static void Prefix()
	{
		TraderSelection.Reset();
		HoverPrices.Suspend();
	}
}
