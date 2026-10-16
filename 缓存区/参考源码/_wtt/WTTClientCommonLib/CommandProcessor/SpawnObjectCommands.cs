using EFT.Console.Core;

namespace WTTClientCommonLib.CommandProcessor;

public class SpawnObjectCommands
{
	private static SpawnCommands _spawnCommands;

	public static void SetSpawnCommands(SpawnCommands spawnCommands)
	{
		_spawnCommands = spawnCommands;
	}

	[ConsoleCommand("SpawnObject", "Spawn Static Object using bundle name, prefab name", "<String>, <String>", "", new string[] { })]
	public static void SpawnObject(string bundleName, string prefabName)
	{
		_spawnCommands?.SpawnObject(bundleName, prefabName);
	}
}
