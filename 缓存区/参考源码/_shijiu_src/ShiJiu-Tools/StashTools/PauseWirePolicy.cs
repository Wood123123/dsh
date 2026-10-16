using System;
using System.Collections.Generic;
using System.Linq;

namespace StashTools;

internal static class PauseWirePolicy
{
	internal const int Protocol = 10402;

	internal const int MaxMembers = 64;

	internal const int MaxJson = 16384;

	internal const double HeartbeatSeconds = 1.0;

	internal const double LeaseSeconds = 6.0;

	internal static bool ValidMembers(IReadOnlyCollection<PauseMember>? members, string host, string local)
	{
		if (members != null)
		{
			int count = members.Count;
			if (count > 0 && count <= 64 && members.All((PauseMember m) => m != null && !string.IsNullOrWhiteSpace(m.Id) && m.Id.Length <= 80 && m.Name != null && m.Name.Length <= 80) && members.Select((PauseMember m) => m.Id).Distinct<string>(StringComparer.Ordinal).Count() == members.Count && members.Any((PauseMember m) => m.Id == host))
			{
				return members.Any((PauseMember m) => m.Id == local);
			}
		}
		return false;
	}
}
