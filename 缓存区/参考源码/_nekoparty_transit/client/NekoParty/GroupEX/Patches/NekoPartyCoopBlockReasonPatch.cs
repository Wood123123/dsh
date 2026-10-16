using EFT.UI.Matchmaker;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCoopBlockReasonPatch
{
	internal static bool Prefix(ref ECoopBlock reason, ref bool __result)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		reason = (ECoopBlock)0;
		__result = true;
		return false;
	}
}
