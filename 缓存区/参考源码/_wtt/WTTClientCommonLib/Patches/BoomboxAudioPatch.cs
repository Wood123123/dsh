using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Patches;

internal class BoomboxAudioPatch : ModulePatch
{
	private static readonly Random _random = new Random();

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(AudioArray), "PlayWithOffset", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	private static bool Prefix(AudioArray __instance, ref AudioClip sound, out AudioClip __state)
	{
		__state = null;
		try
		{
			if (((Object)((Component)__instance).gameObject).name != "BoomboxAudio")
			{
				return true;
			}
			HideoutRadioStateTracker hideoutRadioStateTracker = ((Component)__instance).gameObject.GetComponent<HideoutRadioStateTracker>();
			if ((Object)(object)hideoutRadioStateTracker == (Object)null)
			{
				hideoutRadioStateTracker = ((Component)__instance).gameObject.AddComponent<HideoutRadioStateTracker>();
			}
			if (ShouldSkipPlayback(hideoutRadioStateTracker))
			{
				return true;
			}
			if (!TryGetAudioResource(hideoutRadioStateTracker, out string audioResource))
			{
				return true;
			}
			if (!ResourceLoader.ClipCache.TryGetAudioClip(audioResource, out AudioClip clip))
			{
				LogHelper.LogWarn("Audio clip not found in cache: " + audioResource);
				return true;
			}
			__state = sound;
			sound = clip;
			hideoutRadioStateTracker.HasPlayedFirstEntranceAudio = true;
			float volumeForLocation = GetVolumeForLocation(hideoutRadioStateTracker.Location);
			((Component)__instance).GetComponent<AudioSource>().volume = volumeForLocation;
			return true;
		}
		catch (Exception)
		{
			return true;
		}
	}

	[PatchPostfix]
	private static void Postfix(AudioArray __instance, AudioClip sound, AudioClip __state)
	{
		if ((Object)(object)__state == (Object)null || (Object)(object)sound == (Object)(object)__state)
		{
			return;
		}
		try
		{
			if ((Object)(object)((Component)__instance).gameObject.GetComponent<HideoutRadioStateTracker>() == (Object)null)
			{
				return;
			}
			AudioSource[] components = ((Component)__instance).GetComponents<AudioSource>();
			foreach (AudioSource val in components)
			{
				if ((Object)(object)val.clip == (Object)(object)sound)
				{
					((Component)val).gameObject.AddComponent<RevertClipComponent>().Initialize(val, __state, delegate
					{
					});
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static float GetVolumeForLocation(RadioLocation location)
	{
		return location switch
		{
			RadioLocation.Gym => RadioSettings.GymRadioVolume.Value, 
			RadioLocation.RestSpace => RadioSettings.RestSpaceRadioVolume.Value, 
			_ => 1f, 
		};
	}

	private static bool ShouldSkipPlayback(HideoutRadioStateTracker tracker)
	{
		if (!tracker.Enabled)
		{
			return true;
		}
		if (tracker.PlayOnFirstEntrance && !tracker.HasPlayedFirstEntranceAudio)
		{
			return false;
		}
		if ((float)_random.Next(0, 100) > tracker.ReplacementChance)
		{
			return true;
		}
		return false;
	}

	private static bool TryGetAudioResource(HideoutRadioStateTracker tracker, out string audioResource)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		audioResource = null;
		Player myPlayer = GamePlayerOwner.MyPlayer;
		if (tracker.PlayOnFirstEntrance && !tracker.HasPlayedFirstEntranceAudio && ((myPlayer != null) ? myPlayer.Profile : null) != null)
		{
			string text = LocalizationExtensions.LocalizedName(((Dictionary<EBodyModelPart, MongoID>)(object)myPlayer.Profile.Customization).TryGetValue((EBodyModelPart)2, out MongoID value) ? value : MongoID.op_Implicit((string)null));
			if (!string.IsNullOrEmpty(text))
			{
				string audioForFace = ResourceLoader.GetAudioForFace(text);
				if (!string.IsNullOrEmpty(audioForFace))
				{
					audioResource = audioForFace;
					return true;
				}
			}
		}
		List<string> radioPool = new List<string>(ResourceLoader.GetRadioAudio());
		if (((myPlayer != null) ? myPlayer.Profile : null) != null)
		{
			string text2 = LocalizationExtensions.LocalizedName(((Dictionary<EBodyModelPart, MongoID>)(object)myPlayer.Profile.Customization).TryGetValue((EBodyModelPart)2, out MongoID value2) ? value2 : MongoID.op_Implicit((string)null));
			if (!string.IsNullOrEmpty(text2) && ResourceLoader.Manifest.FaceCardMappings.TryGetValue(text2, out FaceCardAudioEntry value3) && value3.PlayOnRadio)
			{
				List<string> audio = value3.Audio;
				if (audio != null && audio.Count > 0 && !value3.Audio.Any((string audioKey) => radioPool.Contains(audioKey)))
				{
					radioPool.AddRange(value3.Audio);
				}
			}
		}
		if (radioPool.Count > 0)
		{
			audioResource = radioPool[Random.Range(0, radioPool.Count)];
			return true;
		}
		return false;
	}
}
