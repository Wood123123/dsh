using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CookingGrenades;

internal static class GrenadePacketCodecCache
{
	private static readonly ConcurrentDictionary<Type, MethodInfo> PutMethods = new ConcurrentDictionary<Type, MethodInfo>();

	private static readonly ConcurrentDictionary<Type, MethodInfo> GetFloatMethods = new ConcurrentDictionary<Type, MethodInfo>();

	public static MethodInfo GetPut(Type writerType)
	{
		return PutMethods.GetOrAdd(writerType, (Type t) => t.GetMethod("Put", new Type[1] { typeof(float) }));
	}

	public static MethodInfo GetGetFloat(Type readerType)
	{
		return GetFloatMethods.GetOrAdd(readerType, (Type t) => t.GetMethod("GetFloat"));
	}
}
