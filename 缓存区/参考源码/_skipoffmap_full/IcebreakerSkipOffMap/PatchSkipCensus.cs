using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace IcebreakerSkipOffMap;

public class PatchSkipCensus : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type type = AccessTools.TypeByName("Manimal.Icebreaker.RenderEnvProbe");
		if (type == null)
		{
			throw new Exception("Manimal.Icebreaker.RenderEnvProbe not found");
		}
		MethodInfo methodInfo = AccessTools.Method(type, "TickVanillaPerfCensus", (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			throw new Exception("RenderEnvProbe.TickVanillaPerfCensus not found");
		}
		return methodInfo;
	}

	[PatchPrefix]
	public static bool PatchPrefix()
	{
		return IceGateState.On;
	}
}
