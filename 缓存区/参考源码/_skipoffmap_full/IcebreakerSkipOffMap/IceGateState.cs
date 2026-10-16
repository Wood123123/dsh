using System;
using System.Reflection;
using HarmonyLib;

namespace IcebreakerSkipOffMap;

internal static class IceGateState
{
	private const string TypeName = "Manimal.Icebreaker.IceGate";

	private static Func<bool> _getOn;

	private static bool _broken;

	internal static bool On
	{
		get
		{
			if (_broken)
			{
				return true;
			}
			if (_getOn == null)
			{
				Resolve();
				if (_getOn == null)
				{
					return true;
				}
			}
			try
			{
				return _getOn();
			}
			catch
			{
				return true;
			}
		}
	}

	private static void Resolve()
	{
		try
		{
			Type type = AccessTools.TypeByName("Manimal.Icebreaker.IceGate");
			if (!(type == null))
			{
				MethodInfo methodInfo = AccessTools.PropertyGetter(type, "On");
				if (methodInfo == null)
				{
					_broken = true;
				}
				else
				{
					_getOn = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), methodInfo);
				}
			}
		}
		catch
		{
			_broken = true;
		}
	}
}
