using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRewindStatusResponse
{
	[DataMember(Name = "hasPendingRewind")]
	public bool HasPendingRewind;

	[DataMember(Name = "snapshot")]
	public NekoRewindSnapshotData? Snapshot;
}
