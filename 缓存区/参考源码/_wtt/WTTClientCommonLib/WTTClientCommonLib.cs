using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Comfort.Common;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.CommandProcessor;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib;

[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInPlugin("com.wtt.commonlib", "WTT-ClientCommonLib", "3.0.6")]
public class WTTClientCommonLib : BaseUnityPlugin
{
	private static global::WTTClientCommonLib.CommandProcessor.CommandProcessor _commandProcessor;

	private static GameWorld _gameWorld;

	public static Player Player;

	private GameObject _updaterObject;

	public AssetLoader AssetLoader;

	private PlayerWorldStats _playerWorldStats;

	public SpawnCommands SpawnCommands;

	private static object _fikaHelper;

	private static Assembly _fikaAssembly;

	private static Type _fikaHelperType;

	private static MethodInfo _sendFikaPacketMethod;

	public static bool FikaInstalled { get; private set; }

	public static WTTClientCommonLib Instance { get; private set; }

	private void Awake()
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Instance = this;
			LogHelper.SetLogger(((BaseUnityPlugin)this).Logger);
			FikaInstalled = Chainloader.PluginInfos.ContainsKey("com.fika.core");
			if (FikaInstalled)
			{
				LogHelper.LogInfo("Fika detected - loading Fika support");
				LoadFikaModule();
			}
			AssetLoader = new AssetLoader(((BaseUnityPlugin)this).Logger);
			SpawnCommands = new SpawnCommands(((BaseUnityPlugin)this).Logger, AssetLoader);
			_playerWorldStats = new PlayerWorldStats(((BaseUnityPlugin)this).Logger);
			UniversalConfigManager.Initialize(((BaseUnityPlugin)this).Config);
			ZoneConfigManager.Initialize(((BaseUnityPlugin)this).Config);
			StaticSpawnSystemConfigManager.Initialize(((BaseUnityPlugin)this).Config);
			RadioSettings.Init(((BaseUnityPlugin)this).Config);
			new PatchManager((BaseUnityPlugin)(object)this, true).EnablePatches();
			new ResourceLoader(((BaseUnityPlugin)this).Logger, AssetLoader).LoadAllResourcesFromServer();
			new ExtendedRecipeLoader().FetchExtendedRecipesFromServer();
			new CustomItemParentLoader().FetchCustomParentsFromServer();
		}
		catch (Exception arg)
		{
			((BaseUnityPlugin)this).Logger.LogError((object)$"Failed to initialize WTT-ClientCommonLib: {arg}");
		}
	}

	private void LoadFikaModule()
	{
		try
		{
			string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "WTT-ClientCommonLibFika.dll");
			if (!File.Exists(text))
			{
				LogHelper.LogError("Fika module not found at: " + text);
				FikaInstalled = false;
				return;
			}
			_fikaAssembly = Assembly.LoadFrom(text);
			_fikaHelperType = _fikaAssembly.GetType("WTTClientCommonLib.Fika.Helpers.StaticSpawnFikaHelpers");
			if (_fikaHelperType != null)
			{
				_fikaHelper = Activator.CreateInstance(_fikaHelperType);
				_fikaHelperType.GetMethod("SubscribeToFikaEvents")?.Invoke(_fikaHelper, null);
				_sendFikaPacketMethod = _fikaHelperType.GetMethod("SendFikaSpawnPacket", BindingFlags.Static | BindingFlags.Public);
			}
			else
			{
				LogHelper.LogError("Could not find StaticSpawnFikaHelpers type in Fika assembly");
				FikaInstalled = false;
			}
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"Failed to load Fika module: {arg}");
			FikaInstalled = false;
		}
	}

	public static void SendFikaPacket(CustomSpawnConfig config, Quaternion rotation)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (!FikaInstalled || _sendFikaPacketMethod == null)
		{
			return;
		}
		try
		{
			_sendFikaPacketMethod.Invoke(null, new object[2] { config, rotation });
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"Failed to send Fika packet: {arg}");
		}
	}

	internal async void Start()
	{
		try
		{
			if (_commandProcessor == null)
			{
				_commandProcessor = new global::WTTClientCommonLib.CommandProcessor.CommandProcessor(_playerWorldStats, SpawnCommands);
				_commandProcessor.RegisterCommandProcessor();
			}
			if ((Object)(object)_updaterObject == (Object)null)
			{
				_updaterObject = new GameObject("SpawnSystemUpdater");
				_updaterObject.AddComponent<SpawnSystemUpdater>();
				Object.DontDestroyOnLoad((Object)(object)_updaterObject);
			}
			CustomItemParentLoader instance = CustomItemParentLoader.Instance;
			if (instance != null)
			{
				await instance.RegisterCustomParents();
			}
		}
		catch (Exception ex)
		{
			LogHelper.LogError(ex.ToString());
		}
	}

	private void Update()
	{
		if (Singleton<GameWorld>.Instantiated && ((Object)(object)_gameWorld == (Object)null || (Object)(object)Player == (Object)null))
		{
			_gameWorld = Singleton<GameWorld>.Instance;
			Player = _gameWorld.MainPlayer;
		}
	}
}
