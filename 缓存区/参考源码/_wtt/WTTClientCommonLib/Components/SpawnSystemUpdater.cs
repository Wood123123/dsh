using System;
using UnityEngine;
using WTTClientCommonLib.CommandProcessor;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Components;

public class SpawnSystemUpdater : MonoBehaviour
{
	private readonly SpawnCommands _spawnCommands;

	public SpawnSystemUpdater()
	{
		_spawnCommands = WTTClientCommonLib.Instance.SpawnCommands;
	}

	private void Update()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			_spawnCommands.UpdateEditMode();
			if (_spawnCommands.IsEditing && (Object)(object)_spawnCommands.LastSpawnedObject != (Object)null)
			{
				_ = _spawnCommands.LastSpawnedObject.transform.position;
			}
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"Updater failed: {arg}");
		}
	}
}
