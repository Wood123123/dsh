using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class BaseSoundPlayerOnSoundAtPointPatch : ModulePatch
{
	public static BaseSoundPlayer HaveToNotRunFuseSound;

	protected override MethodBase GetTargetMethod()
	{
		Type typeFromHandle = typeof(BaseSoundPlayer);
		Type type = null;
		Type[] interfaces = typeFromHandle.GetInterfaces();
		foreach (Type type2 in interfaces)
		{
			if (type2.FullName != null && type2.FullName.IndexOf("IEventsConsumer", StringComparison.Ordinal) >= 0)
			{
				type = type2;
				break;
			}
		}
		if (type != null)
		{
			InterfaceMapping interfaceMap = typeFromHandle.GetInterfaceMap(type);
			for (int j = 0; j < interfaceMap.InterfaceMethods.Length; j++)
			{
				if (interfaceMap.InterfaceMethods[j].Name == "OnSoundAtPoint")
				{
					MethodInfo methodInfo = interfaceMap.TargetMethods[j];
					Plugin.log.LogInfo((object)("[SoundPatch] 找到目标: " + methodInfo.DeclaringType?.Name + "." + methodInfo.Name + " (通过 " + type.Name + ")"));
					return methodInfo;
				}
			}
		}
		MethodInfo methodInfo2 = AccessTools.Method(typeFromHandle, "SoundAtPointEventHandler", new Type[1] { typeof(string) }, (Type[])null);
		if (methodInfo2 != null)
		{
			Plugin.log.LogInfo((object)"[SoundPatch] 回退使用: SoundAtPointEventHandler(String)");
			return methodInfo2;
		}
		Plugin.log.LogWarning((object)"[SoundPatch] 未找到 OnSoundAtPoint 方法，保险丝静音功能不可用");
		return null;
	}

	[PatchPrefix]
	public static bool PatchPrefix(BaseSoundPlayer __instance, string StringParam)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0015: Expected O, but got Unknown
		if ((Object)HaveToNotRunFuseSound == (Object)__instance && StringParam == "SndFuse")
		{
			HaveToNotRunFuseSound = null;
			return false;
		}
		return true;
	}
}
