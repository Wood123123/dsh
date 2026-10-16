using BepInEx;
using HarmonyLib;

namespace HeadlessRaidGuard;

[BepInPlugin("com.headless.raidguard", "Headless Raid-Start Guard", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
	public void Awake()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		new Harmony("com.headless.raidguard").PatchAll();
	}
}
