using System;
using System.Threading.Tasks;
using StashTools.History;

namespace StashTools;

internal static class HistoryDelivery
{
	internal static async Task<HistoryReply> Send(RaidRecord record, Func<string, object, Task<HistoryReply>> request, Func<bool> sameSession)
	{
		if (!sameSession())
		{
			return Changed();
		}
		if (record.Team != null)
		{
			HistoryReply historyReply = await request("join", new TeamJoin
			{
				Context = record.Team,
				RecordId = record.Id,
				StartedUtc = record.StartedUtc
			});
			if (!historyReply.Ok)
			{
				return historyReply;
			}
			if (!sameSession())
			{
				return Changed();
			}
		}
		return await request("save", record);
		static HistoryReply Changed()
		{
			return new HistoryReply
			{
				Error = "登录账号或服务器已变化，记录保留待下次上传。"
			};
		}
	}
}
