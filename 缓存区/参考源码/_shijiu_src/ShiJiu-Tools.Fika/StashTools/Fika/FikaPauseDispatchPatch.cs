using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fika.Core.Networking;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseDispatchPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new Type[2]
		{
			typeof(FikaServer),
			typeof(FikaClient)
		}.SelectMany((Type type) => new string[3] { "HandlePlayerPacket", "HandleFirearmPacket", "HandleGenericPacket" }.Select((string name) => AccessTools.DeclaredMethod(type, name, (Type[])null, (Type[])null)));
	}

	private static bool Prefix(object __instance, MethodBase __originalMethod, object[] __args)
	{
		return FikaPauseGameplay.DeferDispatch(__instance, __originalMethod, __args);
	}
}
