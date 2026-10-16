using BepInEx.Configuration;
using UnityEngine;

namespace WTTClientCommonLib.Helpers;

public static class KeyboardShortcutExtensions
{
	public static bool BetterIsPressed(this KeyboardShortcut key)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!Input.GetKey(((KeyboardShortcut)(ref key)).MainKey))
		{
			return false;
		}
		foreach (KeyCode modifier in ((KeyboardShortcut)(ref key)).Modifiers)
		{
			if (!Input.GetKey(modifier))
			{
				return false;
			}
		}
		return true;
	}

	public static bool BetterIsDown(this KeyboardShortcut key)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!Input.GetKeyDown(((KeyboardShortcut)(ref key)).MainKey))
		{
			return false;
		}
		foreach (KeyCode modifier in ((KeyboardShortcut)(ref key)).Modifiers)
		{
			if (!Input.GetKey(modifier))
			{
				return false;
			}
		}
		return true;
	}
}
