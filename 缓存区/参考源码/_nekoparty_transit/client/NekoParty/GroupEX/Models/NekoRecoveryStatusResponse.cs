using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRecoveryStatusResponse
{
	[DataMember(Name = "hasPendingRecovery")]
	public bool HasPendingRecovery;

	[DataMember(Name = "snapshot")]
	public NekoRecoverySnapshotData? Snapshot;
}
