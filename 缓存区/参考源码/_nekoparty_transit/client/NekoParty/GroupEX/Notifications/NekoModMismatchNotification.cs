using EFT.Communications;
using Fika.Core.UI;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Notifications;

public class NekoModMismatchNotification : Notification
{
	[JsonProperty("nekoType")]
	public string NekoType;

	[JsonProperty("peerNickname")]
	public string PeerNickname;

	[JsonProperty("report")]
	public string Report;

	[JsonProperty("blocked")]
	public bool Blocked;

	public override ENotificationIconType Icon => (ENotificationIconType)1;

	public override string Description
	{
		get
		{
			if (!string.IsNullOrEmpty(PeerNickname))
			{
				return "与 " + FikaUIGlobals.ColorizeText((EColor)5, PeerNickname) + " 的服务器 MOD 配置不一致";
			}
			return "跨服 MOD 校验发现差异";
		}
	}
}
