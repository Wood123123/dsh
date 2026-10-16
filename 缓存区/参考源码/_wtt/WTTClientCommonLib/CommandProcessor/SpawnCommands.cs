using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.CommandProcessor;

public class SpawnCommands(ManualLogSource logger, AssetLoader assetLoader)
{
	private const float MoveSpeed = 1f;

	private const float RotateSpeed = 15f;

	private bool _altPressed;

	private int _currentSpawnedIndex = -1;

	private GameObject _editIndicator;

	private GamePlayerOwner _gamePlayerOwner;

	private GameObject _lastSpawnedObject;

	private bool _shiftPressed;

	private readonly List<SpawnedObjectInfo> _spawnedObjects = new List<SpawnedObjectInfo>();

	public GameObject LastSpawnedObject
	{
		get
		{
			if (_currentSpawnedIndex < 0)
			{
				return null;
			}
			return _spawnedObjects[_currentSpawnedIndex].Object;
		}
	}

	public bool IsEditing { get; private set; }

	public void SpawnObject(string bundleName, string prefabName, string[] args = null)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		Player mainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
		if ((Object)(object)mainPlayer == (Object)null)
		{
			LogHelper.LogDebug("Player not found. Are you in a raid?");
			return;
		}
		Vector3 val = mainPlayer.Transform.position + mainPlayer.Transform.forward * 3f;
		Quaternion val2 = Quaternion.identity;
		if (args != null && args.Length >= 3)
		{
			if (float.TryParse(args[0], out var result) && float.TryParse(args[1], out var result2) && float.TryParse(args[2], out var result3))
			{
				((Vector3)(ref val))._002Ector(result, result2, result3);
			}
			if (args.Length >= 6 && float.TryParse(args[3], out var result4) && float.TryParse(args[4], out var result5) && float.TryParse(args[5], out var result6))
			{
				val2 = Quaternion.Euler(result4, result5, result6);
			}
		}
		GameObject val3 = assetLoader.LoadPrefabFromBundle(bundleName, prefabName);
		if ((Object)(object)val3 == (Object)null)
		{
			LogHelper.LogDebug("Failed to load prefab: " + prefabName + " from bundle: " + bundleName);
			return;
		}
		GameObject val4 = (_lastSpawnedObject = Object.Instantiate<GameObject>(val3, val, val2));
		_spawnedObjects.Add(new SpawnedObjectInfo
		{
			Object = val4,
			BundleName = bundleName,
			PrefabName = prefabName
		});
		_currentSpawnedIndex = _spawnedObjects.Count - 1;
		LogHelper.LogDebug($"Spawned {prefabName} at {val}");
	}

	public void StartEditMode()
	{
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_lastSpawnedObject == (Object)null)
		{
			LogHelper.LogDebug("No object to edit. Spawn an object first.");
			return;
		}
		IsEditing = true;
		_gamePlayerOwner = ((Component)WTTClientCommonLib.Player).GetComponentInChildren<GamePlayerOwner>();
		((Behaviour)_gamePlayerOwner).enabled = false;
		if ((Object)(object)_editIndicator == (Object)null)
		{
			_editIndicator = GameObject.CreatePrimitive((PrimitiveType)0);
			_editIndicator.transform.localScale = Vector3.one * 0.3f;
			_editIndicator.GetComponent<Renderer>().material.color = Color.red;
			_editIndicator.GetComponent<Collider>().enabled = false;
			GameObject obj = GameObject.CreatePrimitive((PrimitiveType)3);
			obj.transform.SetParent(_editIndicator.transform);
			obj.transform.localPosition = new Vector3(0f, 0f, 0.5f);
			obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
		}
		LogHelper.LogDebug("Entering edit mode. Use WASD to move, Arrow Keys to rotate. Press Enter to confirm.");
		LogHelper.LogDebug("Current position: " + ((object)_lastSpawnedObject.transform.position/*cast due to constrained. prefix*/).ToString());
	}

	public void ExitEditMode()
	{
		LogHelper.LogDebug("Exiting edit mode.");
		IsEditing = false;
		_gamePlayerOwner = ((Component)WTTClientCommonLib.Player).GetComponentInChildren<GamePlayerOwner>();
		((Behaviour)_gamePlayerOwner).enabled = true;
		if ((Object)(object)_editIndicator != (Object)null)
		{
			Object.Destroy((Object)(object)_editIndicator);
			_editIndicator = null;
		}
	}

	public void UpdateEditMode()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0551: Unknown result type (might be due to invalid IL or missing references)
		//IL_0556: Unknown result type (might be due to invalid IL or missing references)
		//IL_055a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0592: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		if (!IsEditing || (Object)(object)_lastSpawnedObject == (Object)null)
		{
			if (IsEditing)
			{
				logger.LogError((object)"EditMode active but no object!");
			}
			return;
		}
		Camera main = Camera.main;
		Transform val = ((main != null) ? ((Component)main).transform : null);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		if (StaticSpawnSystemConfigManager.CycleSpawnedObjects.Value.BetterIsDown())
		{
			CycleSpawnedObjects();
		}
		else if (StaticSpawnSystemConfigManager.CyclePreviousSpawnedObject.Value.BetterIsDown())
		{
			CyclePreviousSpawnedObject();
		}
		else if (StaticSpawnSystemConfigManager.DeleteSelectedObject.Value.BetterIsDown())
		{
			DeleteSelectedObject();
			if ((Object)(object)_lastSpawnedObject == (Object)null)
			{
				return;
			}
		}
		_shiftPressed = Input.GetKey((KeyCode)304) || Input.GetKey((KeyCode)303);
		_altPressed = Input.GetKey((KeyCode)308) || Input.GetKey((KeyCode)307);
		float num = 1f;
		if (_shiftPressed)
		{
			num = 2f;
			if (_altPressed)
			{
				num = 4f;
			}
		}
		bool num2 = StaticSpawnSystemConfigManager.MoveForwardKey.Value.BetterIsPressed();
		bool flag = StaticSpawnSystemConfigManager.MoveBackwardKey.Value.BetterIsPressed();
		bool flag2 = StaticSpawnSystemConfigManager.MoveLeftKey.Value.BetterIsPressed();
		bool flag3 = StaticSpawnSystemConfigManager.MoveRightKey.Value.BetterIsPressed();
		bool flag4 = StaticSpawnSystemConfigManager.MoveUpKey.Value.BetterIsPressed();
		bool flag5 = StaticSpawnSystemConfigManager.MoveDownKey.Value.BetterIsPressed();
		Vector3 val2 = Vector3.zero;
		Vector3 forward = val.forward;
		Vector3 right = val.right;
		Vector3 up = Vector3.up;
		forward.y = 0f;
		right.y = 0f;
		((Vector3)(ref forward)).Normalize();
		((Vector3)(ref right)).Normalize();
		if (num2)
		{
			val2 += forward;
		}
		if (flag)
		{
			val2 -= forward;
		}
		if (flag2)
		{
			val2 -= right;
		}
		if (flag3)
		{
			val2 += right;
		}
		if (flag4)
		{
			val2 += up;
		}
		if (flag5)
		{
			val2 -= up;
		}
		if (((Vector3)(ref val2)).sqrMagnitude > 1f)
		{
			((Vector3)(ref val2)).Normalize();
		}
		Transform transform = _lastSpawnedObject.transform;
		transform.position += val2 * (1f * num * Time.deltaTime);
		Vector3 zero = Vector3.zero;
		KeyboardShortcut value = StaticSpawnSystemConfigManager.RotatePitchUpKey.Value;
		bool flag6 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotatePitchDownKey.Value;
		bool flag7 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotateYawLeftKey.Value;
		bool num3 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotateYawRightKey.Value;
		bool flag8 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		if (flag6)
		{
			zero.x++;
		}
		if (flag7)
		{
			zero.x = 1f;
		}
		if (num3)
		{
			zero.y--;
		}
		if (flag8)
		{
			zero.y++;
		}
		value = StaticSpawnSystemConfigManager.RotatePitchRollLeftKey.Value;
		bool num4 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotatePitchRollRightKey.Value;
		bool flag9 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotatePitchRollLeftInvertKey.Value;
		bool flag10 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		value = StaticSpawnSystemConfigManager.RotatePitchRollRightInvertKey.Value;
		bool flag11 = Input.GetKey(((KeyboardShortcut)(ref value)).MainKey) && !_shiftPressed && !_altPressed;
		if (num4)
		{
			zero.x--;
			zero.z++;
		}
		if (flag9)
		{
			zero.x--;
			zero.z--;
		}
		if (flag10)
		{
			zero.x++;
			zero.z++;
		}
		if (flag11)
		{
			zero.x++;
			zero.z--;
		}
		if (zero != Vector3.zero)
		{
			_lastSpawnedObject.transform.Rotate(zero.x * 15f * num * Time.deltaTime, zero.y * 15f * num * Time.deltaTime, zero.z * 15f * num * Time.deltaTime, (Space)0);
		}
		if (StaticSpawnSystemConfigManager.ConfirmPositionKey.Value.BetterIsDown())
		{
			ExitEditMode();
			LogHelper.LogDebug("Position confirmed!");
			LogHelper.LogDebug($"Position: {_lastSpawnedObject.transform.position}");
			Quaternion rotation = _lastSpawnedObject.transform.rotation;
			LogHelper.LogDebug($"Rotation: {((Quaternion)(ref rotation)).eulerAngles}");
		}
		if ((Object)(object)_editIndicator != (Object)null)
		{
			_editIndicator.transform.position = _lastSpawnedObject.transform.position;
			_editIndicator.transform.rotation = Quaternion.identity;
		}
	}

	private void DeleteSelectedObject()
	{
		if (_spawnedObjects.Count == 0 || _currentSpawnedIndex < 0)
		{
			LogHelper.LogDebug("No object to delete.");
			return;
		}
		SpawnedObjectInfo spawnedObjectInfo = _spawnedObjects[_currentSpawnedIndex];
		Object.Destroy((Object)(object)spawnedObjectInfo.Object);
		_spawnedObjects.RemoveAt(_currentSpawnedIndex);
		LogHelper.LogDebug("Deleted object: " + spawnedObjectInfo.PrefabName);
		if (_spawnedObjects.Count > 0)
		{
			_currentSpawnedIndex = Mathf.Clamp(_currentSpawnedIndex, 0, _spawnedObjects.Count - 1);
			_lastSpawnedObject = _spawnedObjects[_currentSpawnedIndex].Object;
			return;
		}
		_currentSpawnedIndex = -1;
		_lastSpawnedObject = null;
		if (IsEditing)
		{
			ExitEditMode();
		}
	}

	private void CycleSpawnedObjects()
	{
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		if (_spawnedObjects.Count == 0)
		{
			LogHelper.LogDebug("No spawned objects to cycle.");
			return;
		}
		_currentSpawnedIndex = (_currentSpawnedIndex + 1) % _spawnedObjects.Count;
		SpawnedObjectInfo spawnedObjectInfo = _spawnedObjects[_currentSpawnedIndex];
		_lastSpawnedObject = spawnedObjectInfo.Object;
		LogHelper.LogDebug($"Switched to object #{_currentSpawnedIndex}: " + spawnedObjectInfo.PrefabName + " [Bundle: " + spawnedObjectInfo.BundleName + "]");
		if (IsEditing && (Object)(object)_editIndicator != (Object)null)
		{
			_editIndicator.transform.position = _lastSpawnedObject.transform.position;
		}
	}

	private void CyclePreviousSpawnedObject()
	{
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		if (_spawnedObjects.Count == 0)
		{
			LogHelper.LogDebug("No spawned objects to cycle.");
			return;
		}
		_currentSpawnedIndex = (_currentSpawnedIndex - 1 + _spawnedObjects.Count) % _spawnedObjects.Count;
		SpawnedObjectInfo spawnedObjectInfo = _spawnedObjects[_currentSpawnedIndex];
		_lastSpawnedObject = spawnedObjectInfo.Object;
		LogHelper.LogDebug($"Switched to object #{_currentSpawnedIndex}: " + spawnedObjectInfo.PrefabName + " [Bundle: " + spawnedObjectInfo.BundleName + "]");
		if (IsEditing && (Object)(object)_editIndicator != (Object)null)
		{
			_editIndicator.transform.position = _lastSpawnedObject.transform.position;
		}
	}

	public void ExportSpawnedObjectsLocations()
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (_spawnedObjects == null || _spawnedObjects.Count == 0)
		{
			LogHelper.LogDebug("No spawned objects to export.");
			return;
		}
		List<CustomSpawnConfig> list = new List<CustomSpawnConfig>();
		foreach (SpawnedObjectInfo spawnedObject in _spawnedObjects)
		{
			CustomSpawnConfig obj = new CustomSpawnConfig
			{
				QuestId = null,
				LocationID = (WTTClientCommonLib.Player.Location ?? "unknown_location"),
				BundleName = spawnedObject.BundleName,
				PrefabName = spawnedObject.PrefabName,
				Position = spawnedObject.Object.transform.position
			};
			Quaternion rotation = spawnedObject.Object.transform.rotation;
			obj.Rotation = ((Quaternion)(ref rotation)).eulerAngles;
			obj.RequiredQuestStatuses = new List<string>();
			obj.ExcludedQuestStatuses = new List<string>();
			obj.QuestMustExist = null;
			obj.LinkedQuestId = null;
			obj.LinkedRequiredStatuses = new List<string>();
			obj.LinkedExcludedStatuses = new List<string>();
			obj.LinkedQuestMustExist = null;
			obj.RequiredItemInInventory = null;
			obj.RequiredLevel = null;
			obj.RequiredFaction = null;
			obj.RequiredBossSpawned = null;
			CustomSpawnConfig item = obj;
			list.Add(item);
		}
		string text = Path.Combine(Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..\\..\\..\\..\\")), $"WTT-ClientCommonLib-CustomStaticSpawnConfig-Output-{DateTime.Now:yyyyMMddHHmmssffff}.json");
		File.WriteAllText(text, JsonConvert.SerializeObject((object)list, (Formatting)1));
		LogHelper.LogDebug($"Exported {_spawnedObjects.Count} spawned objects to {text}");
	}
}
