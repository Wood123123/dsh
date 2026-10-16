using EFT;
using EFT.Communications;
using Fika.Core.UI;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Notifications;

public class NekoPartyMemberLeftNotification : Notification
{
	[JsonProperty("nekoType")]
	public string NekoType;

	[JsonProperty("memberProfileId")]
	public string MemberProfileId;

	[JsonProperty("memberNickname")]
	public string MemberNickname;

	public override ENotificationIconType Icon => (ENotificationIconType)2;

	public override string Description => string.Format(LocalizationExtensions.Localized("NKP_PartyMemberLeftNotification", (string)null), FikaUIGlobals.ColorizeText((EColor)2, MemberNickname));
}
