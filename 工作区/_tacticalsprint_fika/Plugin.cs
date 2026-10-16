using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace TacticalSprint.Fika
{
    [BepInPlugin("com.local.tacticalsprint.fika", "Tactical Sprint - 动画同步", "1.0.2")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.fromhell.tacticalsprint", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        private FikaHandler fikaHandler;

        private void Start()
        {
            Log = base.Logger;

            fikaHandler = new FikaHandler();

            Log.LogInfo((object)"[TacticalSprint.Fika] v1.0.2 已加载，战术冲刺动画同步已启用。");
        }

        private void OnDestroy()
        {
            fikaHandler?.Dispose();
            fikaHandler = null;
        }
    }
}
