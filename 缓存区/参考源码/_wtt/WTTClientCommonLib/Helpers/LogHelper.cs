using BepInEx.Logging;

namespace WTTClientCommonLib.Helpers;

public static class LogHelper
{
	public static ManualLogSource Logger;

	public static void SetLogger(ManualLogSource logger)
	{
		Logger = logger;
	}

	public static void LogDebug(string message)
	{
	}

	public static void LogInfo(string message)
	{
		ManualLogSource logger = Logger;
		if (logger != null)
		{
			logger.LogInfo((object)message);
		}
	}

	public static void LogError(string message)
	{
		ManualLogSource logger = Logger;
		if (logger != null)
		{
			logger.LogError((object)message);
		}
	}

	public static void LogWarn(string message)
	{
		ManualLogSource logger = Logger;
		if (logger != null)
		{
			logger.LogWarning((object)message);
		}
	}
}
