using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.UI;
using Newtonsoft.Json;
using SPT.Common.Http;
using UnityEngine;
using UnityEngine.Rendering;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Helpers;

internal static class Utils
{
	private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");

	private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

	private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

	public static Vector3? GetPlayerPosition()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (!Object.op_Implicit((Object)(object)Singleton<GameWorld>.Instance.MainPlayer))
		{
			ConsoleScreen.Log("Player is null, or you are not ingame.");
			return null;
		}
		return ((IPlayer)Singleton<GameWorld>.Instance.MainPlayer).Position;
	}

	public static string? GetLocationId()
	{
		if (!Object.op_Implicit((Object)(object)Singleton<GameWorld>.Instance))
		{
			ConsoleScreen.Log("Gameworld is null.");
			return null;
		}
		return Singleton<GameWorld>.Instance.LocationId;
	}

	public static T? Get<T>(string url)
	{
		try
		{
			string json = RequestHandler.GetJson(url);
			if (string.IsNullOrWhiteSpace(json) || json == "null" || (json.TrimStart().StartsWith("{") && (json.Contains("\"err\"") || json.Contains("\"error\""))))
			{
				LogHelper.LogError("Invalid/empty/error response from " + url + ": " + json);
				return default(T);
			}
			return JsonConvert.DeserializeObject<T>(json);
		}
		catch (Exception ex)
		{
			LogHelper.LogError("Error fetching " + url + ": " + ex.Message);
			return default(T);
		}
	}

	public static GameObject? CreateNewZoneCube(string objectName)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		Vector3? playerPosition = GetPlayerPosition();
		if (!playerPosition.HasValue)
		{
			return null;
		}
		Vector3 value = playerPosition.Value;
		GameObject obj = GameObject.CreatePrimitive((PrimitiveType)3);
		Renderer component = obj.GetComponent<Renderer>();
		component.material.SetOverrideTag("RenderType", "Transparent");
		component.material.SetInt(SrcBlend, 5);
		component.material.SetInt(DstBlend, 10);
		component.material.SetInt(ZWrite, 0);
		component.material.DisableKeyword("_ALPHATEST_ON");
		component.material.EnableKeyword("_ALPHABLEND_ON");
		component.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		component.material.renderQueue = 3000;
		component.shadowCastingMode = (ShadowCastingMode)0;
		component.material.color = ZoneConfigManager.ColorZoneRed;
		obj.GetComponent<Collider>().enabled = false;
		obj.transform.position = new Vector3(value.x, value.y, value.z);
		obj.transform.localScale = new Vector3(1f, 1f, 1f);
		((Object)obj).name = objectName;
		return obj;
	}

	public static List<CustomQuestZone> ConvertZoneFormat(List<CustomZoneContainer> customZoneContainer, string locationId)
	{
		List<CustomQuestZone> convertedZones = new List<CustomQuestZone>();
		customZoneContainer.ForEach(delegate(CustomZoneContainer zone)
		{
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			GameObject gameObject = zone.GameObject;
			CustomQuestZone customQuestZone = new CustomQuestZone
			{
				ZoneId = ((Object)gameObject).name,
				ZoneName = ((Object)gameObject).name,
				ZoneLocation = locationId,
				ZoneType = zone.ZoneType,
				FlareType = zone.FlareZoneType,
				Position = new ZoneTransform(gameObject.transform.position.x.ToString(), gameObject.transform.position.y.ToString(), gameObject.transform.position.z.ToString()),
				Scale = new ZoneTransform(gameObject.transform.localScale.x.ToString(), gameObject.transform.localScale.y.ToString(), gameObject.transform.localScale.z.ToString()),
				Rotation = new ZoneTransform(gameObject.transform.rotation.x.ToString(), gameObject.transform.rotation.y.ToString(), gameObject.transform.rotation.z.ToString(), gameObject.transform.rotation.w.ToString())
			};
			if (customQuestZone.ZoneType == "salvage")
			{
				customQuestZone.Salvage = new SalvageConfig
				{
					RequiredItemTpl = "PUT YOUR REQUIRED ITEM TPL HERE",
					SalvageTime = 10f,
					Rewards = new List<SalvageRewardConfig>
					{
						new SalvageRewardConfig
						{
							ItemTpl = "PUT YOUR REWARD ITEM HERE",
							Count = 1,
							ToQuestInventory = false
						}
					}
				};
			}
			convertedZones.Add(customQuestZone);
		});
		return convertedZones;
	}
}
