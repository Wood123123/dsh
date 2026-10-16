using System;
using System.Reflection;
using Fika.Core.Main.Components;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseExpirationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.DeclaredMethod(typeof(FikaTimeManager), "Update", (Type[])null, (Type[])null);
	}

	private static bool Prefix()
	{
		if (!FikaPauseGameplay.Active)
		{
			return true;
		}
		try
		{
			FikaPauseGameplay.Compensate();
		}
		catch (Exception ex)
		{
			FikaPause.Fail("暂停计时补偿失败：" + ex.Message);
		}
		return !FikaPauseGameplay.Active;
	}
}
