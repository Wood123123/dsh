using System.Reflection;

namespace CookingGrenades;

internal static class FikaPatches_GrenadePacketSerialize
{
	public static void Postfix(object writer)
	{
		if (writer != null)
		{
			MethodInfo put = GrenadePacketCodecCache.GetPut(writer.GetType());
			if (!(put == null))
			{
				put.Invoke(writer, new object[1] { Plugin.NextCookTime });
				Plugin.NextCookTime = 0f;
			}
		}
	}
}
