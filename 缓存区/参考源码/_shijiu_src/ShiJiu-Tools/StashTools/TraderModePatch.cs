using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(TraderDealScreen), "TraderModeChanged")]
internal static class TraderModePatch
{
	private static void Prefix()
	{
		Selection.Clear();
		SelectionVisuals.Clear();
		StashBehaviour.Drawing = false;
		Batch.Cancel("买卖模式已切换。");
	}
}
