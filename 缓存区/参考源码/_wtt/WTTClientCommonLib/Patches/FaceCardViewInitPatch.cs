using System;
using System.Reflection;
using Arena.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class FaceCardViewInitPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(FaceCardView), "Init", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	private static void Postfix(FaceCardView __instance, string faceName)
	{
		if ((Object)(object)((Component)__instance).gameObject.GetComponent<CharacterAudioHandler>() != (Object)null)
		{
			LogHelper.LogDebug("Handler already exists for " + faceName + ", skipping duplicate");
			return;
		}
		string audioForFace = ResourceLoader.GetAudioForFace(faceName);
		if (string.IsNullOrEmpty(audioForFace))
		{
			LogHelper.LogDebug("No audio found for face: " + faceName);
			return;
		}
		if (!ResourceLoader.ClipCache.TryGetAudioClip(audioForFace, out AudioClip _))
		{
			LogHelper.LogDebug("Audio clip not in cache: " + audioForFace);
			return;
		}
		LogHelper.LogDebug("Creating audio handler for " + faceName + " with audio " + audioForFace);
		((Component)__instance).gameObject.AddComponent<CharacterAudioHandler>().Initialize(faceName);
	}
}
