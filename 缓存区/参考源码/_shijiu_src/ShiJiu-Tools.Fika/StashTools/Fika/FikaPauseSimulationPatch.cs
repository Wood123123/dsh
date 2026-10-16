using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseSimulationPatch
{
	internal static readonly (string Type, string Method)[] Targets = new(string, string)[11]
	{
		("Fika.Core.Main.Players.FikaPlayer", "ManualUpdate"),
		("Fika.Core.Main.Players.ObservedPlayer", "ManualUpdate"),
		("Fika.Core.Main.Players.ObservedPlayer", "ManualStateUpdate"),
		("Fika.Core.Main.Players.FikaPlayer", "HealthControllerUpdate"),
		("Fika.Core.Main.Players.ObservedPlayer", "HealthControllerUpdate"),
		("Fika.Core.Main.Components.BotStateManager", "Update"),
		("Fika.Core.Main.Components.FikaExfilManager", "Update"),
		("Fika.Core.Main.Components.Bleedout", "Update"),
		("Fika.Core.Main.HostClasses.FikaHostWorld", "Update"),
		("Fika.Core.Main.ClientClasses.FikaClientWorld", "Update"),
		("Fika.Core.Main.ClientClasses.FikaClientWorld", "LateUpdate")
	};

	private static IEnumerable<MethodBase> TargetMethods()
	{
		(string Type, string Method)[] targets = Targets;
		for (int i = 0; i < targets.Length; i++)
		{
			var (text, text2) = targets[i];
			yield return AccessTools.DeclaredMethod(AccessTools.TypeByName(text) ?? throw new TypeLoadException(text), text2, (Type[])null, (Type[])null) ?? throw new MissingMethodException(text, text2);
		}
		yield return AccessTools.DeclaredMethod(typeof(BotsController), "UpdateByUnity", (Type[])null, (Type[])null);
	}

	private static bool Prefix()
	{
		return !FikaPauseGameplay.Active;
	}
}
