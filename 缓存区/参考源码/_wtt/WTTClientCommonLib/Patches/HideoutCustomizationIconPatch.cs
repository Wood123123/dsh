using System;
using System.Reflection;
using EFT.Hideout;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class HideoutCustomizationIconPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(HideoutCustomizationIcons), "GetSprite", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	private static bool Prefix(string id, ref Sprite __result, HideoutCustomizationIcons __instance)
	{
		if (ResourceLoader._customHideoutIcons.TryGetValue(id, out Sprite value))
		{
			LogHelper.LogDebug("[HideoutIcons] Using custom sprite for: " + id);
			__result = value;
			return false;
		}
		return true;
	}
}
