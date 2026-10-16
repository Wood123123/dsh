using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CookingGrenades;

internal static class FikaPatches_GrenadePacketExecute
{
	private static readonly ConcurrentDictionary<Type, PropertyInfo> HandsControllerProps = new ConcurrentDictionary<Type, PropertyInfo>();

	public static void Prefix(object __instance, object player)
	{
		float cookTime;
		if (__instance == null || player == null)
		{
			Plugin.log.LogWarning((object)"[CG-Fika][Execute] __instance 或 player 为 null，跳过 cook 绑定");
		}
		else if (PacketCookTimeStore.TryGet(__instance, out cookTime))
		{
			object handsController = GetHandsController(player);
			if (handsController == null)
			{
				Plugin.log.LogWarning((object)$"[CG-Fika][Execute] 检出 cookTime={cookTime:F2}，但 player.HandsController 为 null，无法绑定（headless 无本地玩家的同步断点之一）");
			}
			else
			{
				ObservedControllerCookStore.Set(handsController, cookTime);
			}
		}
	}

	private static object GetHandsController(object player)
	{
		Type type = player.GetType();
		if (HandsControllerProps.TryGetValue(type, out var value))
		{
			return value?.GetValue(player);
		}
		PropertyInfo property = type.GetProperty("HandsController", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		HandsControllerProps.TryAdd(type, property);
		return property?.GetValue(player);
	}

	public static void Postfix(object __instance, object player)
	{
		if (__instance != null)
		{
			PacketCookTimeStore.Remove(__instance);
		}
		if (player != null)
		{
			object handsController = GetHandsController(player);
			if (handsController != null)
			{
				ObservedControllerCookStore.Remove(handsController);
			}
		}
	}
}
