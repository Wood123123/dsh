using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace StashTools;

internal static class MarketQuotes
{
	internal readonly record struct Quote(MarketQuote Value, bool Pending, double Expires)
	{
		internal double Price => Value.Select(FleaPriceBasis.平均价);
	}

	private static readonly QuoteRequestQueue<MarketQuote> Requests = new QuoteRequestQueue<MarketQuote>(MarketTransport.Request, 2, TimeSpan.FromSeconds(12.0), queueTimeout: TimeSpan.FromSeconds(60.0), successTtl: TimeSpan.FromSeconds(30.0), failureCooldown: TimeSpan.FromSeconds(60.0));

	private static readonly Dictionary<string, Task<MarketQuote>> Seen = new Dictionary<string, Task<MarketQuote>>();

	private static readonly Dictionary<string, (MarketQuote Value, double Until)> Latest = new Dictionary<string, (MarketQuote, double)>();

	private static double _warningAt;

	private static double _nextPoll;

	internal static bool Stopped { get; private set; }

	internal static Quote Get(string template)
	{
		double num = Time.unscaledTime;
		if (Stopped)
		{
			return new Quote(default(MarketQuote), Pending: false, num + 60.0);
		}
		Task<MarketQuote> async = Requests.GetAsync(template);
		if (async.IsCompletedSuccessfully)
		{
			if (Latest.Count >= 512 && !Latest.ContainsKey(template))
			{
				string[] array = (from p in Latest.OrderBy<KeyValuePair<string, (MarketQuote, double)>, double>((KeyValuePair<string, (MarketQuote Value, double Until)> p) => p.Value.Until).Take(64)
					select p.Key).ToArray();
				foreach (string key in array)
				{
					Latest.Remove(key);
				}
			}
			Latest[template] = (async.Result, num + 60.0);
			return new Quote(async.Result, Pending: false, num + 30.0);
		}
		if (async.IsFaulted)
		{
			_ = async.Exception;
			if (num >= _warningAt)
			{
				_warningAt = num + 10.0;
				Plugin.Logger.LogWarning((object)"市场报价暂不可用，失败查询进入60秒冷却，其他游戏流程继续运行。");
			}
		}
		else if (!async.IsCanceled)
		{
			Seen[template] = async;
		}
		(MarketQuote, double) value;
		return new Quote((Latest.TryGetValue(template, out value) && num < value.Item2) ? value.Item1 : default(MarketQuote), !async.IsCompleted, num + (async.IsCompleted ? 60.0 : 0.25));
	}

	internal static Task<MarketQuote> Read(string template)
	{
		if (!Stopped)
		{
			return Requests.GetAsync(template);
		}
		return Task.FromCanceled<MarketQuote>(new CancellationToken(canceled: true));
	}

	internal static double? Peek(string template)
	{
		if (!Requests.TryGetCompleted(template, out var value))
		{
			return null;
		}
		return value.Select(FleaPriceBasis.平均价);
	}

	internal static void Tick()
	{
		if (Stopped || Seen.Count == 0 || (double)Time.unscaledTime < _nextPoll)
		{
			return;
		}
		_nextPoll = (double)Time.unscaledTime + 0.1;
		bool flag = false;
		KeyValuePair<string, Task<MarketQuote>>[] array = Seen.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<string, Task<MarketQuote>> keyValuePair = array[i];
			if (keyValuePair.Value.IsCompleted)
			{
				if (keyValuePair.Value.IsFaulted)
				{
					_ = keyValuePair.Value.Exception;
				}
				Seen.Remove(keyValuePair.Key);
				flag = true;
			}
		}
		if (flag)
		{
			HoverPrices.Invalidate();
		}
	}

	internal static void Stop()
	{
		if (!Stopped)
		{
			Stopped = true;
			Requests.CancelPending();
			Seen.Clear();
			Plugin.Logger.LogInfo((object)"报价已暂停：取消未完成请求，结算仅读取已完成缓存。");
		}
	}

	internal static void Resume()
	{
		if (Stopped)
		{
			Stopped = false;
			Plugin.Logger.LogInfo((object)"已进入有效物品会话，恢复独立市场报价。");
		}
	}
}
