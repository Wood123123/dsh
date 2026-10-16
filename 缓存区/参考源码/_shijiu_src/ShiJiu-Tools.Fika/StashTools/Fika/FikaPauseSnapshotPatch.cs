using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;

namespace StashTools.Fika;

[HarmonyPatch]
internal static class FikaPauseSnapshotPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new Type[2]
		{
			typeof(FikaServer),
			typeof(FikaClient)
		}.Select((Type type) => AccessTools.DeclaredMethod(type, "OnNetworkReceive", (Type[])null, (Type[])null));
	}

	private static bool Prefix(object __instance, NetPacketReader __1)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		if (!FikaPauseGameplay.Active)
		{
			return true;
		}
		int position = ((NetDataReader)__1).Position;
		try
		{
			if (__instance is FikaServer)
			{
				((NetDataReader)__1).GetByte();
			}
			EPacketType val = (EPacketType)((NetDataReader)__1).PeekByte();
			return (int)val != 1 && (int)val != 5;
		}
		catch (Exception ex)
		{
			FikaPause.Log.LogError((object)ex);
			FikaPause.Fail("暂停快照检查失败");
			return true;
		}
		finally
		{
			((NetDataReader)__1).SetPosition(position);
		}
	}
}
