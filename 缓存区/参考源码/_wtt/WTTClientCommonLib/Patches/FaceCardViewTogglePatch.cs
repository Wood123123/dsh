using System;
using System.Reflection;
using Arena.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class FaceCardViewTogglePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(FaceCardView), "SetSelected", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	private static void Postfix(FaceCardView __instance, bool isSelected)
	{
		CharacterAudioHandler component = ((Component)__instance).gameObject.GetComponent<CharacterAudioHandler>();
		if ((Object)(object)component == (Object)null)
		{
			LogHelper.LogDebug("No CharacterAudioHandler found on " + ((Object)((Component)__instance).gameObject).name);
			return;
		}
		LogHelper.LogDebug($"FaceCard toggle: {((Object)((Component)__instance).gameObject).name}, Selected: {isSelected}");
		if (isSelected)
		{
			LogHelper.LogDebug("Fading in audio...");
			component.FadeIn();
		}
		else
		{
			LogHelper.LogDebug("Fading out audio...");
			component.FadeOut();
		}
	}
}
