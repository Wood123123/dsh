using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class TrajectoryRendererGameWorldStartedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(GameWorld).GetMethod("OnGameStarted");
	}

	[PatchPostfix]
	public static void Postfix(GameWorld __instance)
	{
		Player mainPlayer = __instance.MainPlayer;
		if ((Object)(object)mainPlayer == (Object)null)
		{
			Plugin.log.LogWarning((object)"[Trajectory] GameWorld.OnGameStarted: MainPlayer is null");
		}
		else if ((Object)(object)((Component)mainPlayer).gameObject.GetComponent<TrajectoryRenderer>() == (Object)null)
		{
			((Component)mainPlayer).gameObject.AddComponent<TrajectoryRenderer>();
			Plugin.log.LogInfo((object)("[Trajectory] 已挂载到 MainPlayer (" + mainPlayer.Profile.Nickname + ")"));
		}
		else
		{
			Plugin.log.LogInfo((object)"[Trajectory] 组件已存在，跳过挂载");
		}
	}
}
