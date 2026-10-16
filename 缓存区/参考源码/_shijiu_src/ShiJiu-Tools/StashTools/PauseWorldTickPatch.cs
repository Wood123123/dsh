using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class PauseWorldTickPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new string[3] { "DoWorldTick", "DoOtherWorldTick", "LateUpdateWorld" }.Select((string name) => typeof(GameWorld).GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null) ?? throw new MissingMethodException(name));
	}

	private static bool Prefix(GameWorld __instance)
	{
		if (!SinglePlayerPause.Owns(__instance))
		{
			return !RaidPause.CoopPaused;
		}
		return false;
	}
}
