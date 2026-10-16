using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class HistoryOtherPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return from m in typeof(OtherPlayerProfileScreen).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
			where m.Name == "Show" && (m.GetParameters().Length == 1 || m.GetParameters().Length == 4)
			select m;
	}

	private static void Postfix(OtherPlayerProfileScreen __instance)
	{
		try
		{
			HistoryUi.Other(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("好友战绩入口不可用：" + ex));
		}
	}
}
