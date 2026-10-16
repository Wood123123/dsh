using System.Runtime.Serialization;
using NekoParty.Core.Models;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRecoverySnapshotUploadRequest
{
	[DataMember(Name = "locationId")]
	public string LocationId;

	[DataMember(Name = "time")]
	public int Time;

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
