using System;

namespace CookingGrenades;

public static class CGEvents
{
	public static event Action<float> OnGrenadeThrown;

	internal static void Fire(float cookTime)
	{
		try
		{
			OnGrenadeThrown?.Invoke(cookTime);
		}
		catch
		{
		}
	}
}
