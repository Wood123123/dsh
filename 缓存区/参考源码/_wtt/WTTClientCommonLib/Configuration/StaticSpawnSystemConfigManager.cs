using System;
using BepInEx.Configuration;
using UnityEngine;
using WTTClientCommonLib.Attributes;

namespace WTTClientCommonLib.Configuration;

public static class StaticSpawnSystemConfigManager
{
	internal static ConfigEntry<KeyboardShortcut> MoveForwardKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> MoveBackwardKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> MoveLeftKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> MoveRightKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> MoveUpKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> MoveDownKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchUpKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchDownKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotateYawLeftKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotateYawRightKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchRollLeftKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchRollRightKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchRollLeftInvertKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> RotatePitchRollRightInvertKey { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> DeleteSelectedObject { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> CycleSpawnedObjects { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> CyclePreviousSpawnedObject { get; private set; }

	internal static ConfigEntry<KeyboardShortcut> ConfirmPositionKey { get; private set; }

	public static void Initialize(ConfigFile config)
	{
		if (UniversalConfigManager.DeveloperMode.Value)
		{
			CreateKeybindConfigs(config);
		}
	}

	private static void CreateKeybindConfigs(ConfigFile config)
	{
		int num = 10;
		MoveForwardKey = CreateKeybind(config, "Keybinds", "Move Forward", (KeyCode)119, num++);
		MoveBackwardKey = CreateKeybind(config, "Keybinds", "Move Backward", (KeyCode)115, num++);
		MoveLeftKey = CreateKeybind(config, "Keybinds", "Move Left", (KeyCode)97, num++);
		MoveRightKey = CreateKeybind(config, "Keybinds", "Move Right", (KeyCode)100, num++);
		MoveUpKey = CreateKeybind(config, "Keybinds", "Move Up", (KeyCode)101, num++);
		MoveDownKey = CreateKeybind(config, "Keybinds", "Move Down", (KeyCode)113, num++);
		RotatePitchUpKey = CreateKeybind(config, "Keybinds", "Rotate Pitch Up", (KeyCode)264, num++);
		RotatePitchDownKey = CreateKeybind(config, "Keybinds", "Rotate Pitch Down", (KeyCode)258, num++);
		RotateYawLeftKey = CreateKeybind(config, "Keybinds", "Rotate Yaw Left", (KeyCode)260, num++);
		RotateYawRightKey = CreateKeybind(config, "Keybinds", "Rotate Yaw Right", (KeyCode)262, num++);
		RotatePitchRollLeftKey = CreateKeybind(config, "Keybinds", "Rotate Pitch+Roll Left", (KeyCode)263, num++);
		RotatePitchRollRightKey = CreateKeybind(config, "Keybinds", "Rotate Pitch+Roll Right", (KeyCode)265, num++);
		RotatePitchRollLeftInvertKey = CreateKeybind(config, "Keybinds", "Rotate Pitch+Roll Left Inverted", (KeyCode)257, num++);
		RotatePitchRollRightInvertKey = CreateKeybind(config, "Keybinds", "Rotate Pitch+Roll Right Inverted", (KeyCode)259, num++);
		DeleteSelectedObject = CreateKeybind(config, "Keybinds", "Delete Selected Object", (KeyCode)127, num++);
		CycleSpawnedObjects = CreateKeybind(config, "Keybinds", "Cycle Spawned Object", (KeyCode)46, num++);
		CyclePreviousSpawnedObject = CreateKeybind(config, "Keybinds", "Cycle Previous Spawned Object", (KeyCode)44, num++);
		ConfirmPositionKey = CreateKeybind(config, "Keybinds", "Confirm Position", (KeyCode)92, num++);
	}

	private static ConfigEntry<KeyboardShortcut> CreateKeybind(ConfigFile config, string section, string name, KeyCode defaultKey, int order)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		return config.Bind<KeyboardShortcut>(section, name, new KeyboardShortcut(defaultKey, Array.Empty<KeyCode>()), new ConfigDescription("Key for " + name, (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = order
			}
		}));
	}
}
