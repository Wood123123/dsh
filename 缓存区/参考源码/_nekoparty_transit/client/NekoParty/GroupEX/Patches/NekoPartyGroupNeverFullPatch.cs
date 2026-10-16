namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyGroupNeverFullPatch
{
	internal static bool Prefix(ref bool __result)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
