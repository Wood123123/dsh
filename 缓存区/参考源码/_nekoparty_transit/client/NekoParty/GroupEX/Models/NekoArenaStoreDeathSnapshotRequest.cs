using System.Runtime.Serialization;
using NekoParty.Core.Models;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoArenaStoreDeathSnapshotRequest
{
	[DataMember(Name = "locationId")]
	public string LocationId;

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

	[DataMember(Name = "handsItemId")]
	public string HandsItemId;
}
