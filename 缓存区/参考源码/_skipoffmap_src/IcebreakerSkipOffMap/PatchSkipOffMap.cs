using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace IcebreakerSkipOffMap;

public class PatchSkipOffMap : ModulePatch
{
	private static bool _loggedSkip;

	protected override MethodBase GetTargetMethod()
	{
		Type type = AccessTools.TypeByName("Manimal.Icebreaker.RenderEnvProbe");
		if (type == null)
		{
			throw new Exception("Manimal.Icebreaker.RenderEnvProbe not found");
		}
		MethodInfo methodInfo = AccessTools.Method(type, "Update", (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			throw new Exception("RenderEnvProbe.Update not found");
		}
		return methodInfo;
	}

	[PatchPrefix]
	public static bool PatchPrefix()
	{
		if (IceGateState.On)
		{
			return true;
		}
		if (!_loggedSkip)
		{
			_loggedSkip = true;
			Plugin.Log.LogInfo((object)"[SkipOffMap] off-map: RenderEnvProbe.Update skipped");
		}
		return false;
	}
}
