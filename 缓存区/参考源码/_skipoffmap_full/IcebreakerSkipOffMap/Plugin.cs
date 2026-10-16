using System;
using BepInEx;
using BepInEx.Logging;
using SPT.Reflection.Patching;

namespace IcebreakerSkipOffMap;

[BepInPlugin("com.icebreaker.skipoffmap", "Icebreaker Off-Map Skip", "1.0.0")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class Plugin : BaseUnityPlugin
{
	public const string Guid = "com.icebreaker.skipoffmap";

	public const string Name = "Icebreaker Off-Map Skip";

	public const string Version = "1.0.0";

	internal static ManualLogSource Log;

	public void Awake()
	{
		Log = ((BaseUnityPlugin)this).Logger;
		Try("RenderEnvProbe.Update", delegate
		{
			((ModulePatch)new PatchSkipOffMap()).Enable();
		});
		Try("RenderEnvProbe.TickVanillaPerfCensus", delegate
		{
			((ModulePatch)new PatchSkipCensus()).Enable();
		});
	}

	private void Try(string what, Action enable)
	{
		try
		{
			enable();
			((BaseUnityPlugin)this).Logger.LogInfo((object)("[SkipOffMap] " + what + " patched"));
		}
		catch (Exception ex)
		{
			((BaseUnityPlugin)this).Logger.LogWarning((object)("[SkipOffMap] " + what + " NOT patched: " + ex.Message));
		}
	}
}
