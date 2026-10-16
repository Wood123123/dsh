using System.Reflection;
using System.Threading;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidServerDestroyPatch
{
	internal static void Postfix(object __instance)
	{
		try
		{
			(__instance?.GetType().GetField("_cts", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(__instance) as CancellationTokenSource)?.Cancel();
		}
		catch
		{
		}
	}
}
