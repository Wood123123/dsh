using EFT;
using EFT.Communications;
using Fika.Core.UI;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Notifications;

public class NekoRaidInviteReceivedNotification : Notification
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

	[JsonProperty("serverId")]
	public string ServerId;

	[JsonProperty("raidCode")]
	public string RaidCode;

	[JsonProperty("location")]
	public string Location;

	[JsonProperty("side")]
	public int Side;

	[JsonProperty("targetSide")]
	public int TargetSide;

	[JsonProperty("time")]
	public int Time;

	[JsonProperty("expiresAtUnix")]
	public long ExpiresAtUnix;

	public override ENotificationIconType Icon => (ENotificationIconType)2;

	public override string Description
	{
		get
		{
			string arg = FikaUIGlobals.ColorizeText((EColor)2, LeaderNickname);
			return string.Format(LocalizationExtensions.Localized("NKP_RaidInviteNotification", (string)null), arg);
		}
	}
}
