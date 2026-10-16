using System.Collections.Generic;
using System.Linq;

namespace StashTools.Fika;

internal sealed class FikaPauseReadiness
{
	internal string LocalId { get; private set; } = "";

	internal string HostId { get; private set; } = "";

	internal string MapId { get; private set; } = "";

	internal string Reason { get; private set; } = "";

	internal bool CanHandshake { get; private set; }

	internal bool Ready { get; private set; }

	internal bool Observe(string? local, string? map, bool host, bool transportReady, bool raidStarted, bool gameStarted, bool timerStarted)
	{
		local = (ValidId(local) ? local : "");
		map = (ValidId(map) ? map : "");
		bool result = (LocalId.Length > 0 && LocalId != local) || (MapId.Length > 0 && MapId != map);
		LocalId = local;
		MapId = map;
		HostId = (host ? local : "");
		CanHandshake = transportReady && local.Length > 0 && map.Length > 0;
		Ready = CanHandshake & raidStarted & gameStarted & timerStarted;
		Reason = ((local.Length == 0) ? "本机玩家身份尚未生成" : ((map.Length == 0) ? "地图信息尚未准备好" : ((!transportReady) ? "暂停网络接口尚未完成注册" : ((!raidStarted) ? "等待房主开始战局" : ((!gameStarted) ? "战局尚未进入已开始状态" : ((!timerStarted) ? "战局计时尚未开始" : ""))))));
		return result;
	}

	internal bool CapabilityReady(bool prepared, bool clockReady, bool acceptedRoom)
	{
		return Ready & prepared & clockReady & acceptedRoom;
	}

	internal static bool CompleteTransportRoster(IEnumerable<string?> livePeerIds, IReadOnlyCollection<PauseMember> members, string local)
	{
		return livePeerIds.All((string id) => ValidId(id) && id != local && members.Any((PauseMember m) => m.Id == id));
	}

	private static bool ValidId(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Length <= 80;
		}
		return false;
	}

	internal void Reset()
	{
		LocalId = "";
		HostId = "";
		MapId = "";
		Reason = "";
		CanHandshake = false;
		Ready = false;
	}
}
