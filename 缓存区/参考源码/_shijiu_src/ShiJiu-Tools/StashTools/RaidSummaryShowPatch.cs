using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI.SessionEnd;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class RaidSummaryShowPatch
{
	private static MethodBase TargetMethod()
	{
		return typeof(SessionResultExitStatus).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).Single((MethodInfo m) => m.Name == "Show" && m.GetParameters().Length == 7 && m.GetParameters()[0].ParameterType == typeof(Profile));
	}

	private static void Postfix(SessionResultExitStatus __instance, Profile __0)
	{
		try
		{
			RaidEarnings.Reconcile(__0);
			RaidSummary.Open(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError((object)("结算显示初始化失败：" + ex));
		}
	}
}
