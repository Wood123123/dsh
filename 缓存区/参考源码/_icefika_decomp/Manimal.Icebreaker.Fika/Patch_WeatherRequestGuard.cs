using System;
using Comfort.Common;
using EFT;
using EFT.Weather;
using Fika.Core.Main.GameMode;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.World;
using HarmonyLib;

namespace Manimal.Icebreaker.Fika;

[HarmonyPatch(typeof(WeatherRequest), "HandleRequest")]
internal static class Patch_WeatherRequestGuard
{
	[HarmonyPrefix]
	private static void Prefix(NetPeer peer, FikaServer server)
	{
		bool flag = false;
		IFikaGame instance = Singleton<IFikaGame>.Instance;
		string text = "NULL";
		string text2 = "NULL";
		try
		{
			flag = IceGate.On;
			text = ((((instance != null) ? instance.GameController : null) != null) ? ((object)instance.GameController).GetType().Name : "NULL");
			object obj;
			if (instance == null)
			{
				obj = null;
			}
			else
			{
				BaseGameController gameController = instance.GameController;
				obj = ((gameController != null) ? gameController.WeatherClasses : null);
			}
			text2 = ((obj == null) ? "NULL" : instance.GameController.WeatherClasses.Length.ToString());
		}
		catch (Exception ex)
		{
			text = "ERR:" + ex.Message;
		}
		FikaAddonPlugin.Log.LogWarning((object)string.Format("[IceWeather] WeatherRequest received — gate={0} peer={1} fikaGame={2} ctrl={3} classes={4}", flag, (peer != null) ? ((LiteNetPeer)peer).Id.ToString() : "?", (instance != null) ? ((object)instance).GetType().Name : "NULL", text, text2));
		if (!flag)
		{
			return;
		}
		try
		{
			if (((instance != null) ? instance.GameController : null) != null && (instance.GameController.WeatherClasses == null || instance.GameController.WeatherClasses.Length == 0))
			{
				DateTime dateTime = DateTimeExtensions.StartOfDay();
				WeatherNode val = WeatherNode.CreateDefault();
				WeatherNode val2 = WeatherNode.CreateDefault();
				val.Time = dateTime.Ticks;
				val2.Time = dateTime.AddDays(1.0).Ticks;
				instance.GameController.WeatherClasses = (WeatherNode[])(object)new WeatherNode[2] { val, val2 };
				FikaAddonPlugin.Log.LogWarning((object)"[IceWeather] host had NO WeatherClasses when a client asked — synthesized defaults so the raid can't deadlock");
			}
		}
		catch (Exception ex2)
		{
			FikaAddonPlugin.Log.LogWarning((object)("[IceWeather] guard failed: " + ex2.Message));
		}
	}
}
