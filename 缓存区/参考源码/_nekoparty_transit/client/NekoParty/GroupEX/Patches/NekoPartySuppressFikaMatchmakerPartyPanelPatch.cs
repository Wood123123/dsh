namespace NekoParty.GroupEX.Patches;

public static class NekoPartySuppressFikaMatchmakerPartyPanelPatch
{
	public static bool Prefix()
	{
		return false;
	}
}
