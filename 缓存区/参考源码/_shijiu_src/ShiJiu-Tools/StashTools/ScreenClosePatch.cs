using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(InventoryScreen), "Close")]
internal static class ScreenClosePatch
{
	private static void Prefix()
	{
		StashBehaviour.Close();
	}
}
