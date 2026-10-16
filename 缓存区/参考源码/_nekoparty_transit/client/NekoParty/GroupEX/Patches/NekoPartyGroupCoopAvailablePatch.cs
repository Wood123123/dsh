namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyGroupCoopAvailablePatch
{
	internal static bool Prefix(ref bool __result)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		__result = true;
		return false;
	}
}
