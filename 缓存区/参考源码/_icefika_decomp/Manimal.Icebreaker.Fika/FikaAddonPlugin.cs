using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

[BepInPlugin("com.manimal.icebreaker.fika", "Manimal-IcebreakerFika", "1.1.3")]
[BepInDependency("com.fika.core", "2.4.2")]
[BepInDependency("com.manimal.icebreaker", "1.1.3")]
public class FikaAddonPlugin : BaseUnityPlugin
{
	internal static ManualLogSource Log;

	internal static ConfigEntry<KeyboardShortcut> ProbeKey;

	private IceSyncHandler _handler;

	private void Awake()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		Log = ((BaseUnityPlugin)this).Logger;
		ProbeKey = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("Diagnostics", "BodyProbeKey", new KeyboardShortcut((KeyCode)288, Array.Empty<KeyCode>()), "dump the renderer state of the observed player you're LOOKING AT to the log (invisible-bot hunt)");
		_handler = new IceSyncHandler();
		new Harmony("com.manimal.icebreaker.fika").PatchAll();
		GameObject val = new GameObject("Icebreaker_FikaDiag");
		Object.DontDestroyOnLoad((Object)val);
		val.AddComponent<IceBodyDiag>();
		Log.LogInfo((object)"Manimal-IcebreakerFika 1.1.3 loaded — chain/seal/keypad world events will sync");
	}

	private void OnDestroy()
	{
		_handler?.Dispose();
		_handler = null;
	}
}
