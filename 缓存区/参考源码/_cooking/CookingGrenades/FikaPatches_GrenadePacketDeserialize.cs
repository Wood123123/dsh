using System.Reflection;

namespace CookingGrenades;

internal static class FikaPatches_GrenadePacketDeserialize
{
	public static void Postfix(object __instance, object reader)
	{
		if (__instance != null && reader != null)
		{
			MethodInfo getFloat = GrenadePacketCodecCache.GetGetFloat(reader.GetType());
			if (!(getFloat == null))
			{
				float cookTime = (float)getFloat.Invoke(reader, null);
				PacketCookTimeStore.Set(__instance, cookTime);
			}
		}
	}
}
