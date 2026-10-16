using System.Runtime.Serialization;
using NekoParty.Core.Models;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaRespawnResponse
{
	[DataMember(Name = "success")]
	public bool Success;

	[DataMember(Name = "error")]
	public string Error;

	[DataMember(Name = "respawnsUsed")]
	public int RespawnsUsed;

	[DataMember(Name = "respawnsLimit")]
	public int RespawnsLimit;

	[DataMember(Name = "respawnsRemaining")]
	public int RespawnsRemaining;

	[DataMember(Name = "locationId")]
	public string LocationId;

	[DataMember(Name = "initialLoadoutBase64")]
	public string InitialLoadoutBase64;

	[DataMember(Name = "deathSnapshotBase64")]
	public string DeathSnapshotBase64;

	[DataMember(Name = "deathPositionX")]
	public float DeathPositionX;

	[DataMember(Name = "deathPositionY")]
	public float DeathPositionY;

	[DataMember(Name = "deathPositionZ")]
	public float DeathPositionZ;

	[DataMember(Name = "deathRotationY")]
	public float DeathRotationY;

	[DataMember(Name = "deathBones")]
	public NekoRecoveryBoneData[] DeathBones;

	[DataMember(Name = "handsItemId")]
	public string HandsItemId;
}
