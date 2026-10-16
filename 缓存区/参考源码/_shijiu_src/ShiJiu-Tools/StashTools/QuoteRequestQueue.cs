using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StashTools;

internal sealed class QuoteRequestQueue<T> : IDisposable
{
	private sealed class Entry
	{
		internal readonly string Key;

		internal readonly TaskCompletionSource<T> Completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

		internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

		internal double Expires = double.PositiveInfinity;

		internal Entry(string key)
		{
			Key = key;
		}
	}

	private readonly object _sync = new object();

	private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

	private readonly Func<string, CancellationToken, Task<T>> _fetch;

	private readonly SemaphoreSlim _slots;

	private readonly TimeSpan _timeout;

	private readonly TimeSpan _queueTimeout;

	private readonly TimeSpan _successTtl;

	private readonly TimeSpan _failureCooldown;

	private readonly int _maxEntries;

	private readonly Func<double> _seconds;

	private bool _disposed;

	internal QuoteRequestQueue(Func<string, CancellationToken, Task<T>> fetch, int concurrency = 3, TimeSpan? timeout = null, TimeSpan? successTtl = null, TimeSpan? failureCooldown = null, int maxEntries = 512, Func<double>? seconds = null, TimeSpan? queueTimeout = null)
	{
		_fetch = fetch ?? throw new ArgumentNullException("fetch");
		if (concurrency < 1)
		{
			throw new ArgumentOutOfRangeException("concurrency");
		}
		if (maxEntries < concurrency)
		{
			throw new ArgumentOutOfRangeException("maxEntries");
		}
		_timeout = timeout ?? TimeSpan.FromSeconds(12.0);
		_queueTimeout = queueTimeout ?? _timeout;
		_successTtl = successTtl ?? TimeSpan.FromSeconds(30.0);
		_failureCooldown = failureCooldown ?? TimeSpan.FromSeconds(15.0);
		if (_timeout <= TimeSpan.Zero || _queueTimeout <= TimeSpan.Zero || _successTtl < TimeSpan.Zero || _failureCooldown <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException("timeout");
		}
		_slots = new SemaphoreSlim(concurrency, concurrency);
		_maxEntries = maxEntries;
		_seconds = seconds ?? ((Func<double>)(() => (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency));
	}

	internal Task<T> GetAsync(string key, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled<T>(cancellationToken);
		}
		if (string.IsNullOrWhiteSpace(key))
		{
			return Task.FromException<T>(new ArgumentException("A quote template is required.", "key"));
		}
		Task<T> task;
		lock (_sync)
		{
			if (_disposed)
			{
				return Task.FromException<T>(new ObjectDisposedException("QuoteRequestQueue"));
			}
			double now = _seconds();
			if (_entries.TryGetValue(key, out Entry value) && (!value.Completion.Task.IsCompleted || now < value.Expires))
			{
				task = value.Completion.Task;
			}
			else
			{
				if (value != null)
				{
					_entries.Remove(key);
				}
				if (_entries.Count >= _maxEntries)
				{
					string[] array = (from e in _entries
						where e.Value.Completion.Task.IsCompleted && now >= e.Value.Expires
						select e.Key).ToArray();
					foreach (string key2 in array)
					{
						_entries.Remove(key2);
					}
					if (_entries.Count >= _maxEntries)
					{
						string text = (from e in _entries
							where e.Value.Completion.Task.IsCompletedSuccessfully
							orderby e.Value.Expires
							select e.Key).FirstOrDefault();
						if (text != null)
						{
							_entries.Remove(text);
						}
					}
					if (_entries.Count >= _maxEntries)
					{
						return Task.FromException<T>(new InvalidOperationException("Quote request capacity reached."));
					}
				}
				Entry entry = new Entry(key);
				_entries[key] = entry;
				task = entry.Completion.Task;
				Task.Run(() => Run(entry));
			}
		}
		if (!cancellationToken.CanBeCanceled)
		{
			return task;
		}
		return WaitFor(task, cancellationToken);
	}

	private static async Task<T> WaitFor(Task<T> task, CancellationToken cancellation)
	{
		TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using (cancellation.Register(delegate
		{
			stopped.TrySetResult(result: true);
		}))
		{
			if (await Task.WhenAny(task, stopped.Task).ConfigureAwait(continueOnCapturedContext: false) != task)
			{
				cancellation.ThrowIfCancellationRequested();
			}
			return await task.ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task Run(Entry entry)
	{
		bool entered = false;
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(entry.Cancellation.Token);
		using (deadline.Token.Register(delegate
		{
			lock (_sync)
			{
				if (!entry.Completion.Task.IsCompleted)
				{
					entry.Expires = _seconds() + _failureCooldown.TotalSeconds;
					if (entry.Cancellation.IsCancellationRequested)
					{
						entry.Completion.TrySetCanceled(entry.Cancellation.Token);
					}
					else
					{
						entry.Completion.TrySetException(new TimeoutException("Market quote request timed out."));
					}
				}
			}
		}))
		{
			deadline.CancelAfter(_queueTimeout);
			try
			{
				await _slots.WaitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				entered = true;
				deadline.Token.ThrowIfCancellationRequested();
				deadline.CancelAfter(_timeout);
				T result = await _fetch(entry.Key, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				lock (_sync)
				{
					if (!entry.Completion.Task.IsCompleted)
					{
						entry.Expires = _seconds() + _successTtl.TotalSeconds;
						entry.Completion.TrySetResult(result);
					}
				}
			}
			catch (Exception exception)
			{
				lock (_sync)
				{
					if (!entry.Completion.Task.IsCompleted)
					{
						entry.Expires = _seconds() + _failureCooldown.TotalSeconds;
						entry.Completion.TrySetException(exception);
					}
				}
			}
			finally
			{
				if (entered)
				{
					_slots.Release();
				}
				entry.Cancellation.Dispose();
			}
		}
	}

	internal bool TryGetCompleted(string key, out T value)
	{
		lock (_sync)
		{
			if (_entries.TryGetValue(key, out Entry value2) && value2.Completion.Task.IsCompletedSuccessfully)
			{
				value = value2.Completion.Task.Result;
				return true;
			}
			value = default(T);
			return false;
		}
	}

	internal void CancelPending()
	{
		Cancel(clear: false, dispose: false);
	}

	internal void Clear()
	{
		Cancel(clear: true, dispose: false);
	}

	public void Dispose()
	{
		Cancel(clear: true, dispose: true);
	}

	private void Cancel(bool clear, bool dispose)
	{
		Entry[] array;
		Entry[] array2;
		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}
			if (dispose)
			{
				_disposed = true;
			}
			array = _entries.Values.Where((Entry e) => !e.Completion.Task.IsCompleted).ToArray();
			array2 = array;
			foreach (Entry entry in array2)
			{
				_entries.Remove(entry.Key);
				entry.Completion.TrySetCanceled();
			}
			if (clear)
			{
				_entries.Clear();
			}
		}
		array2 = array;
		foreach (Entry entry2 in array2)
		{
			try
			{
				entry2.Cancellation.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
		}
	}
}
