using EFT;
using EFT.Communications;
using Fika.Core.UI;
using NekoParty.GroupEX.Models;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Notifications;

public class NekoPartyInviteReceivedNotification : Notification
{
	[JsonProperty("nekoType")]
	public string NekoType;

	[JsonProperty("inviteId")]
	public string InviteId;

	[JsonProperty("partyId")]
	public string PartyId;

	[JsonProperty("leaderProfileId")]
	public string LeaderProfileId;

	[JsonProperty("leaderNickname")]
	public string LeaderNickname;

	[JsonProperty("targetProfileId")]
	public string TargetProfileId;

	[JsonProperty("targetNickname")]
	public string TargetNickname;

	[JsonProperty("fromProfile")]
	public NekoPartyInvitePlayerData FromProfile;

	[JsonProperty("members")]
	public NekoPartyInvitePlayerData[] Members;

	[JsonProperty("expiresAtUnix")]
	public long ExpiresAtUnix;

	public override ENotificationIconType Icon => (ENotificationIconType)2;

	public override string Description
	{
		get
		{
			string arg = FikaUIGlobals.ColorizeText((EColor)2, LeaderNickname);
			return string.Format(LocalizationExtensions.Localized("NKP_PartyInviteNotification", (string)null), arg);
		}
	}
}
