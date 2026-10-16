using System;
using System.Threading.Tasks;
using BepInEx.Logging;
using EFT.Communications;
using EFT.UI;
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;

namespace RAID_REVIEW;

public class Telemetry
{
	private static WebSocket ws = null;

	private static readonly ManualLogSource Logger = Logger.CreateLogSource("Telemetry");

	public static void Connect(string host)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		if (ws != null)
		{
			ws.Close();
		}
		ws = new WebSocket(host, Array.Empty<string>());
		ws.OnOpen += delegate
		{
			Logger.LogInfo((object)"[RAID-REVIEW] WebSocket connected.");
			RAID_REVIEW.WebSocketConnected = true;
			NotificationManager.DisplayMessageNotification("Raid Review Server Connected", (ENotificationDurationType)1, (ENotificationIconType)0, (Color?)null);
			ws.Send("WS_CONNECTED");
		};
		ws.OnMessage += delegate(object sender, MessageEventArgs e)
		{
			Logger.LogInfo((object)("[RAID-REVIEW] Received message: " + e.Data));
		};
		ws.OnError += delegate(object sender, ErrorEventArgs e)
		{
			Logger.LogError((object)("[RAID-REVIEW] WebSocket error: " + e.Message));
		};
		ws.OnClose += delegate
		{
			Logger.LogInfo((object)"[RAID-REVIEW] WebSocket closed.");
		};
		Task.Run(delegate
		{
			try
			{
				ws.Connect();
			}
			catch (Exception ex)
			{
				NotificationManager.DisplayMessageNotification("Raid Review - Unable to Connect To Server", (ENotificationDurationType)1, (ENotificationIconType)1, (Color?)null);
				MonoBehaviourSingleton<PreloaderUI>.Instance.CloseErrorScreen();
				MonoBehaviourSingleton<PreloaderUI>.Instance.ShowErrorScreen("Raid Review - Server Connection Error", "Raid Review was unable to connect to the Websocket server, please check 'Server IP' in the F12 menu and restart game.", (Action)null);
				RAID_REVIEW.WebSocketConnected = false;
				Logger.LogError((object)("WebSocket connection error: " + ex.Message));
			}
		});
	}

	public static Task Send(string Action, string Payload)
	{
		return Task.Run(delegate
		{
			if (RAID_REVIEW.EnableRecording.Value)
			{
				try
				{
					WsPayload wsPayload = new WsPayload
					{
						Action = Action,
						Payload = Payload
					};
					ws.Send(JsonConvert.SerializeObject((object)wsPayload));
				}
				catch (Exception ex)
				{
					Logger.LogError((object)("WebSocket send error: " + ex.Message));
				}
			}
		});
	}
}
