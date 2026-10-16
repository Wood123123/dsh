using NekoParty.GroupEX.CrossRaid;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidBeaconPatch
{
	internal static void Prefix()
	{
		NekoCrossTransitRuntime.ApplyBeaconIfNeeded();
	}
}
