using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using FikaServer.Models.Enums;
using FikaServer.Models.Fika.WebSocket;

namespace NekoParty.Server.GroupEX.Models;

public abstract record NekoPartyNotificationBase : IFikaNotification
{
	[JsonPropertyName("type")]
	public EFikaNotification Type
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CType_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CType_003Ek__BackingField = value;
		}
	}

	[JsonPropertyName("nekoType")]
	public string NekoType { get; init; }
}
