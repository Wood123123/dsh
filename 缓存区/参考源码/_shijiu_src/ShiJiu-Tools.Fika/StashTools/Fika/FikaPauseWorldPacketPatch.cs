using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fika.Core.Networking;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseWorldPacketPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new Type[2]
		{
			typeof(FikaServer),
			typeof(FikaClient)
		}.Select((Type type) => AccessTools.DeclaredMethod(type, "OnWorldPacketReceived", (Type[])null, (Type[])null));
	}

	private static bool Prefix(object __instance, MethodBase __originalMethod, object[] __args)
	{
		return FikaPauseGameplay.DeferWorld(__instance, __originalMethod, __args);
	}
}
