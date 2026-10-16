using System.Runtime.Serialization;

namespace NekoParty.GroupEX.Models;

[DataContract]
public enum ENekoInviteStatus
{
	Pending,
	Accepted,
	Rejected,
	Expired
}
