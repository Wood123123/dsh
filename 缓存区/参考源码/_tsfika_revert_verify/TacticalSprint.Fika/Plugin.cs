using BepInEx;
using BepInEx.Logging;

namespace TacticalSprint.Fika;

[BepInPlugin("com.local.tacticalsprint.fika", "Tactical Sprint - 动画同步", "1.0.2")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class Plugin : BaseUnityPlugin
{
	private FikaHandler fikaHandler;

	internal static ManualLogSource Log { get; private set; }

	private void Start()
	{
		Log = ((BaseUnityPlugin)this).Logger;
		fikaHandler = new FikaHandler();
		Log.LogInfo((object)"[TacticalSprint.Fika] v1.0.2 已加载，战术冲刺动画同步已启用。");
	}

	private void OnDestroy()
	{
		fikaHandler?.Dispose();
		fikaHandler = null;
	}
}
