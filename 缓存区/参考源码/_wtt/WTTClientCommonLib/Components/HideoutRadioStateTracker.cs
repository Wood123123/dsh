using UnityEngine;
using WTTClientCommonLib.Configuration;

namespace WTTClientCommonLib.Components;

public class HideoutRadioStateTracker : MonoBehaviour
{
	private const string GymParentPath = "level1";

	private const string RestSpaceParentPath = "level3";

	public RadioLocation Location { get; private set; }

	public bool HasPlayedFirstEntranceAudio { get; set; }

	public float ReplacementChance { get; set; } = 100f;

	public bool Enabled { get; set; } = true;

	public bool PlayOnFirstEntrance { get; private set; } = true;

	private void Awake()
	{
		DetermineLocation();
		ApplyConfigSettings();
	}

	public void RefreshSettings()
	{
		Enabled = GetEnabledSetting();
		ReplacementChance = GetReplacementChance();
		PlayOnFirstEntrance = GetPlayOnFirstEntrance();
		float volumeForLocation = GetVolumeForLocation();
		AudioSource[] components = ((Component)this).GetComponents<AudioSource>();
		for (int i = 0; i < components.Length; i++)
		{
			components[i].volume = volumeForLocation;
		}
	}

	private float GetVolumeForLocation()
	{
		return Location switch
		{
			RadioLocation.Gym => RadioSettings.GymRadioVolume.Value, 
			RadioLocation.RestSpace => RadioSettings.RestSpaceRadioVolume.Value, 
			_ => 1f, 
		};
	}

	private bool GetEnabledSetting()
	{
		return Location switch
		{
			RadioLocation.Gym => RadioSettings.GymEnabled.Value, 
			RadioLocation.RestSpace => RadioSettings.RestSpaceEnabled.Value, 
			_ => false, 
		};
	}

	private float GetReplacementChance()
	{
		return Location switch
		{
			RadioLocation.Gym => RadioSettings.GymReplacementChance.Value, 
			RadioLocation.RestSpace => RadioSettings.RestSpaceReplacementChance.Value, 
			_ => 100f, 
		};
	}

	private bool GetPlayOnFirstEntrance()
	{
		return Location switch
		{
			RadioLocation.Gym => RadioSettings.GymPlayOnFirstEntrance.Value, 
			RadioLocation.RestSpace => RadioSettings.RestSpacePlayOnFirstEntrance.Value, 
			_ => true, 
		};
	}

	public void ApplyConfigSettings()
	{
		switch (Location)
		{
		case RadioLocation.Gym:
			Enabled = RadioSettings.GymEnabled.Value;
			ReplacementChance = RadioSettings.GymReplacementChance.Value;
			PlayOnFirstEntrance = RadioSettings.GymPlayOnFirstEntrance.Value;
			break;
		case RadioLocation.RestSpace:
			Enabled = RadioSettings.RestSpaceEnabled.Value;
			ReplacementChance = RadioSettings.RestSpaceReplacementChance.Value;
			PlayOnFirstEntrance = RadioSettings.RestSpacePlayOnFirstEntrance.Value;
			break;
		default:
			Enabled = false;
			ReplacementChance = 0f;
			PlayOnFirstEntrance = false;
			break;
		}
	}

	private void DetermineLocation()
	{
		Transform parent = ((Component)this).transform.parent;
		int num = 0;
		while ((Object)(object)parent != (Object)null && num < 5)
		{
			if (((Object)parent).name.Contains("level1"))
			{
				Location = RadioLocation.Gym;
				return;
			}
			if (((Object)parent).name.Contains("level3"))
			{
				Location = RadioLocation.RestSpace;
				return;
			}
			parent = parent.parent;
			num++;
		}
		Location = RadioLocation.Unknown;
	}
}
