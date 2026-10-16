using System;
using EFT.UI;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.CommandProcessor;

public class CommandProcessor(PlayerWorldStats playerWorldStats, SpawnCommands spawnCommands)
{
	public void RegisterCommandProcessor()
	{
		ConsoleScreen.Processor.RegisterCommand("clear", (Action)delegate
		{
			MonoBehaviourSingleton<PreloaderUI>.Instance.Console.Clear();
		}, (string)null);
		if (UniversalConfigManager.DeveloperMode.Value)
		{
			ConsoleScreen.Processor.RegisterCommand("GetPlayerWorldStats", (Action)playerWorldStats.GetPlayerWorldStats, (string)null);
			ConsoleScreen.Processor.RegisterCommand("EnterEditMode", (Action)spawnCommands.StartEditMode, (string)null);
			ConsoleScreen.Processor.RegisterCommand("ExitEditMode", (Action)spawnCommands.ExitEditMode, (string)null);
			ConsoleScreen.Processor.RegisterCommand("ExportSpawnedObjectInfo", (Action)spawnCommands.ExportSpawnedObjectsLocations, (string)null);
			SpawnObjectCommands.SetSpawnCommands(spawnCommands);
			ConsoleScreen.Processor.RegisterCommandGroup<SpawnObjectCommands>();
		}
	}
}
