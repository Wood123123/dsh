using System;
using System.Collections.Generic;
using System.Reflection;
using Fika.Core.Main.GameMode;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseEndPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new MethodInfo[2]
		{
			AccessTools.DeclaredMethod(typeof(CoopGame), "Stop", (Type[])null, (Type[])null),
			AccessTools.DeclaredMethod(typeof(CoopGame), "Dispose", (Type[])null, (Type[])null)
		};
	}

	[HarmonyPriority(800)]
	private static void Prefix()
	{
		if (FikaPauseGameplay.Active)
		{
			FikaPause.Fail("战局结束，取消暂停");
		}
		else
		{
			RaidPause.End();
		}
	}
}
