using Fika.Core.Main.Utils;
using NekoParty.GroupEX.State;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidConnectionLedgerPatch
{
	internal static void Postfix()
	{
		string text = FikaBackendUtils.GroupId + ":" + FikaBackendUtils.RaidCode;
		if (NekoGroupState.ConnectedRaidKeys.Count > 64)
		{
			NekoGroupState.ConnectedRaidKeys.Clear();
		}
		if (NekoGroupState.ConnectedRaidKeys.Add(text))
		{
			FikaGlobals.LogInfo("[NekoCrossRaid] 已记录战局连接 " + text, "Postfix");
		}
	}
}
