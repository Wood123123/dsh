using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.UI;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Models.Presence;
using Fika.Core.UI.Custom;
using HarmonyLib;
using JsonType;
using NekoParty.Core.Registry;
using NekoParty.Core.UI;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.State;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NekoParty.GroupEX.UI;

public class NekoPartyMainMenuController : MonoBehaviour
{
	private MainMenuUIScript _owner;

	private readonly Dictionary<string, string> _availableReceiversByName = new Dictionary<string, string>();

	private readonly HashSet<string> _partyMemberIds = new HashSet<string>();

	private NekoPartyData? _currentParty;

	private bool _isPartyLeader;

	private float _invitePollTimer;

	private int _pollCycle;

	private bool _invitePopupOpen;

	private bool _inviteJoinInProgress;

	private bool _bannerArenaInit;

	private bool _bannerArenaState;

	private static readonly Dictionary<int, string[]> _warningTextBackups;

	private static readonly Dictionary<int, Color[]> _warningImageColorBackups;

	private static readonly Color MainMenuWarningTint;

	private static readonly Color RewindWarningTint;

	private static readonly Color RecoveryWarningTint;

	private static readonly Color ArenaWarningTint;

	public static NekoPartyMainMenuController Instance { get; private set; }

	public static bool Exists
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Expected O, but got Unknown
			return (Object)Instance != (Object)null;
		}
	}

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0015: Expected O, but got Unknown
		if ((Object)Instance == (Object)this)
		{
			Instance = null;
		}
	}

	public void Initialize(MainMenuUIScript owner)
	{
		_owner = owner;
		RefreshRecoveryBanner();
	}

	private void Update()
	{
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		_invitePollTimer += Time.deltaTime;
		if (_invitePollTimer >= 5f)
		{
			_invitePollTimer = 0f;
			_pollCycle++;
			if (_pollCycle % 12 == 0)
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] MainMenu控制器心跳 tick=" + _pollCycle + " guard=" + ((!string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId)) ? "1" : "0") + "/" + ((NekoGroupState.PendingAcceptedRaidInvite != null) ? "1" : "0") + "/" + NekoGroupState.SuppressedRaidInviteIds.Count + " pending=" + NekoGroupState.PendingRaidInvites.Count + " ownerNull=" + ((Object)_owner == (Object)null), "RaidInviteDiag");
			}
			if (!NekoGroupExConfig.UseNativePartyInvite)
			{
				PollPartyInviteFallback();
			}
			PollRaidInviteFallback();
			NekoCoreRegistry.RaidExStatus?.PollPendingRecovery(showNotification: true);
			NekoCoreRegistry.RaidExStatus?.PollPendingRewind(showNotification: true);
		}
		if (!_invitePopupOpen && !_inviteJoinInProgress && (NekoGroupExConfig.UseNativePartyInvite || !TryShowNextPartyInvitePopup()))
		{
			TryShowNextRaidInvitePopup();
		}
		RefreshRecoveryBanner();
	}

	public void RefreshPlayerRows(FikaPlayerPresence[] responses)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)_owner == (Object)null)
		{
			return;
		}
		RefreshPartyContext();
		List<GameObject> value = Traverse.Create((object)_owner).Field("_players").GetValue<List<GameObject>>();
		if (value != null)
		{
			for (int i = 0; i < value.Count && i < responses.Length; i++)
			{
				NekoPartyPlayerRowUi.RemoveInlineActionButtons(value[i].transform);
				SetupGroupActionButton(value[i], responses[i]);
			}
			RefreshRecoveryBanner();
		}
	}

	public void RefreshPartyStateFromExternalChange()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((Object)_owner == (Object)null)
		{
			return;
		}
		try
		{
			Traverse.Create((object)_owner).Method("ClearAndQueryPlayers", Array.Empty<object>()).GetValue();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty MainMenu refresh failed: " + ex.Message, "RefreshPartyStateFromExternalChange");
		}
	}

	private void RefreshPartyContext()
	{
		NekoPartyMixedSideRuntime.SyncCurrentPreferredSideBestEffort();
		_availableReceiversByName.Clear();
		_partyMemberIds.Clear();
		_isPartyLeader = false;
		_currentParty = null;
		NekoGroupState.CurrentParty = null;
		try
		{
			NekoPartyOperationResponse currentParty = NekoGroupRequestHandler.GetCurrentParty();
			if (currentParty.Success && currentParty.Party.HasValue)
			{
				_currentParty = currentParty.Party;
				NekoGroupState.CurrentParty = currentParty.Party;
			}
			if (_currentParty.HasValue && _currentParty.Value.Members != null)
			{
				NekoPartyMemberData[] members = _currentParty.Value.Members;
				for (int i = 0; i < members.Length; i++)
				{
					NekoPartyMemberData nekoPartyMemberData = members[i];
					if (!string.IsNullOrEmpty(nekoPartyMemberData.ProfileId))
					{
						_partyMemberIds.Add(nekoPartyMemberData.ProfileId);
					}
				}
				_isPartyLeader = NekoPartyMixedSideRuntime.IsLocalProfileId(_currentParty.Value.LeaderProfileId);
			}
			foreach (KeyValuePair<string, string> item in NekoGroupRequestHandler.AvailableReceivers(FikaBackendUtils.Profile.ProfileId))
			{
				_availableReceiversByName[item.Key] = item.Value;
			}
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty RefreshPartyContext failed: " + ex.Message, "RefreshPartyContext");
		}
	}

	private bool HasPendingPartyInvite()
	{
		if (NekoGroupState.PendingPartyInvites.Count <= 0)
		{
			return !string.IsNullOrEmpty(NekoGroupState.PartyInviteInProgressId);
		}
		return true;
	}

	private void RefreshRecoveryBanner()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Expected O, but got Unknown
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		if ((Object)_owner == (Object)null)
		{
			return;
		}
		INekoRaidExStatus raidExStatus = NekoCoreRegistry.RaidExStatus;
		if (raidExStatus == null || !raidExStatus.GetPendingRecovery().HasValue)
		{
			NekoCoreRegistry.RaidExStatus?.PollPendingRecovery(showNotification: false);
		}
		INekoRaidExStatus raidExStatus2 = NekoCoreRegistry.RaidExStatus;
		if (raidExStatus2 == null || !raidExStatus2.GetPendingRewind().HasValue)
		{
			NekoCoreRegistry.RaidExStatus?.PollPendingRewind(showNotification: false);
		}
		MenuScreen val = ((Component)_owner).GetComponent<MenuScreen>() ?? ((Component)_owner).GetComponentInParent<MenuScreen>();
		if ((Object)val == (Object)null)
		{
			return;
		}
		GameObject value = Traverse.Create((object)val).Field("_alphaWarningGameObject").GetValue<GameObject>();
		GameObject value2 = Traverse.Create((object)val).Field("_warningGameObject").GetValue<GameObject>();
		bool flag = false;
		bool flag2 = !flag;
		if (flag2)
		{
			if ((Object)value != (Object)null)
			{
				value.SetActive(false);
			}
			if ((Object)value2 != (Object)null)
			{
				value2.SetActive(true);
			}
		}
		else
		{
			if ((Object)value != (Object)null)
			{
				value.SetActive(true);
			}
			if ((Object)value2 != (Object)null)
			{
				value2.SetActive(false);
			}
		}
		UpdateWarningBlock(value, flag);
		UpdateWarningBlock(value2, flag);
		bool arenaModeEnabled = NekoCoreRegistry.ArenaModeEnabled;
		if (_bannerArenaInit && _bannerArenaState == arenaModeEnabled)
		{
			return;
		}
		bool bannerArenaInit = _bannerArenaInit;
		_bannerArenaInit = true;
		_bannerArenaState = arenaModeEnabled;
		if (bannerArenaInit)
		{
			GameObject val2 = (flag2 ? value2 : value);
			if ((Object)val2 != (Object)null)
			{
				NekoUiCanvasFade.Play(val2, 0.2f, 1f, 0.3f);
			}
		}
	}

	private static void UpdateWarningBlock(GameObject warningRoot, bool reconnectAvailable)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)warningRoot == (Object)null)
		{
			return;
		}
		TextMeshProUGUI[] componentsInChildren = warningRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
		Image[] componentsInChildren2 = warningRoot.GetComponentsInChildren<Image>(true);
		if (componentsInChildren == null || componentsInChildren.Length == 0)
		{
			return;
		}
		int instanceID = ((Object)warningRoot).GetInstanceID();
		if (!_warningTextBackups.ContainsKey(instanceID))
		{
			_warningTextBackups[instanceID] = componentsInChildren.Select((TextMeshProUGUI x) => ((TMP_Text)x).text).ToArray();
		}
		if (!_warningImageColorBackups.ContainsKey(instanceID))
		{
			_warningImageColorBackups[instanceID] = componentsInChildren2.Select(delegate(Image x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				return ((Graphic)x).color;
			}).ToArray();
		}
		NekoRaidExRewindHint? nekoRaidExRewindHint = NekoCoreRegistry.RaidExStatus?.GetPendingRewind();
		NekoRaidExRecoveryHint? nekoRaidExRecoveryHint = NekoCoreRegistry.RaidExStatus?.GetPendingRecovery();
		if (!nekoRaidExRewindHint.HasValue && !nekoRaidExRecoveryHint.HasValue)
		{
			if (!reconnectAvailable)
			{
				if (NekoCoreRegistry.ArenaModeEnabled)
				{
					SetWarningTextsEnabled(componentsInChildren, enabled: true);
					ApplyWarningContent(componentsInChildren, LocalizationExtensions.Localized("NKP_ArenaMainMenuWarningTitle", (string)null), LocalizationExtensions.Localized("NKP_ArenaMainMenuWarningBody", (string)null));
					TintWarningImages(componentsInChildren2, ArenaWarningTint);
				}
				else
				{
					ApplyWarningContent(componentsInChildren, LocalizationExtensions.Localized("NKP_MainMenuWarningTitle", (string)null), LocalizationExtensions.Localized("NKP_MainMenuWarningBody", (string)null));
					TintWarningImages(componentsInChildren2, MainMenuWarningTint);
				}
			}
			else
			{
				string[] array = _warningTextBackups[instanceID];
				for (int num = 0; num < componentsInChildren.Length && num < array.Length; num++)
				{
					((TMP_Text)componentsInChildren[num]).text = array[num];
				}
				RestoreWarningImages(componentsInChildren2, _warningImageColorBackups[instanceID]);
			}
		}
		else
		{
			RestoreWarningImages(componentsInChildren2, _warningImageColorBackups[instanceID]);
			SetWarningTextsEnabled(componentsInChildren, enabled: true);
			string title;
			string body;
			if (nekoRaidExRewindHint.HasValue)
			{
				string arg = LocalizationExtensions.Localized(nekoRaidExRewindHint.Value.LocationId, (string)null);
				title = LocalizationExtensions.Localized("NKP_RewindMapTag", (string)null);
				body = string.Format(LocalizationExtensions.Localized("NKP_RewindPending", (string)null), arg);
				TintWarningImages(componentsInChildren2, RewindWarningTint);
			}
			else
			{
				string arg2 = LocalizationExtensions.Localized(nekoRaidExRecoveryHint.Value.LocationId, (string)null);
				title = LocalizationExtensions.Localized("NKP_RecoveryPanelTitle", (string)null);
				body = string.Format(LocalizationExtensions.Localized("NKP_RecoveryMenuHint", (string)null), arg2);
				TintWarningImages(componentsInChildren2, RecoveryWarningTint);
			}
			ApplyWarningContent(componentsInChildren, title, body);
		}
	}

	private static void ApplyWarningContent(TextMeshProUGUI[] texts, string title, string body)
	{
		if (texts != null && texts.Length != 0)
		{
			if (texts.Length == 1)
			{
				((TMP_Text)texts[0]).text = title + "\n" + body;
				return;
			}
			((TMP_Text)texts[0]).text = title;
			((TMP_Text)texts[1]).text = body;
		}
	}

	private static void TintWarningImages(Image[] images, Color tint)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (images == null)
		{
			return;
		}
		foreach (Image val in images)
		{
			if (!((Object)val == (Object)null))
			{
				Color color = ((Graphic)val).color;
				((Graphic)val).color = new Color(tint.r, tint.g, tint.b, color.a);
			}
		}
	}

	private static void RestoreWarningImages(Image[] images, Color[] originalColors)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (images == null || originalColors == null)
		{
			return;
		}
		for (int i = 0; i < images.Length && i < originalColors.Length; i++)
		{
			if ((Object)images[i] != (Object)null)
			{
				((Graphic)images[i]).color = originalColors[i];
			}
		}
	}

	private static void SetWarningTextsEnabled(TextMeshProUGUI[] texts, bool enabled)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		if (texts == null)
		{
			return;
		}
		foreach (TextMeshProUGUI val in texts)
		{
			if ((Object)val != (Object)null)
			{
				((Behaviour)val).enabled = enabled;
			}
		}
	}

	private void SetupGroupActionButton(GameObject rowObject, FikaPlayerPresence presence)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Expected O, but got Unknown
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Expected O, but got Unknown
		string profileId = FikaBackendUtils.Profile.ProfileId;
		string nickname = FikaBackendUtils.Profile.Nickname;
		bool flag = _currentParty.HasValue && _currentParty.Value.Members != null && _currentParty.Value.Members.Length != 0;
		string targetProfileId = null;
		if (_availableReceiversByName.TryGetValue(presence.Nickname, out var value))
		{
			targetProfileId = value;
		}
		else if ((Object)_owner != (Object)null)
		{
			Dictionary<string, string> value2 = Traverse.Create((object)_owner).Field("_availableReceiversByName").GetValue<Dictionary<string, string>>();
			if (value2 != null && value2.TryGetValue(presence.Nickname, out value))
			{
				targetProfileId = value;
			}
		}
		if (string.IsNullOrEmpty(targetProfileId) && presence.Nickname == nickname)
		{
			targetProfileId = profileId;
		}
		if (string.IsNullOrEmpty(targetProfileId))
		{
			return;
		}
		bool flag2 = targetProfileId == profileId;
		bool flag3 = _partyMemberIds.Contains(targetProfileId);
		TryApplySideLabel(rowObject, targetProfileId, presence.Nickname, flag3);
		bool canInvite = !flag2 && !HasPendingPartyInvite() && (!flag || _isPartyLeader) && !flag3;
		bool flag4 = (_isPartyLeader && !flag2) & flag3;
		bool flag5 = flag & flag2;
		if (!canInvite && !flag4 && !flag5)
		{
			return;
		}
		GameObject val = new GameObject("NekoPartyActionButton");
		val.transform.SetParent(rowObject.transform, false);
		RectTransform val2 = val.AddComponent<RectTransform>();
		val2.sizeDelta = new Vector2(88f, 28f);
		((Graphic)val.AddComponent<Image>()).color = (canInvite ? new Color(0.33f, 0.35f, 0.38f, 0.94f) : new Color(0.26f, 0.1f, 0.1f, 0.96f));
		Button obj = val.AddComponent<Button>();
		GameObject val3 = new GameObject("Label");
		val3.transform.SetParent(val.transform, false);
		RectTransform obj2 = val3.AddComponent<RectTransform>();
		obj2.anchorMin = Vector2.zero;
		obj2.anchorMax = Vector2.one;
		obj2.offsetMin = Vector2.zero;
		obj2.offsetMax = Vector2.zero;
		TextMeshProUGUI val4 = val3.AddComponent<TextMeshProUGUI>();
		((TMP_Text)val4).text = (canInvite ? LocalizationExtensions.Localized("NKP_PartyInvite", (string)null) : ((!flag5) ? LocalizationExtensions.Localized("NKP_PartyRemove", (string)null) : (_isPartyLeader ? LocalizationExtensions.Localized("NKP_PartyDisband", (string)null) : LocalizationExtensions.Localized("NKP_PartyLeave", (string)null))));
		((TMP_Text)val4).fontSize = 13f;
		((TMP_Text)val4).alignment = (TextAlignmentOptions)514;
		((Graphic)val4).color = Color.white;
		NekoPartyPlayerRowUi.FitButtonToLabel(val2, val4);
		NekoPartyPlayerRowUi.PlaceInlineActionButton(val2, rowObject);
		((UnityEvent)obj.onClick).AddListener((UnityAction)delegate
		{
			Singleton<GUISounds>.Instance.PlayUISound((EUISoundType)3);
			try
			{
				if (canInvite && !_isPartyLeader)
				{
					if (HasPendingPartyInvite())
					{
						NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null), (ENotificationDurationType)0);
						return;
					}
					NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.CreateParty();
					if (nekoPartyOperationResponse.Success && nekoPartyOperationResponse.Party.HasValue)
					{
						_currentParty = nekoPartyOperationResponse.Party;
						NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
						_isPartyLeader = NekoPartyMixedSideRuntime.IsLocalProfileId(_currentParty.Value.LeaderProfileId);
					}
				}
				if (canInvite && !_isPartyLeader)
				{
					NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null), (ENotificationDurationType)0);
				}
				else
				{
					NekoPartyOperationResponse nekoPartyOperationResponse2 = (canInvite ? NekoGroupRequestHandler.InvitePartyMember(targetProfileId) : NekoGroupRequestHandler.RemovePartyMember(targetProfileId));
					if (!nekoPartyOperationResponse2.Success)
					{
						NotificationManager.DisplayWarningNotification(string.IsNullOrEmpty(nekoPartyOperationResponse2.Error) ? LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null) : nekoPartyOperationResponse2.Error, (ENotificationDurationType)0);
					}
					else
					{
						_currentParty = nekoPartyOperationResponse2.Party;
						NekoGroupState.CurrentParty = nekoPartyOperationResponse2.Party;
						RefreshPartyStateFromExternalChange();
					}
				}
			}
			catch (Exception ex)
			{
				FikaGlobals.LogWarning("NekoParty SetupGroupActionButton failed: " + ex.Message, "SetupGroupActionButton");
			}
		});
	}

	private static void TryApplySideLabel(GameObject rowObject, string profileId, string nickname, bool inParty)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		if (inParty && !((Object)rowObject == (Object)null))
		{
			TextMeshProUGUI val = rowObject.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(delegate(TextMeshProUGUI x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Expected O, but got Unknown
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Expected O, but got Unknown
				return (Object)x != (Object)null && !string.IsNullOrWhiteSpace(((TMP_Text)x).text) && (Object)((TMP_Text)x).transform.parent != (Object)null && ((Object)((TMP_Text)x).transform.parent).name != "Label";
			});
			if (!((Object)val == (Object)null))
			{
				int partyMemberPreferredSide = NekoPartyMixedSideRuntime.GetPartyMemberPreferredSide(profileId, nickname);
				((TMP_Text)val).text = NekoPartyMixedSideRuntime.FormatDisplayName(nickname, partyMemberPreferredSide);
			}
		}
	}

	private void PollPartyInviteFallback()
	{
		try
		{
			NekoPendingPartyInvitesResponse pendingPartyInvites = NekoGroupRequestHandler.GetPendingPartyInvites();
			if (pendingPartyInvites.Invites == null)
			{
				return;
			}
			NekoPartyInviteData[] invites = pendingPartyInvites.Invites;
			for (int i = 0; i < invites.Length; i++)
			{
				NekoPartyInviteData nekoPartyInviteData = invites[i];
				if (!string.IsNullOrEmpty(nekoPartyInviteData.InviteId) && !NekoGroupState.PendingPartyInvites.ContainsKey(nekoPartyInviteData.InviteId))
				{
					NekoGroupState.PendingPartyInvites[nekoPartyInviteData.InviteId] = new NekoPartyInviteReceivedNotification
					{
						NekoType = "nekoparty.partyInviteReceived",
						InviteId = nekoPartyInviteData.InviteId,
						PartyId = nekoPartyInviteData.PartyId,
						LeaderProfileId = nekoPartyInviteData.LeaderProfileId,
						LeaderNickname = nekoPartyInviteData.LeaderNickname,
						TargetProfileId = nekoPartyInviteData.TargetProfileId,
						TargetNickname = nekoPartyInviteData.TargetNickname,
						FromProfile = nekoPartyInviteData.FromProfile,
						Members = nekoPartyInviteData.Members,
						ExpiresAtUnix = nekoPartyInviteData.ExpiresAtUnix
					};
				}
			}
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty PollPartyInviteFallback failed: " + ex.Message, "PollPartyInviteFallback");
		}
	}

	private void PollRaidInviteFallback()
	{
		if (!string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId) || NekoGroupState.PendingAcceptedRaidInvite != null || NekoGroupState.SuppressedRaidInviteIds.Count > 0)
		{
			return;
		}
		try
		{
			NekoPendingRaidInvitesResponse pendingRaidInvites = NekoGroupRequestHandler.GetPendingRaidInvites();
			if (pendingRaidInvites.Invites == null)
			{
				return;
			}
			NekoRaidInviteData[] invites = pendingRaidInvites.Invites;
			for (int i = 0; i < invites.Length; i++)
			{
				NekoRaidInviteData nekoRaidInviteData = invites[i];
				if (!string.IsNullOrEmpty(nekoRaidInviteData.InviteId) && !NekoGroupState.PendingRaidInvites.ContainsKey(nekoRaidInviteData.InviteId) && !NekoGroupState.SuppressedRaidInviteIds.Contains(nekoRaidInviteData.InviteId))
				{
					FikaGlobals.LogWarning("[NekoP2P][Diag] 轮询拉到战局邀请 leader=" + nekoRaidInviteData.LeaderNickname + " serverId=" + nekoRaidInviteData.ServerId + " loc=" + nekoRaidInviteData.Location, "RaidInviteDiag");
					NekoGroupState.PendingRaidInvites[nekoRaidInviteData.InviteId] = new NekoRaidInviteReceivedNotification
					{
						NekoType = "nekoparty.raidInviteReceived",
						InviteId = nekoRaidInviteData.InviteId,
						PartyId = nekoRaidInviteData.PartyId,
						LeaderProfileId = nekoRaidInviteData.LeaderProfileId,
						LeaderNickname = nekoRaidInviteData.LeaderNickname,
						TargetProfileId = nekoRaidInviteData.TargetProfileId,
						TargetNickname = nekoRaidInviteData.TargetNickname,
						ServerId = nekoRaidInviteData.ServerId,
						RaidCode = nekoRaidInviteData.RaidCode,
						Location = nekoRaidInviteData.Location,
						Side = nekoRaidInviteData.Side,
						TargetSide = nekoRaidInviteData.TargetSide,
						Time = nekoRaidInviteData.Time,
						ExpiresAtUnix = nekoRaidInviteData.ExpiresAtUnix
					};
				}
			}
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty PollRaidInviteFallback failed: " + ex.Message, "PollRaidInviteFallback");
		}
	}

	private bool TryShowNextPartyInvitePopup()
	{
		if (FikaGlobals.IsInRaid || NekoGroupState.PendingPartyInvites.Count == 0)
		{
			return false;
		}
		NekoPartyInviteReceivedNotification invite = null;
		foreach (KeyValuePair<string, NekoPartyInviteReceivedNotification> pendingPartyInvite in NekoGroupState.PendingPartyInvites)
		{
			if (!(pendingPartyInvite.Key == NekoGroupState.PartyInviteInProgressId))
			{
				invite = pendingPartyInvite.Value;
				break;
			}
		}
		if (invite == null)
		{
			return false;
		}
		_invitePopupOpen = true;
		if (!NekoPartyInviteUiRuntime.TryShowNativePartyInvitePopup(invite, delegate
		{
			_invitePopupOpen = false;
			HandlePartyInviteAccept(invite);
		}, delegate
		{
			_invitePopupOpen = false;
			HandlePartyInviteReject(invite);
		}))
		{
			ItemUiContext.Instance.ShowMessageWindow(string.Format(LocalizationExtensions.Localized("NKP_PartyInvitePopup", (string)null), invite.LeaderNickname), (Action)delegate
			{
				_invitePopupOpen = false;
				HandlePartyInviteAccept(invite);
			}, (Action)delegate
			{
				_invitePopupOpen = false;
				HandlePartyInviteReject(invite);
			}, (string)null, 0f, false, (TextAlignmentOptions)514);
		}
		return true;
	}

	private void TryShowNextRaidInvitePopup()
	{
		NekoRaidInviteReceivedNotification nekoRaidInviteReceivedNotification = NekoPartyMixedSideRuntime.PeekAutoJoinReannounceCandidate();
		if (nekoRaidInviteReceivedNotification != null)
		{
			FikaGlobals.LogWarning("[NekoP2P] 第一封已接受，静默接续开房真服并执行选人+自动加入 serverId=" + nekoRaidInviteReceivedNotification.ServerId + " loc=" + nekoRaidInviteReceivedNotification.Location, "RaidInviteDiag");
			NekoPartyMixedSideRuntime.TryConsumeAutoJoinReannounce(nekoRaidInviteReceivedNotification);
			HandleRaidInviteAccept(nekoRaidInviteReceivedNotification);
			return;
		}
		if (FikaGlobals.IsInRaid || NekoGroupState.PendingRaidInvites.Count == 0 || !string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId) || NekoGroupState.PendingAcceptedRaidInvite != null || NekoGroupState.SuppressedRaidInviteIds.Count > 0)
		{
			if (NekoGroupState.PendingRaidInvites.Count > 0)
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] 有待处理战局邀请但弹窗被卡 inRaid=" + FikaGlobals.IsInRaid + " inProgress=" + NekoGroupState.RaidInviteInProgressId + " accepted=" + (NekoGroupState.PendingAcceptedRaidInvite != null) + " suppressed=" + NekoGroupState.SuppressedRaidInviteIds.Count, "RaidInviteDiag");
			}
			return;
		}
		NekoGroupState.PurgeExpiredRaidInvites(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		NekoRaidInviteReceivedNotification invite = null;
		foreach (KeyValuePair<string, NekoRaidInviteReceivedNotification> pendingRaidInvite in NekoGroupState.PendingRaidInvites)
		{
			if (!(pendingRaidInvite.Key == NekoGroupState.RaidInviteInProgressId))
			{
				invite = pendingRaidInvite.Value;
				break;
			}
		}
		if ((invite == null || !NekoPartyMixedSideRuntime.TryConsumeAutoJoinReannounce(invite)) && invite != null && !TryShowNativeRaidInvitePopup(invite))
		{
			_invitePopupOpen = true;
			ItemUiContext.Instance.ShowMessageWindow(string.Format(LocalizationExtensions.Localized("NKP_RaidInvitePopup", (string)null), invite.LeaderNickname, LocalizationExtensions.Localized(invite.Location, (string)null)), (Action)delegate
			{
				_invitePopupOpen = false;
				HandleRaidInviteAccept(invite);
			}, (Action)delegate
			{
				_invitePopupOpen = false;
				HandleRaidInviteReject(invite);
			}, (string)null, 0f, false, (TextAlignmentOptions)514);
		}
	}

	private bool TryShowNativeRaidInvitePopup(NekoRaidInviteReceivedNotification invite)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		TarkovApplication val = null;
		if ((Object)ItemUiContext.Instance == (Object)null || !TarkovApplication.Exist(ref val))
		{
			return false;
		}
		IEftSession session = ((ClientApplication<IEftSession>)(object)val).Session;
		MainMenuShowOperation value = Traverse.Create((object)val).Field("_menuOperation").GetValue<MainMenuShowOperation>();
		if (session == null || value == null || value.InventoryController == null)
		{
			return false;
		}
		RaidSettings raidSettings_ = value.raidSettings_0;
		if (raidSettings_ == null)
		{
			return false;
		}
		NekoPartyMixedSideRuntime.PrepareInviteRaidSettings(raidSettings_, invite);
		_invitePopupOpen = true;
		bool handled = false;
		DialogWindowContext obj = ItemUiContext.Instance.ShowInviteInRaidWindow((IMatchmakerSession<RaidSettings>)(object)session, raidSettings_, value.InventoryController, CreateRaidInvitePlayer(invite));
		NekoPartyMixedSideRuntime.PatchNativeInviteWindowText(invite);
		((BaseWindowContext<bool>)(object)obj).OnAccept += delegate
		{
			if (!handled)
			{
				handled = true;
				_invitePopupOpen = false;
				HandleRaidInviteAccept(invite);
			}
		};
		((BaseWindowContext<bool>)(object)obj).OnDecline += delegate
		{
			if (!handled)
			{
				handled = true;
				_invitePopupOpen = false;
				HandleRaidInviteReject(invite);
			}
		};
		((BaseWindowContext<bool>)(object)obj).OnCloseSilent += delegate
		{
			if (!handled)
			{
				handled = true;
				_invitePopupOpen = false;
				HandleRaidInviteReject(invite);
			}
		};
		return true;
	}

	private static GroupPlayer CreateRaidInvitePlayer(NekoRaidInviteReceivedNotification invite)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_0074: Expected O, but got Unknown
		ESideType invitePreviewSide = NekoPartyMixedSideRuntime.GetInvitePreviewSide(invite);
		return new GroupPlayer
		{
			AccountId = invite.LeaderProfileId,
			Id = invite.LeaderProfileId,
			IsLeader = true,
			IsReady = true,
			Info = new PlayerInfo
			{
				Nickname = invite.LeaderNickname,
				Side = (EPlayerSide)(((int)invitePreviewSide != 1) ? 1 : 4),
				Level = 1,
				PrestigeLevel = 0,
				MemberCategory = (EMemberCategory)0,
				SelectedMemberCategory = (EMemberCategory)0
			}
		};
	}

	private void HandleRaidInviteAccept(NekoRaidInviteReceivedNotification invite)
	{
		if (_inviteJoinInProgress)
		{
			return;
		}
		_inviteJoinInProgress = true;
		try
		{
			NekoGroupState.RaidInviteInProgressId = invite.InviteId;
			NekoGroupState.SuppressedRaidInviteIds.Add(invite.InviteId);
			NekoGroupState.AutoJoinRaidInviteId = string.Empty;
			if (!TryOpenInviteJoinScreen(invite))
			{
				NekoGroupState.PendingAcceptedRaidInvite = null;
				NekoPartyMixedSideRuntime.ClearPendingInviteSelection();
				NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(invite.InviteId);
				NekoPartyMixedSideRuntime.ClearFikaInviteInProgress(invite.InviteId);
				NekoGroupState.RaidInviteInProgressId = string.Empty;
				NekoGroupState.SuppressedRaidInviteIds.Remove(invite.InviteId);
				NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_InviteAcceptFailed", (string)null), (ENotificationDurationType)0);
			}
			_inviteJoinInProgress = false;
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty HandleRaidInviteAccept failed: " + ex.Message, "HandleRaidInviteAccept");
			NekoGroupState.PendingAcceptedRaidInvite = null;
			NekoPartyMixedSideRuntime.ClearPendingInviteSelection();
			NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(invite.InviteId);
			NekoPartyMixedSideRuntime.ClearFikaInviteInProgress(invite.InviteId);
			NekoGroupState.RaidInviteInProgressId = string.Empty;
			NekoGroupState.SuppressedRaidInviteIds.Remove(invite.InviteId);
			NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_InviteAcceptFailed", (string)null), (ENotificationDurationType)0);
			_inviteJoinInProgress = false;
		}
	}

	private void HandleRaidInviteReject(NekoRaidInviteReceivedNotification invite)
	{
		try
		{
			NekoGroupRequestHandler.RejectRaidInvite(invite.InviteId);
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty HandleRaidInviteReject failed: " + ex.Message, "HandleRaidInviteReject");
		}
		finally
		{
			NekoGroupState.PendingAcceptedRaidInvite = null;
			NekoPartyMixedSideRuntime.ClearPendingInviteSelection();
			NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(invite.InviteId);
			NekoPartyMixedSideRuntime.ClearFikaInviteInProgress(invite.InviteId);
			NekoGroupState.SuppressedRaidInviteIds.Remove(invite.InviteId);
			if (NekoGroupState.RaidInviteInProgressId == invite.InviteId)
			{
				NekoGroupState.RaidInviteInProgressId = string.Empty;
			}
			NekoGroupState.PendingRaidInvites.Remove(invite.InviteId);
		}
	}

	private void HandlePartyInviteAccept(NekoPartyInviteReceivedNotification invite)
	{
		try
		{
			NekoGroupState.PartyInviteInProgressId = invite.InviteId;
			NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.AcceptPartyInvite(invite.InviteId);
			if (!nekoPartyOperationResponse.Success)
			{
				NotificationManager.DisplayWarningNotification(string.IsNullOrEmpty(nekoPartyOperationResponse.Error) ? LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null) : nekoPartyOperationResponse.Error, (ENotificationDurationType)0);
				return;
			}
			NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
			NekoGroupState.PendingPartyInvites.Remove(invite.InviteId);
			RefreshPartyStateFromExternalChange();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty HandlePartyInviteAccept failed: " + ex.Message, "HandlePartyInviteAccept");
			NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null), (ENotificationDurationType)0);
		}
		finally
		{
			if (NekoGroupState.PartyInviteInProgressId == invite.InviteId)
			{
				NekoGroupState.PartyInviteInProgressId = string.Empty;
			}
		}
	}

	private void HandlePartyInviteReject(NekoPartyInviteReceivedNotification invite)
	{
		try
		{
			NekoGroupRequestHandler.RejectPartyInvite(invite.InviteId);
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty HandlePartyInviteReject failed: " + ex.Message, "HandlePartyInviteReject");
		}
		finally
		{
			if (NekoGroupState.PartyInviteInProgressId == invite.InviteId)
			{
				NekoGroupState.PartyInviteInProgressId = string.Empty;
			}
			NekoGroupState.PendingPartyInvites.Remove(invite.InviteId);
			RefreshPartyStateFromExternalChange();
		}
	}

	private bool TryOpenInviteJoinScreen(NekoRaidInviteReceivedNotification invite)
	{
		return NekoPartyMixedSideRuntime.OpenInviteSideSelection(invite);
	}

	static NekoPartyMainMenuController()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		_warningTextBackups = new Dictionary<int, string[]>();
		_warningImageColorBackups = new Dictionary<int, Color[]>();
		MainMenuWarningTint = new Color(0.3f, 0.82f, 0.86f, 1f);
		RewindWarningTint = new Color(0.42f, 0.78f, 1f, 1f);
		RecoveryWarningTint = new Color(0.19f, 0.84f, 0.58f, 1f);
		ArenaWarningTint = new Color(0.86f, 0.27f, 0.24f, 1f);
	}
}
