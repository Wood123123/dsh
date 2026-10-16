using EFT.UI.Matchmaker;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyScavSideUnlockPatch
{
	internal static void Postfix(ref EScavLockReason __result)
	{
		if (NekoGroupExConfig.UseNativePartyInvite && (int)__result == 4)
		{
			__result = (EScavLockReason)0;
		}
	}
}
