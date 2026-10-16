using System;
using System.Collections;
using System.Reflection;
using Comfort.Common;
using CookingGrenades.Config;
using CookingGrenades.Utils;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace CookingGrenades;

public class GrenadeCookingTimer
{
	private float _cookingStartTime;

	public Coroutine existingCoroutine;

	private CookIndicator _cookIndicator;

	public float CookingStartTime => _cookingStartTime;

	public bool IsCooking
	{
		get
		{
			if (_cookingStartTime > 0f)
			{
				return (Object)(object)Controller != (Object)null;
			}
			return false;
		}
	}

	public GrenadeHandsController Controller { get; private set; }

	public GrenadeCookingTimer()
	{
		Controller = null;
	}

	public GrenadeCookingTimer(GrenadeHandsController controller)
	{
		Controller = controller;
	}

	public void SetCookingItem(GrenadeHandsController controller)
	{
		Controller = controller;
		_cookingStartTime = 0f;
	}

	public void StartCooking(GrenadeHandsController controller)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		_cookingStartTime = Time.time;
		existingCoroutine = ((MonoBehaviour)controller).StartCoroutine(ForceThrowCoroutine(controller));
		if (ConfigManager.EnableCookIndicator.Value)
		{
			ThrowWeap item = controller.Item;
			if (item != null)
			{
				GameObject val = new GameObject("CookIndicator");
				_cookIndicator = val.AddComponent<CookIndicator>();
				_cookIndicator.Initialize(((Component)controller).transform, item, _cookingStartTime);
				Plugin.log.LogInfo((object)"[CookIndicator] 温雷提示已启动");
			}
		}
		if (!ConfigManager.DebugGUI.Value)
		{
			return;
		}
		DebugDisplay.Instance.InsertDisplayObject("Cooking Time ", delegate
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Expected O, but got Unknown
			Player mainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
			float num = 0f;
			Item item2 = mainPlayer.HandsController.Item;
			ThrowWeap val2 = (ThrowWeap)((item2 is ThrowWeap) ? item2 : null);
			if (val2 != null)
			{
				num = val2.GetExplDelay;
			}
			return $"{GetCookingTime():F3}/{num:F3} sec ({GetCookingTime() / num * 100f:F1}%)";
		});
	}

	public float GetCookingTime()
	{
		if (!IsCooking)
		{
			return 0f;
		}
		return Time.time - _cookingStartTime;
	}

	public void Reset(GrenadeHandsController oldController)
	{
		if ((Object)(object)_cookIndicator != (Object)null)
		{
			_cookIndicator.PlayThrowAnimation();
			_cookIndicator = null;
		}
		Controller = null;
		_cookingStartTime = 0f;
		((MonoBehaviour)oldController).StopCoroutine(existingCoroutine);
		oldController = null;
	}

	private IEnumerator ForceThrowCoroutine(GrenadeHandsController controller)
	{
		yield return (object)new WaitForSeconds(controller.Item.GetExplDelay - Math.Max(ConfigManager.AutoThrowLeadTime.Value, 0.05f));
		if (IsCooking && (Object)(object)controller != (Object)null)
		{
			ForceThrow(controller);
		}
	}

	private void ForceThrow(GrenadeHandsController controller)
	{
		MethodInfo methodInfo = (controller.WaitingForHighThrow ? AccessTools.Method(typeof(GrenadeHandsController), "HandleFireInput", (Type[])null, (Type[])null) : AccessTools.Method(typeof(GrenadeHandsController), "HandleAltFireInput", (Type[])null, (Type[])null));
		if (methodInfo != null)
		{
			methodInfo.Invoke(controller, null);
		}
		else
		{
			Plugin.log.LogError((object)"Throw method not found");
		}
	}
}
