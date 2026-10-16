using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Fika.Core.Modding;
using UnityEngine;

namespace StashTools.Fika;

[BepInPlugin("local.shijiu.tools.spt4.fika", "【拾玖】Fika 联机适配", "1.0.4")]
[BepInDependency("local.shijiu.tools.spt4", "1.0.4")]
[BepInDependency("com.fika.core", "2.4.4")]
[BepInProcess("EscapeFromTarkov.exe")]
public sealed class FikaPluginAdapter : BaseUnityPlugin
{
	public const string Id = "local.shijiu.tools.spt4.fika";

	internal static ManualLogSource Logger;

	private static bool _history;

	private static bool _pause;

	private void Awake()
	{
		Logger = ((BaseUnityPlugin)this).Logger;
		Version version = typeof(FikaEventDispatcher).Assembly.GetName().Version;
		if (version != new Version(2, 4, 4, 0))
		{
			Logger.LogWarning((object)("当前 Fika " + version?.ToString() + " 尚未核对接口，联机适配未启用；拾玖其他功能继续运行。"));
			return;
		}
		if (Mark(typeof(Plugin).Assembly) != Mark(typeof(FikaPluginAdapter).Assembly))
		{
			Logger.LogError((object)"拾玖客户端与 Fika 适配文件不是同一构建，请同时更新 ShiJiu-Tools.dll 和 ShiJiu-Tools.Fika.dll。");
			return;
		}
		try
		{
			FikaHistory.Install();
			_history = true;
		}
		catch (Exception ex)
		{
			Logger.LogError((object)("Fika 战绩适配初始化失败：" + ex));
		}
		try
		{
			FikaPause.Install();
			_pause = true;
		}
		catch (Exception ex2)
		{
			Logger.LogError((object)("Fika 暂停适配初始化失败：" + ex2));
		}
		if (_history || _pause)
		{
			((Component)this).gameObject.AddComponent<FikaAdapterBehaviour>();
		}
		Logger.LogInfo((object)("【拾玖】Fika 2.4.4 适配 1.0.4 | by: 拾玖；战绩=" + _history + "，暂停=" + _pause));
		static string? Mark(Assembly asm)
		{
			return asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault((AssemblyMetadataAttribute a) => a.Key == "Build")?.Value;
		}
	}

	internal static void Tick()
	{
		if (_history)
		{
			try
			{
				FikaHistory.Tick();
			}
			catch (Exception ex)
			{
				_history = false;
				try
				{
					FikaHistory.Dispose();
				}
				catch
				{
				}
				Logger.LogError((object)("Fika 战绩适配已停用，其他功能继续运行：" + ex));
			}
		}
		if (!_pause)
		{
			return;
		}
		try
		{
			FikaPause.Tick();
		}
		catch (Exception ex2)
		{
			_pause = false;
			try
			{
				FikaPause.Dispose();
			}
			catch
			{
			}
			Logger.LogError((object)("Fika 暂停适配已恢复并停用：" + ex2));
		}
	}

	internal static void Dispose()
	{
		if (_pause)
		{
			try
			{
				FikaPause.Dispose();
			}
			catch (Exception ex)
			{
				Logger.LogWarning((object)ex);
			}
		}
		if (_history)
		{
			try
			{
				FikaHistory.Dispose();
			}
			catch (Exception ex2)
			{
				Logger.LogWarning((object)ex2);
			}
		}
		_pause = (_history = false);
	}
}
