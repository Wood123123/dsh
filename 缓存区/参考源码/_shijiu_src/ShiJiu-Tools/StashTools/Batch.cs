using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StashTools;

internal static class Batch
{
	private static readonly Queue<(string name, Action<Action<bool, string>> run)> Jobs = new Queue<(string, Action<Action<bool, string>>)>();

	private static int _generation;

	private static int _total;

	private static int _done;

	private static bool _waiting;

	private static readonly Dictionary<string, int> Skipped = new Dictionary<string, int>();

	private static readonly Dictionary<string, int> Succeeded = new Dictionary<string, int>();

	private static float _deadline;

	internal static bool Busy { get; private set; }

	internal static string Status { get; private set; } = "";

	internal static int Generation => _generation;

	internal static void Success(string action, int count = 1)
	{
		Succeeded[action] = Succeeded.GetValueOrDefault(action) + count;
	}

	internal static void Skip(string reason, int count = 1)
	{
		Skipped[reason] = Skipped.GetValueOrDefault(reason) + count;
	}

	internal static void Append(string name, Action<Action<bool, string>> run)
	{
		if (Busy)
		{
			Jobs.Enqueue((name, run));
			_total++;
		}
	}

	internal static bool Start(string title, IEnumerable<(string, Action<Action<bool, string>>)> jobs)
	{
		if (Busy)
		{
			return false;
		}
		foreach (var job in jobs)
		{
			Jobs.Enqueue(job);
		}
		if (Jobs.Count == 0)
		{
			StashBehaviour.Notify("没有可操作的物品。");
			return false;
		}
		_generation++;
		_total = Jobs.Count;
		_done = 0;
		Busy = true;
		_waiting = false;
		Skipped.Clear();
		Succeeded.Clear();
		Status = title;
		return true;
	}

	internal static void Tick()
	{
		if (!Busy)
		{
			return;
		}
		if (_waiting)
		{
			if (Time.unscaledTime > _deadline)
			{
				Cancel("操作等待超时，停止后续操作。请检查库存/报价列表后再试，避免重复提交。");
			}
			return;
		}
		if (!StashBehaviour.Active)
		{
			Cancel("已离开仓库，停止剩余操作。");
			return;
		}
		if (!Jobs.TryDequeue(out (string, Action<Action<bool, string>>) result))
		{
			Busy = false;
			Status = "";
			string message = "批处理结束。" + string.Join("；", Succeeded.Select<KeyValuePair<string, int>, string>((KeyValuePair<string, int> p) => $"{p.Key} {p.Value} 件").Concat(Skipped.Select<KeyValuePair<string, int>, string>((KeyValuePair<string, int> p) => $"{p.Key}：{p.Value} 件，保留原处")));
			if (Skipped.Count > 0)
			{
				StashBehaviour.Notify(message);
			}
			else
			{
				StashBehaviour.Completed(message);
			}
			return;
		}
		_waiting = true;
		_deadline = Time.unscaledTime + 30f;
		int generation = _generation;
		Status = $"{result.Item1}  ({_done + 1}/{_total})";
		try
		{
			bool reported = false;
			result.Item2(delegate(bool ok, string error)
			{
				StashBehaviour.Post(delegate
				{
					if (!reported && generation == _generation && Busy)
					{
						reported = true;
						if (!ok)
						{
							Cancel(error);
						}
						else
						{
							_done++;
							_waiting = false;
						}
					}
				});
			});
		}
		catch (Exception ex)
		{
			Cancel("操作发生异常，已停止。");
			StashBehaviour.Error(result.Item1, ex);
		}
	}

	internal static void Cancel(string reason)
	{
		if (Busy)
		{
			Jobs.Clear();
			Busy = false;
			_waiting = false;
			_generation++;
			StashBehaviour.Notify(reason + $" 已完成 {_done}/{_total} 项。");
		}
	}
}
