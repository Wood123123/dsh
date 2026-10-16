using System;
using System.Collections.Concurrent;

namespace StashTools;

public static class RaidPauseCoopBridge
{
	internal sealed record Message(string Provider, string Room, string Sender, long Sequence, PauseRoomState? State, bool Failure = false);

	private static readonly ConcurrentQueue<Message> Inbox = new ConcurrentQueue<Message>();

	private static string _provider = "";

	internal static RaidPauseCoopAdapter? Adapter { get; private set; }

	public static void Register(string provider, RaidPauseCoopAdapter adapter)
	{
		if (string.IsNullOrWhiteSpace(provider) || adapter == null || adapter.Capture == null || adapter.SendPressToHost == null || adapter.BroadcastFromHost == null || adapter.SetGameplayPaused == null || adapter.ReportFailure == null)
		{
			throw new ArgumentException("暂停适配器未完整配置");
		}
		if (Adapter != null)
		{
			throw new InvalidOperationException("已有暂停适配器，请先注销");
		}
		_provider = provider;
		Adapter = adapter;
		Inbox.Clear();
	}

	public static void Unregister(string provider)
	{
		if (!(provider != _provider))
		{
			Adapter = null;
			_provider = "";
			Inbox.Clear();
		}
	}

	public static void ReceivePress(string provider, string room, string authenticatedSender, long sequence)
	{
		if (Adapter != null && provider == _provider)
		{
			Inbox.Enqueue(new Message(provider, room, authenticatedSender, sequence, null));
		}
	}

	public static void ReceiveState(string provider, string authenticatedHost, PauseRoomState state)
	{
		if (Adapter != null && provider == _provider && state != null)
		{
			Inbox.Enqueue(new Message(provider, state.RoomId, authenticatedHost, 0L, state.Copy()));
		}
	}

	public static void ReceiveFailure(string provider, string room, string authenticatedSender)
	{
		if (Adapter != null && provider == _provider)
		{
			Inbox.Enqueue(new Message(provider, room, authenticatedSender, 0L, null, Failure: true));
		}
	}

	internal static bool Read(out Message? message)
	{
		return Inbox.TryDequeue(out message);
	}
}
