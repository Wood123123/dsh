using System.Runtime.Serialization;
using NekoParty.Core.Models;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRewindSnapshotUploadRequest
{
	[DataMember(Name = "locationId")]
	public string LocationId;

	[DataMember(Name = "time")]
	public int Time;

	[DataMember(Name = "sampleAgeSeconds")]
	public int SampleAgeSeconds;

	[DataMember(Name = "remainingRaidSeconds")]
	public int RemainingRaidSeconds;

	[DataMember(Name = "playerPositionX")]
	public float PlayerPositionX;

	[DataMember(Name = "playerPositionY")]
	public float PlayerPositionY;

	[DataMember(Name = "playerPositionZ")]
	public float PlayerPositionZ;

	[DataMember(Name = "playerRotationY")]
	public float PlayerRotationY;

	[DataMember(Name = "playerHealthNormalized")]
	public float PlayerHealthNormalized;

	[DataMember(Name = "playerInventoryDescriptorBase64")]
	public string PlayerInventoryDescriptorBase64;

	[DataMember(Name = "playerBones")]
	public NekoRecoveryBoneData[] PlayerBones;

	[DataMember(Name = "ais")]
	public NekoRewindAiSnapshotData[] Ais;

	[DataMember(Name = "corpses")]
	public NekoRewindCorpseSnapshotData[] Corpses;

	[DataMember(Name = "looseItems")]
	public NekoRewindLooseItemSnapshotData[] LooseItems;
}
