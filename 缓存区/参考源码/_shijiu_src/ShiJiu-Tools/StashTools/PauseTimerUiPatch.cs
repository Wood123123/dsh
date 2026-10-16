using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI.BattleTimer;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class PauseTimerUiPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new Type[2]
		{
			typeof(TimerPanel),
			typeof(MainTimerPanel)
		}.Select((Type t) => t.GetMethod("UpdateTimer", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) ?? throw new MissingMethodException(t.FullName, "UpdateTimer"));
	}

	private static bool Prefix()
	{
		if (!SinglePlayerPause.Paused)
		{
			return !RaidPause.CoopPaused;
		}
		return false;
	}
}
