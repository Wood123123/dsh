using System.Runtime.Serialization;
using NekoParty.Core.Models;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRewindCorpseSnapshotData
{
	[DataMember(Name = "corpseId")]
	public string CorpseId;

	[DataMember(Name = "profileId")]
	public string ProfileId;

	[DataMember(Name = "nickname")]
	public string Nickname;

	[DataMember(Name = "side")]
	public int Side;

	[DataMember(Name = "role")]
	public int Role;

	[DataMember(Name = "positionX")]
	public float PositionX;

	[DataMember(Name = "positionY")]
	public float PositionY;

	[DataMember(Name = "positionZ")]
	public float PositionZ;

	[DataMember(Name = "rotationY")]
	public float RotationY;

	[DataMember(Name = "inventoryDescriptorBase64")]
	public string InventoryDescriptorBase64;

	[DataMember(Name = "bones")]
	public NekoRecoveryBoneData[] Bones;
}
