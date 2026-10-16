using Fika.Core.Main.Utils;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(FikaBackendUtils), "JoinMatch")]
public static class NekoPartySyncPreferredSideOnJoinPatch
{
	private static void Prefix()
	{
		NekoPartyMixedSideRuntime.SyncCurrentPreferredSideBestEffort();
	}
}
