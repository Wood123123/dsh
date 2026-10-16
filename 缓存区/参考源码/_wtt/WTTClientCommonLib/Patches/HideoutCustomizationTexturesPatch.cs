using System;
using System.Reflection;
using EFT;
using EFT.Hideout;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class HideoutCustomizationTexturesPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(HideoutCustomizationController), "InstallCustomization", new Type[2]
		{
			typeof(ResourceKey),
			typeof(EHideoutCustomizationType)
		}, (Type[])null);
	}

	[PatchPrefix]
	private static void Prefix(ResourceKey resourceKey, EHideoutCustomizationType customizationType, HideoutCustomizationController __instance)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		if ((int)customizationType != 4)
		{
			return;
		}
		string text = resourceKey.ToAssetName();
		if (text == null || !ResourceLoader._customMarkTextures.TryGetValue(text, out Texture2D value))
		{
			return;
		}
		try
		{
			HideoutCustomizationIcons icons = __instance.Icons;
			if ((Object)(object)icons != (Object)null)
			{
				icons.ShootingRangeMarkTextures[text] = (Texture)(object)value;
			}
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"[HideoutTextures] Error injecting texture: {arg}");
		}
	}
}
