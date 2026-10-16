using EFT;
using EFT.Communications;
using Fika.Core.UI;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Notifications;

public class NekoPartyDisbandedNotification : Notification
{
	[JsonProperty("nekoType")]
	public string NekoType;

	[JsonProperty("leaderProfileId")]
	public string LeaderProfileId;

	[JsonProperty("leaderNickname")]
	public string LeaderNickname;

	public override ENotificationIconType Icon => (ENotificationIconType)2;

	public override string Description => string.Format(LocalizationExtensions.Localized("NKP_PartyDisbandedNotification", (string)null), FikaUIGlobals.ColorizeText((EColor)5, LeaderNickname));
}
