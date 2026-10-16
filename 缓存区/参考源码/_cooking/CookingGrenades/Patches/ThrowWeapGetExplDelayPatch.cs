using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CookingGrenades.Config;
using CookingGrenades.Utils;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class ThrowWeapGetExplDelayPatch : ModulePatch
{
	private sealed class DelayRef
	{
		public float Value;
	}

	private static readonly object _explDelayLock = new object();

	private static ConditionalWeakTable<ThrowWeap, DelayRef> _explDelay = new ConditionalWeakTable<ThrowWeap, DelayRef>();

	private static HashSet<string> uiTypes = new HashSet<string>
	{
		typeof(InfoWindow).FullName,
		typeof(ItemUiContext).FullName,
		typeof(ItemSpecificationPanel).FullName,
		typeof(CompactCharacteristicPanel).FullName
	};

	private static float _lastUiCheckTime = -1f;

	private static bool _lastWasUi;

	private static readonly object _uiLock = new object();

	public static void ResetExplDelay()
	{
		lock (_explDelayLock)
		{
			_explDelay = new ConditionalWeakTable<ThrowWeap, DelayRef>();
		}
	}

	public static void SetExplDelay(ThrowWeap throwWeap, float delay)
	{
		lock (_explDelayLock)
		{
			_explDelay.Remove(throwWeap);
			_explDelay.Add(throwWeap, new DelayRef
			{
				Value = delay
			});
		}
	}

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(ThrowWeap), "GetExplDelay");
	}

	[PatchPostfix]
	public static void PatchPostfix(ThrowWeap __instance, ref float __result)
	{
		lock (_explDelayLock)
		{
			if (_explDelay.TryGetValue(__instance, out var value))
			{
				__result = value.Value;
				return;
			}
		}
		if (ConfigManager.RealisticFuseTimeEnable.Value && (!ConfigManager.ShowDefaultFuseTimeInInventoryUI.Value || !IsInUiContext()))
		{
			float num = MathUtils.GenerateNormalRandomBoxMuller(__result, __result * ConfigManager.FuseTimeSpreadFactor.Value);
			lock (_explDelayLock)
			{
				_explDelay.Remove(__instance);
				_explDelay.Add(__instance, new DelayRef
				{
					Value = num
				});
			}
			__result = num;
		}
	}

	private static bool IsInUiContext()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup - _lastUiCheckTime < 0.5f)
		{
			return _lastWasUi;
		}
		lock (_uiLock)
		{
			if (realtimeSinceStartup - _lastUiCheckTime < 0.5f)
			{
				return _lastWasUi;
			}
			_lastWasUi = ComputeIsInUiContext();
			_lastUiCheckTime = realtimeSinceStartup;
			return _lastWasUi;
		}
	}

	private static bool ComputeIsInUiContext()
	{
		StackTrace stackTrace = new StackTrace(2, fNeedFileInfo: false);
		for (int i = 0; i < stackTrace.FrameCount; i++)
		{
			Type type = stackTrace.GetFrame(i).GetMethod()?.DeclaringType;
			if (type != null && uiTypes.Contains(type.FullName))
			{
				return true;
			}
		}
		return false;
	}
}
