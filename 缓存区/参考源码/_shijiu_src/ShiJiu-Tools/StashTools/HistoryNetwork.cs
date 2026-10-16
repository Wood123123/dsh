using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;
using SPT.Common.Http;
using StashTools.History;
using UnityEngine;

namespace StashTools;

internal static class HistoryNetwork
{
	private static readonly string Root;

	private static readonly JsonSerializerSettings Json;

	private static bool _sending;

	private static bool _tryJoin;

	private static float _next;

	internal static void Enqueue(RaidRecord record)
	{
		string text = Folder(RequestHandler.SessionId);
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, record.Id + ".json");
		WriteOutbox(text2 + ".tmp", text2, JsonConvert.SerializeObject((object)record, Json));
	}

	internal static void EnqueueJoin(TeamJoin join)
	{
		string text = Folder(RequestHandler.SessionId);
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, join.RecordId + ".join");
		WriteOutbox(text2 + ".tmp", text2, JsonConvert.SerializeObject((object)join, Json));
	}

	private static string Folder(string profile)
	{
		string text = RequestHandler.Host ?? "unknown";
		using SHA256 sHA = SHA256.Create();
		string path = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(text + "/" + profile))).Replace("-", "");
		return Path.Combine(Root, path);
	}

	internal static async Task<HistoryReply> Request(string action, object data)
	{
		_ = 1;
		try
		{
			string text = JsonConvert.SerializeObject((object)new
			{
				Payload = JsonConvert.SerializeObject(data, Json)
			}, Json);
			Task<string> request = RequestHandler.PostJsonAsync("/stashtools/history/" + action, text);
			if (await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(15.0))) != request)
			{
				request.ContinueWith(delegate(Task<string> task)
				{
					_ = task.Exception;
				}, TaskContinuationOptions.OnlyOnFaulted);
				throw new TimeoutException("对局记录请求超时");
			}
			return JsonConvert.DeserializeObject<HistoryReply>(await request, Json) ?? new HistoryReply
			{
				Error = "服务端没有返回有效记录"
			};
		}
		catch (Exception ex)
		{
			return new HistoryReply
			{
				Error = "无法连接对局记录服务。请确认已安装配套服务端并重启服务器。" + (Plugin.DiagnosticMode.Value ? (" " + ex.Message) : "")
			};
		}
	}

	internal static void Tick()
	{
		if (_sending || Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 15f;
		string sessionId = RequestHandler.SessionId;
		if (string.IsNullOrEmpty(sessionId))
		{
			return;
		}
		string path = Folder(sessionId);
		if (Directory.Exists(path))
		{
			string text = (_tryJoin ? Directory.EnumerateFiles(path, "*.join").FirstOrDefault() : Directory.EnumerateFiles(path, "*.json").FirstOrDefault());
			_tryJoin = !_tryJoin;
			if (text == null)
			{
				text = Directory.EnumerateFiles(path, "*.json").FirstOrDefault() ?? Directory.EnumerateFiles(path, "*.join").FirstOrDefault();
			}
			if (text != null)
			{
				_sending = true;
				Send(text, sessionId, RequestHandler.Host ?? "");
			}
		}
	}

	private static async Task Send(string path, string profile, string backend)
	{
		try
		{
			if (!SameSession())
			{
				_sending = false;
				return;
			}
			string original = File.ReadAllText(path);
			HistoryReply result;
			if (Path.GetExtension(path) == ".join")
			{
				result = await Request("join", JsonConvert.DeserializeObject<TeamJoin>(original, Json));
			}
			else
			{
				result = await HistoryDelivery.Send(JsonConvert.DeserializeObject<RaidRecord>(original, Json), Request, SameSession);
			}
			StashBehaviour.Post(delegate
			{
				try
				{
					if (result.Ok && SameSession() && File.Exists(path) && File.ReadAllText(path) == original)
					{
						File.Delete(path);
					}
					else if (!result.Ok)
					{
						Plugin.Logger.LogWarning((object)("战绩保留在本地待重试：" + result.Error));
					}
				}
				finally
				{
					_sending = false;
				}
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			StashBehaviour.Post(delegate
			{
				_sending = false;
				Plugin.Logger.LogWarning((object)("战绩上传失败：" + ex3.Message));
			});
		}
		bool SameSession()
		{
			if (RequestHandler.SessionId == profile)
			{
				return (RequestHandler.Host ?? "") == backend;
			}
			return false;
		}
	}

	private static void WriteOutbox(string temp, string path, string json)
	{
		File.WriteAllText(temp, json);
		if (File.Exists(path))
		{
			File.Replace(temp, path, null);
		}
		else
		{
			File.Move(temp, path);
		}
	}

	static HistoryNetwork()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		Root = Path.Combine(Paths.ConfigPath, "StashTools", "history-outbox");
		Json = new JsonSerializerSettings
		{
			DateParseHandling = (DateParseHandling)1,
			TypeNameHandling = (TypeNameHandling)0
		};
		_tryJoin = true;
	}
}
