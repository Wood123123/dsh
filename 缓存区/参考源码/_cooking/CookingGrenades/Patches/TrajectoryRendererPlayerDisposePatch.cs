using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class TrajectoryRendererPlayerDisposePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(Player).GetMethod("Dispose");
	}

	[PatchPostfix]
	public static void Postfix(Player __instance)
	{
		if (__instance.IsYourPlayer)
		{
			TrajectoryRenderer component = ((Component)__instance).gameObject.GetComponent<TrajectoryRenderer>();
			if ((Object)(object)component != (Object)null)
			{
				Object.DestroyImmediate((Object)(object)component);
				Plugin.log.LogInfo((object)"[Trajectory] Player.Dispose: TrajectoryRenderer 已销毁");
			}
		}
	}
}
