using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public struct NekoRewindLooseItemSnapshotData
{
	[DataMember(Name = "itemId")]
	public string ItemId;

	[DataMember(Name = "itemBase64")]
	public string ItemBase64;

	[DataMember(Name = "positionX")]
	public float PositionX;

	[DataMember(Name = "positionY")]
	public float PositionY;

	[DataMember(Name = "positionZ")]
	public float PositionZ;

	[DataMember(Name = "rotationX")]
	public float RotationX;

	[DataMember(Name = "rotationY")]
	public float RotationY;

	[DataMember(Name = "rotationZ")]
	public float RotationZ;

	[DataMember(Name = "rotationW")]
	public float RotationW;
}
