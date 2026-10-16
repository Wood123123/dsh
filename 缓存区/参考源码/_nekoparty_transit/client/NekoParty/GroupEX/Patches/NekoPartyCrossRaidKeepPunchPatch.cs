using Fika.Core.Main.Utils;
using NekoParty.GroupEX.CrossRaid;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidKeepPunchPatch
{
	internal static bool Prefix()
	{
		if (!NekoCrossTransitRuntime.ShouldUseCrossTransit())
		{
			return true;
		}
		FikaGlobals.LogInfo("[NekoCrossRaid] 跨服战局：保持 NAT 心跳（跳过 StopNatIntroduceRoutine）。", "Prefix");
		return false;
	}
}
