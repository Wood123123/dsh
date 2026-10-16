using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Models.Presence;
using HarmonyLib;
using JsonType;
using NekoParty.Core.Registry;
using NekoParty.GroupEX.CrossRaid;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.Patches;
using NekoParty.GroupEX.State;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NekoParty.GroupEX.UI;

public class NekoPartyMatchMakerController : MonoBehaviour
{
	internal DefaultUIButton AcceptButton;

	internal RaidSettings RaidSettings;

	internal DefaultUIButton BackButton;

	internal MatchMakerAcceptScreen AcceptScreen;

	private string _profileId;

	private bool _started;

	private float _invitePollTimer;

	private int _pollCycle;

	private bool _invitePopupOpen;

	private bool _inviteJoinInProgress;

	private GameObject _partyPanel;

	private TextMeshProUGUI _partyHeaderText;

	private TextMeshProUGUI _partyInfoText;

	private TextMeshProUGUI _confirmRecoveryHintText;

	private TMP_Dropdown _partyInviteDropdown;

	private TMP_Dropdown _partyMemberDropdown;

	private Button _partyInviteButton;

	private Button _partyRemoveButton;

	private Button _partyRefreshButton;

	private RectTransform _playerListContent;

	private readonly List<GameObject> _playerRows = new List<GameObject>();

	private readonly Dictionary<string, string> _availableReceiversByName = new Dictionary<string, string>();

	private readonly HashSet<string> _partyMemberIds = new HashSet<string>();

	private bool _isPartyLeader;

	private readonly Dictionary<string, string> _partyInviteCandidates = new Dictionary<string, string>();

	private readonly Dictionary<string, string> _partyRemovableMembers = new Dictionary<string, string>();

	private Coroutine _groupPreviewRebuildRoutine;

	private Coroutine _visualRefreshRoutine;

	private Coroutine _visualEnsureRoutine;

	private GameObject _hostRaidButton;

	private GameObject _p2pStatusPanel;

	private TextMeshProUGUI _p2pStatusText;

	private float _p2pRefreshTimer;

	public static NekoPartyMatchMakerController Instance { get; private set; }

	public static bool Exists
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Expected O, but got Unknown
			return (Object)Instance != (Object)null;
		}
	}

	internal bool IsReadyForExternalInvite
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected O, but got Unknown
			if (_started && !string.IsNullOrEmpty(_profileId))
			{
				return (Object)AcceptButton != (Object)null;
			}
			return false;
		}
	}

	private void Awake()
	{
		Instance = this;
	}

	private void Start()
	{
		_profileId = FikaBackendUtils.Profile.ProfileId;
		EnsureConfirmRecoveryHint();
		RefreshConfirmRecoveryHint();
		_started = true;
		((MonoBehaviour)this).StartCoroutine(DeferredAttachPartyPanel());
		InitializePartyState();
	}

	public void EnsurePartyPanelAttached()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		MatchMakerUI componentInChildren = ((Component)this).GetComponentInChildren<MatchMakerUI>(true);
		Transform val = (((Object)componentInChildren != (Object)null) ? ((Component)componentInChildren).transform : ((Component)this).transform);
		if ((Object)_partyPanel == (Object)null)
		{
			BuildPartyPanel(val);
		}
		else
		{
			_partyPanel.transform.SetParent(val, false);
			ConfigurePanelRect(_partyPanel.GetComponent<RectTransform>());
		}
		SuppressFikaPartyPanel(val);
		_partyPanel.transform.SetAsLastSibling();
		_partyPanel.SetActive(!NekoGroupExConfig.UseNativePartyInvite);
		RefreshPartyPanelData();
		ApplyPartyRaidUiLayout(componentInChildren);
		EnsureP2PStatusPanel(val);
	}

	private void EnsureP2PStatusPanel(Transform parent)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Expected O, but got Unknown
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Expected O, but got Unknown
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Expected O, but got Unknown
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)_p2pStatusPanel != (Object)null)
		{
			_p2pStatusPanel.transform.SetParent(parent, false);
			_p2pStatusPanel.transform.SetAsLastSibling();
			return;
		}
		_p2pStatusPanel = new GameObject("NekoP2PStatusPanel");
		_p2pStatusPanel.transform.SetParent(parent, false);
		RectTransform val = _p2pStatusPanel.AddComponent<RectTransform>();
		val.anchorMin = new Vector2(0f, 0f);
		val.anchorMax = new Vector2(0f, 0f);
		val.pivot = new Vector2(0f, 0f);
		val.sizeDelta = new Vector2(340f, 96f);
		val.anchoredPosition = new Vector2(16f, 96f);
		((Transform)val).localScale = Vector3.one;
		((Graphic)_p2pStatusPanel.AddComponent<Image>()).color = new Color(0.03f, 0.04f, 0.05f, 0.88f);
		Outline obj = _p2pStatusPanel.AddComponent<Outline>();
		((Shadow)obj).effectColor = new Color(0.6f, 0.62f, 0.66f, 0.45f);
		((Shadow)obj).effectDistance = new Vector2(1f, -1f);
		GameObject val2 = new GameObject("Header");
		val2.transform.SetParent((Transform)val, false);
		RectTransform obj2 = val2.AddComponent<RectTransform>();
		obj2.anchorMin = new Vector2(0f, 1f);
		obj2.anchorMax = new Vector2(1f, 1f);
		obj2.pivot = new Vector2(0.5f, 1f);
		obj2.sizeDelta = new Vector2(0f, 28f);
		obj2.anchoredPosition = Vector2.zero;
		TextMeshProUGUI obj3 = val2.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj3).text = "队伍 P2P 连接";
		((TMP_Text)obj3).fontSize = 15f;
		((Graphic)obj3).color = new Color(0.85f, 0.87f, 0.9f, 1f);
		((TMP_Text)obj3).alignment = (TextAlignmentOptions)4097;
		((TMP_Text)obj3).margin = new Vector4(12f, 0f, 0f, 0f);
		GameObject val3 = new GameObject("Body");
		val3.transform.SetParent((Transform)val, false);
		RectTransform obj4 = val3.AddComponent<RectTransform>();
		obj4.anchorMin = new Vector2(0f, 0f);
		obj4.anchorMax = new Vector2(1f, 1f);
		obj4.offsetMin = new Vector2(12f, 8f);
		obj4.offsetMax = new Vector2(-12f, -28f);
		_p2pStatusText = val3.AddComponent<TextMeshProUGUI>();
		((TMP_Text)_p2pStatusText).fontSize = 14f;
		((Graphic)_p2pStatusText).color = new Color(0.96f, 0.96f, 0.96f, 1f);
		((TMP_Text)_p2pStatusText).alignment = (TextAlignmentOptions)257;
		((TMP_Text)_p2pStatusText).richText = true;
		((TMP_Text)_p2pStatusText).enableWordWrapping = false;
		((TMP_Text)_p2pStatusText).overflowMode = (TextOverflowModes)1;
		_p2pStatusPanel.SetActive(false);
	}

	private void RefreshP2PStatusPanel()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)_p2pStatusPanel == (Object)null || (Object)_p2pStatusText == (Object)null)
		{
			return;
		}
		IReadOnlyList<NekoP2PMemberStatus> snapshot = NekoPartyP2PProbe.GetSnapshot();
		if (snapshot == null || snapshot.Count == 0)
		{
			if (_p2pStatusPanel.activeSelf)
			{
				_p2pStatusPanel.SetActive(false);
			}
			return;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		foreach (NekoP2PMemberStatus item in snapshot)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append('\n');
			}
			if (item.IsLeader)
			{
				stringBuilder.Append("<color=#c9a86a>[队长]</color> ");
			}
			stringBuilder.Append("<color=#e6e6e6>").Append(item.Nickname).Append("</color>");
			if (item.IsSelf)
			{
				stringBuilder.Append(" <color=#9aa0a6>（你）</color>");
				AppendNatLabel(stringBuilder, item.NatType);
				continue;
			}
			stringBuilder.Append("  ");
			switch (item.State)
			{
			case NekoP2PState.Direct:
				stringBuilder.Append("<color=#36c393>直连");
				if (item.RttMs >= 0)
				{
					stringBuilder.Append(' ').Append(item.RttMs).Append("ms");
				}
				stringBuilder.Append("</color>");
				break;
			case NekoP2PState.Punching:
				stringBuilder.Append("<color=#e0b73c>打洞中…</color>");
				break;
			case NekoP2PState.Failed:
				stringBuilder.Append("<color=#e73636>直连不通</color>");
				break;
			default:
				stringBuilder.Append("<color=#9aa0a6>等待连接</color>");
				break;
			}
			AppendNatLabel(stringBuilder, item.NatType);
		}
		((TMP_Text)_p2pStatusText).text = stringBuilder.ToString();
		float num = 36f + (float)snapshot.Count * 20f;
		RectTransform val = (RectTransform)_p2pStatusPanel.transform;
		val.sizeDelta = new Vector2(val.sizeDelta.x, num);
		if (!_p2pStatusPanel.activeSelf)
		{
			_p2pStatusPanel.SetActive(true);
		}
	}

	private static void AppendNatLabel(StringBuilder sb, NekoNatType nat)
	{
		string value = NekoNatTypeDetector.Label(nat);
		if (!string.IsNullOrEmpty(value))
		{
			string value2;
			switch (nat)
			{
			case NekoNatType.Open:
				value2 = "#36c393";
				break;
			case NekoNatType.Moderate:
				value2 = "#e0b73c";
				break;
			case NekoNatType.Symmetric:
			case NekoNatType.Blocked:
				value2 = "#e73636";
				break;
			default:
				value2 = "#9aa0a6";
				break;
			}
			sb.Append(" <color=").Append(value2).Append('>')
				.Append('[')
				.Append(value)
				.Append(']')
				.Append("</color>");
		}
	}

	private IEnumerator DeferredAttachPartyPanel()
	{
		yield return null;
		EnsurePartyPanelAttached();
	}

	public void ScheduleGroupPreviewRebuild(MatchMakerAcceptScreen screen)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if (!((Object)screen == (Object)null) && ((Behaviour)this).isActiveAndEnabled)
		{
			if (_groupPreviewRebuildRoutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(_groupPreviewRebuildRoutine);
			}
			_groupPreviewRebuildRoutine = ((MonoBehaviour)this).StartCoroutine(GroupPreviewRebuildRoutine(screen));
		}
	}

	private IEnumerator GroupPreviewRebuildRoutine(MatchMakerAcceptScreen screen)
	{
		float[] array = new float[3] { 0.5f, 0.8f, 1.2f };
		float[] array2 = array;
		foreach (float num in array2)
		{
			yield return (object)new WaitForSeconds(num);
			if ((Object)screen == (Object)null)
			{
				yield break;
			}
			NekoPartyPreparePreview.RebuildGroupPreview(screen);
		}
		NekoPartyPreparePreview.MarkGroupPreviewWarmed(NekoPartyPreparePreview.GetController(screen));
		_groupPreviewRebuildRoutine = null;
	}

	public void RequestGroupPreviewRebuild()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		if (((Behaviour)this).isActiveAndEnabled && !((Object)AcceptScreen == (Object)null))
		{
			if (_visualRefreshRoutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(_visualRefreshRoutine);
			}
			_visualRefreshRoutine = ((MonoBehaviour)this).StartCoroutine(DeferredVisualRefresh());
		}
	}

	private IEnumerator DeferredVisualRefresh()
	{
		float[] array = new float[3] { 0f, 0.4f, 0.9f };
		float[] array2 = array;
		foreach (float num in array2)
		{
			if (num <= 0f)
			{
				yield return null;
			}
			else
			{
				yield return (object)new WaitForSeconds(num);
			}
			if ((Object)AcceptScreen == (Object)null)
			{
				_visualRefreshRoutine = null;
				yield break;
			}
			NekoPartyPreparePreview.RebuildGroupPreview(AcceptScreen);
		}
		_visualRefreshRoutine = null;
	}

	public void ScheduleVisualEnsure(MatchMakerAcceptScreen screen)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if (!((Object)screen == (Object)null) && ((Behaviour)this).isActiveAndEnabled)
		{
			if (_visualEnsureRoutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(_visualEnsureRoutine);
			}
			_visualEnsureRoutine = ((MonoBehaviour)this).StartCoroutine(VisualEnsureRoutine(screen));
		}
	}

	private IEnumerator VisualEnsureRoutine(MatchMakerAcceptScreen screen)
	{
		float[] array = new float[4] { 0.3f, 1.5f, 3.5f, 7f };
		int attempt = 0;
		float[] array2 = array;
		foreach (float num in array2)
		{
			yield return (object)new WaitForSeconds(num);
			if ((Object)screen == (Object)null || !((Behaviour)this).isActiveAndEnabled)
			{
				yield break;
			}
			NekoPartyPreparePreview.SafeEnsureVisuals(screen, attempt);
			attempt++;
		}
		_visualEnsureRoutine = null;
	}

	private static void ConfigurePanelRect(RectTransform panelRect)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		panelRect.anchorMin = new Vector2(0f, 0.5f);
		panelRect.anchorMax = new Vector2(0f, 0.5f);
		panelRect.pivot = new Vector2(0f, 0.5f);
		panelRect.sizeDelta = new Vector2(420f, 500f);
		panelRect.anchoredPosition = new Vector2(16f, 0f);
		((Transform)panelRect).localScale = Vector3.one;
	}

	private static void SuppressFikaPartyPanel(Transform parent)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		Transform val = parent.Find("FikaPartyPanel");
		if ((Object)val != (Object)null)
		{
			((Component)val).gameObject.SetActive(false);
		}
	}

	private void ApplyPartyRaidUiLayout(MatchMakerUI fikaUi)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Expected O, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		//IL_00c6: Expected O, but got Unknown
		try
		{
			if (!NekoGroupExConfig.UseNativePartyInvite || (Object)fikaUi == (Object)null)
			{
				return;
			}
			NekoPartyData? currentParty = NekoGroupState.CurrentParty;
			if (!currentParty.HasValue || currentParty.Value.Members == null || currentParty.Value.Members.Length < 2)
			{
				if ((Object)_hostRaidButton != (Object)null)
				{
					_hostRaidButton.SetActive(false);
				}
				return;
			}
			if (((Component)fikaUi).transform.childCount > 0)
			{
				Transform child = ((Component)fikaUi).transform.GetChild(0);
				if ((Object)child != (Object)null)
				{
					GameObject dediSelection = fikaUi.DediSelection;
					if ((Object)dediSelection != (Object)null && (Object)dediSelection.transform != (Object)child && dediSelection.transform.IsChildOf(child))
					{
						dediSelection.transform.SetParent(((Component)fikaUi).transform, true);
						dediSelection.transform.SetAsLastSibling();
					}
					CanvasGroup obj = ((Component)child).GetComponent<CanvasGroup>() ?? ((Component)child).gameObject.AddComponent<CanvasGroup>();
					obj.alpha = 0f;
					obj.interactable = false;
					obj.blocksRaycasts = false;
				}
			}
			if (_isPartyLeader)
			{
				EnsureHostRaidButton();
			}
			else if ((Object)_hostRaidButton != (Object)null)
			{
				_hostRaidButton.SetActive(false);
			}
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] 组队准备页战局UI改造失败：{arg}", "ApplyPartyRaidUiLayout");
		}
	}

	private void EnsureHostRaidButton()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		if ((Object)BackButton == (Object)null)
		{
			return;
		}
		if ((Object)_hostRaidButton == (Object)null)
		{
			_hostRaidButton = Object.Instantiate<GameObject>(((Component)BackButton).gameObject, ((Component)BackButton).transform.parent);
			((Object)_hostRaidButton).name = "NekoPartyHostRaidButton";
			DefaultUIButton component = _hostRaidButton.GetComponent<DefaultUIButton>();
			if ((Object)component != (Object)null)
			{
				component.SetRawText("主持战局", component.HeaderSize);
				((UnityEventBase)component.OnClick).RemoveAllListeners();
				component.OnClick.AddListener(new UnityAction(InvokeFikaHostRaid));
			}
			RectTransform val = (RectTransform)((Component)BackButton).transform;
			RectTransform val2 = (RectTransform)_hostRaidButton.transform;
			val2.anchorMin = val.anchorMin;
			val2.anchorMax = val.anchorMax;
			val2.pivot = val.pivot;
			val2.sizeDelta = val.sizeDelta;
			((Transform)val2).localScale = Vector3.one;
			Rect rect = val.rect;
			float num = Mathf.Max(((Rect)(ref rect)).height, 40f) + 18f;
			val2.anchoredPosition = val.anchoredPosition + new Vector2(0f, num);
		}
		_hostRaidButton.SetActive(true);
		_hostRaidButton.transform.SetAsLastSibling();
	}

	private void InvokeFikaHostRaid()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		try
		{
			MatchMakerUI componentInChildren = ((Component)this).GetComponentInChildren<MatchMakerUI>(true);
			if ((Object)componentInChildren != (Object)null && (Object)componentInChildren.RaidGroupHostButton != (Object)null)
			{
				((UnityEvent)componentInChildren.RaidGroupHostButton.onClick).Invoke();
			}
			else
			{
				FikaGlobals.LogWarning("[NekoParty] 主持战局：未找到 Fika RaidGroupHostButton，无法弹出主持弹窗。", "InvokeFikaHostRaid");
			}
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] 触发主持开局失败：{arg}", "InvokeFikaHostRaid");
		}
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

	private void Update()
	{
		_invitePollTimer += Time.deltaTime;
		if (_invitePollTimer >= 5f)
		{
			_invitePollTimer = 0f;
			_pollCycle++;
			if (_pollCycle % 12 == 0)
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] MatchMaker控制器心跳 tick=" + _pollCycle + " guard=" + ((!string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId)) ? "1" : "0") + "/" + ((NekoGroupState.PendingAcceptedRaidInvite != null) ? "1" : "0") + "/" + NekoGroupState.SuppressedRaidInviteIds.Count + " pending=" + NekoGroupState.PendingRaidInvites.Count + " active=" + ((Behaviour)this).isActiveAndEnabled, "RaidInviteDiag");
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
		RefreshConfirmRecoveryHint();
		_p2pRefreshTimer += Time.deltaTime;
		if (_p2pRefreshTimer >= 1f)
		{
			_p2pRefreshTimer = 0f;
			RefreshP2PStatusPanel();
		}
	}

	private void InitializePartyState()
	{
		try
		{
			NekoPartyOperationResponse currentParty = NekoGroupRequestHandler.GetCurrentParty();
			if (currentParty.Success)
			{
				NekoGroupState.CurrentParty = currentParty.Party;
			}
			if (!NekoGroupExConfig.UseNativePartyInvite)
			{
				PollPartyInviteFallback();
			}
			PollRaidInviteFallback();
			RefreshPartyPanelData();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty InitializePartyState failed: " + ex.Message, "InitializePartyState");
		}
	}

	public void RefreshPartyStateFromExternalChange()
	{
		EnsurePartyPanelAttached();
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
			RefreshPartyPanelData();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty PollPartyInviteFallback failed: " + ex.Message, "PollPartyInviteFallback");
		}
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
		if (RaidSettings == null)
		{
			return false;
		}
		NekoPartyMixedSideRuntime.PrepareInviteRaidSettings(RaidSettings, invite);
		_invitePopupOpen = true;
		bool handled = false;
		DialogWindowContext obj = ItemUiContext.Instance.ShowInviteInRaidWindow((IMatchmakerSession<RaidSettings>)(object)session, RaidSettings, value.InventoryController, CreateRaidInvitePlayer(invite));
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

	private void HandleRaidInviteAccept(NekoRaidInviteReceivedNotification invite)
	{
		if (!_inviteJoinInProgress)
		{
			_inviteJoinInProgress = true;
			NekoGroupState.RaidInviteInProgressId = invite.InviteId;
			NekoGroupState.SuppressedRaidInviteIds.Add(invite.InviteId);
			NekoGroupState.AutoJoinRaidInviteId = string.Empty;
			if (!NekoPartyMixedSideRuntime.OpenInviteSideSelection(invite))
			{
				NekoGroupState.PendingAcceptedRaidInvite = null;
				NekoPartyMixedSideRuntime.ClearPendingInviteSelection();
				NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(invite.InviteId);
				NekoPartyMixedSideRuntime.ClearFikaInviteInProgress(invite.InviteId);
				NekoGroupState.RaidInviteInProgressId = string.Empty;
				NekoGroupState.SuppressedRaidInviteIds.Remove(invite.InviteId);
				NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_InviteAcceptFailed", (string)null), (ENotificationDurationType)0);
			}
			else
			{
				NekoGroupState.PendingRaidInvites.Remove(invite.InviteId);
			}
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
			NekoGroupState.PendingRaidInvites.Remove(invite.InviteId);
			NekoGroupState.SuppressedRaidInviteIds.Remove(invite.InviteId);
			if (NekoGroupState.PendingAcceptedRaidInvite?.InviteId == invite.InviteId)
			{
				NekoGroupState.PendingAcceptedRaidInvite = null;
				NekoPartyMixedSideRuntime.ClearPendingInviteSelection();
			}
			if (Traverse.Create(typeof(FikaBackendUtils)).Property("InviteInProgressId", (object[])null).GetValue<string>() == invite.InviteId)
			{
				Traverse.Create(typeof(FikaBackendUtils)).Property("InviteInProgressId", (object[])null).SetValue((object)string.Empty);
			}
			NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(invite.InviteId);
			RefreshPartyPanelData();
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
			RefreshPartyPanelData();
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
			RefreshPartyPanelData();
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

	private void BuildPartyPanel(Transform parent)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Expected O, but got Unknown
		if (!((Object)_partyPanel != (Object)null))
		{
			_partyPanel = new GameObject("NekoPartyPanel");
			_partyPanel.transform.SetParent(parent, false);
			RectTransform val = _partyPanel.AddComponent<RectTransform>();
			ConfigurePanelRect(val);
			((Graphic)_partyPanel.AddComponent<Image>()).color = new Color(0.03f, 0.04f, 0.05f, 0.92f);
			Outline obj = _partyPanel.AddComponent<Outline>();
			((Shadow)obj).effectColor = new Color(0.6f, 0.62f, 0.66f, 0.45f);
			((Shadow)obj).effectDistance = new Vector2(1f, -1f);
			GameObject val2 = new GameObject("HeaderBar");
			val2.transform.SetParent((Transform)val, false);
			RectTransform val3 = val2.AddComponent<RectTransform>();
			val3.anchorMin = new Vector2(0f, 1f);
			val3.anchorMax = new Vector2(1f, 1f);
			val3.pivot = new Vector2(0.5f, 1f);
			val3.sizeDelta = new Vector2(0f, 42f);
			val3.anchoredPosition = Vector2.zero;
			((Graphic)val2.AddComponent<Image>()).color = new Color(0.16f, 0.18f, 0.2f, 0.92f);
			_partyHeaderText = CreateText("PartyHeader", val3, string.Format(LocalizationExtensions.Localized("F_MMUI_OnlinePlayers", (string)null), 0), 0f, 0f, 18);
			((TMP_Text)_partyHeaderText).rectTransform.anchorMin = new Vector2(0f, 0f);
			((TMP_Text)_partyHeaderText).rectTransform.anchorMax = new Vector2(1f, 1f);
			((TMP_Text)_partyHeaderText).rectTransform.offsetMin = new Vector2(14f, 0f);
			((TMP_Text)_partyHeaderText).rectTransform.offsetMax = new Vector2(-52f, 0f);
			((TMP_Text)_partyHeaderText).alignment = (TextAlignmentOptions)4097;
			_partyInfoText = CreateText("PartyInfo", val, string.Empty, 0f, 0f, 14);
			((Component)_partyInfoText).gameObject.SetActive(false);
			BuildPlayerList(val);
			_partyRefreshButton = CreateIconButton("PartyRefreshButton", val3, LocalizationExtensions.Localized("NKP_PartyRefreshShort", (string)null), 0f, 0f);
			((UnityEvent)_partyRefreshButton.onClick).AddListener((UnityAction)delegate
			{
				Singleton<GUISounds>.Instance.PlayUISound((EUISoundType)3);
				RefreshPartyPanelData();
			});
		}
	}

	private void BuildPlayerList(RectTransform parent)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Expected O, but got Unknown
		GameObject val = new GameObject("PlayerListViewport");
		val.transform.SetParent((Transform)parent, false);
		RectTransform val2 = val.AddComponent<RectTransform>();
		val2.anchorMin = new Vector2(0f, 0f);
		val2.anchorMax = new Vector2(1f, 1f);
		val2.pivot = new Vector2(0.5f, 0.5f);
		val2.offsetMin = new Vector2(4f, 6f);
		val2.offsetMax = new Vector2(-4f, -48f);
		((Graphic)val.AddComponent<Image>()).color = new Color(0.04f, 0.05f, 0.07f, 0.92f);
		val.AddComponent<RectMask2D>();
		GameObject val3 = new GameObject("PlayerListContent");
		val3.transform.SetParent(val.transform, false);
		_playerListContent = val3.AddComponent<RectTransform>();
		_playerListContent.anchorMin = new Vector2(0f, 1f);
		_playerListContent.anchorMax = new Vector2(1f, 1f);
		_playerListContent.pivot = new Vector2(0.5f, 1f);
		_playerListContent.sizeDelta = new Vector2(0f, 0f);
		_playerListContent.anchoredPosition = Vector2.zero;
		VerticalLayoutGroup obj = val3.AddComponent<VerticalLayoutGroup>();
		((HorizontalOrVerticalLayoutGroup)obj).spacing = 8f;
		((LayoutGroup)obj).padding = new RectOffset(8, 8, 8, 8);
		((LayoutGroup)obj).childAlignment = (TextAnchor)1;
		((HorizontalOrVerticalLayoutGroup)obj).childControlHeight = false;
		((HorizontalOrVerticalLayoutGroup)obj).childControlWidth = true;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandHeight = false;
		((HorizontalOrVerticalLayoutGroup)obj).childForceExpandWidth = true;
		val3.AddComponent<ContentSizeFitter>().verticalFit = (FitMode)2;
		ScrollRect obj2 = val.AddComponent<ScrollRect>();
		obj2.viewport = val2;
		obj2.content = _playerListContent;
		obj2.horizontal = false;
		obj2.vertical = true;
		obj2.movementType = (MovementType)2;
		obj2.scrollSensitivity = 24f;
	}

	private void EnsureConfirmRecoveryHint()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)_confirmRecoveryHintText != (Object)null))
		{
			Transform transform = ((Component)this).transform;
			RectTransform val = (RectTransform)((transform is RectTransform) ? transform : null);
			if ((Object)(object)val != (Object)null)
			{
				GameObject val2 = new GameObject("NekoRecoveryConfirmHint");
				val2.transform.SetParent((Transform)val, false);
				RectTransform obj = val2.AddComponent<RectTransform>();
				obj.anchorMin = new Vector2(0.5f, 1f);
				obj.anchorMax = new Vector2(0.5f, 1f);
				obj.pivot = new Vector2(0.5f, 1f);
				obj.sizeDelta = new Vector2(920f, 32f);
				obj.anchoredPosition = new Vector2(0f, -92f);
				_confirmRecoveryHintText = val2.AddComponent<TextMeshProUGUI>();
				((TMP_Text)_confirmRecoveryHintText).fontSize = 18f;
				((TMP_Text)_confirmRecoveryHintText).alignment = (TextAlignmentOptions)258;
				((Graphic)_confirmRecoveryHintText).color = new Color(0.88f, 0.62f, 0.24f, 0.96f);
				((TMP_Text)_confirmRecoveryHintText).enableWordWrapping = false;
				((Component)_confirmRecoveryHintText).gameObject.SetActive(false);
			}
		}
	}

	private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float x, float y, int fontSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name);
		val.transform.SetParent((Transform)parent, false);
		RectTransform obj = val.AddComponent<RectTransform>();
		obj.anchorMin = new Vector2(0.5f, 0.5f);
		obj.anchorMax = new Vector2(0.5f, 0.5f);
		obj.pivot = new Vector2(0.5f, 0.5f);
		obj.sizeDelta = new Vector2(400f, 132f);
		obj.anchoredPosition = new Vector2(x, y);
		TextMeshProUGUI obj2 = val.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj2).text = text;
		((TMP_Text)obj2).fontSize = fontSize;
		((Graphic)obj2).color = new Color(0.96f, 0.96f, 0.96f, 1f);
		((TMP_Text)obj2).alignment = (TextAlignmentOptions)514;
		((TMP_Text)obj2).enableWordWrapping = true;
		return obj2;
	}

	private TMP_Dropdown CreateDropdown(string name, RectTransform parent, float x, float y)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		GameObject obj = TMP_DefaultControls.CreateDropdown(default(Resources));
		((Object)obj).name = name;
		obj.transform.SetParent((Transform)parent, false);
		TMP_Dropdown component = obj.GetComponent<TMP_Dropdown>();
		RectTransform component2 = obj.GetComponent<RectTransform>();
		component2.sizeDelta = new Vector2(350f, 42f);
		component2.anchoredPosition = new Vector2(x, y);
		Image component3 = obj.GetComponent<Image>();
		if ((Object)component3 != (Object)null)
		{
			((Graphic)component3).color = new Color(0.17f, 0.19f, 0.22f, 0.95f);
		}
		component.ClearOptions();
		component.AddOptions(new List<OptionData>
		{
			new OptionData(LocalizationExtensions.Localized("NKP_CommonNone", (string)null))
		});
		StyleDropdown(component);
		return component;
	}

	private Button CreateButton(string name, RectTransform parent, string text, float x, float y)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		GameObject obj = TMP_DefaultControls.CreateButton(default(Resources));
		((Object)obj).name = name;
		obj.transform.SetParent((Transform)parent, false);
		Button component = obj.GetComponent<Button>();
		RectTransform component2 = obj.GetComponent<RectTransform>();
		component2.sizeDelta = new Vector2(290f, 44f);
		component2.anchoredPosition = new Vector2(x, y);
		Image component3 = obj.GetComponent<Image>();
		if ((Object)component3 != (Object)null)
		{
			((Graphic)component3).color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
		}
		TextMeshProUGUI componentInChildren = obj.GetComponentInChildren<TextMeshProUGUI>();
		if ((Object)componentInChildren != (Object)null)
		{
			((TMP_Text)componentInChildren).text = text;
			((TMP_Text)componentInChildren).fontSize = 16f;
			((Graphic)componentInChildren).color = new Color(0.96f, 0.96f, 0.96f, 1f);
		}
		return component;
	}

	private Button CreateIconButton(string name, RectTransform parent, string text, float x, float y)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name);
		val.transform.SetParent((Transform)parent, false);
		RectTransform obj = val.AddComponent<RectTransform>();
		obj.anchorMin = new Vector2(1f, 0.5f);
		obj.anchorMax = new Vector2(1f, 0.5f);
		obj.pivot = new Vector2(1f, 0.5f);
		obj.sizeDelta = new Vector2(36f, 28f);
		obj.anchoredPosition = new Vector2(-6f + x, y);
		((Graphic)val.AddComponent<Image>()).color = new Color(0.2f, 0.22f, 0.25f, 0.92f);
		Button result = val.AddComponent<Button>();
		TextMeshProUGUI obj2 = CreateRowText("IconLabel", val.transform, text, Vector2.zero, (TextAlignmentOptions)514);
		((TMP_Text)obj2).rectTransform.anchorMin = Vector2.zero;
		((TMP_Text)obj2).rectTransform.anchorMax = Vector2.one;
		((TMP_Text)obj2).fontSize = 16f;
		((Graphic)obj2).color = new Color(0.93f, 0.93f, 0.93f, 1f);
		return result;
	}

	private void StyleDropdown(TMP_Dropdown dropdown)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(((Object)(object)dropdown != (Object)null) ? dropdown.captionText : null) != (Object)null)
		{
			((Graphic)dropdown.captionText).color = new Color(0.96f, 0.96f, 0.96f, 1f);
			dropdown.captionText.fontSize = 15f;
			dropdown.captionText.alignment = (TextAlignmentOptions)4097;
		}
		if ((Object)(((Object)(object)dropdown != (Object)null) ? dropdown.itemText : null) != (Object)null)
		{
			((Graphic)dropdown.itemText).color = new Color(0.96f, 0.96f, 0.96f, 1f);
			dropdown.itemText.fontSize = 15f;
			dropdown.itemText.alignment = (TextAlignmentOptions)4097;
		}
		if ((Object)(((Object)(object)dropdown != (Object)null) ? dropdown.template : null) != (Object)null)
		{
			Image component = ((Component)dropdown.template).GetComponent<Image>();
			if ((Object)component != (Object)null)
			{
				((Graphic)component).color = new Color(0.11f, 0.13f, 0.16f, 0.98f);
			}
			Transform val = ((Transform)dropdown.template).Find("Viewport/Content/Item/Item Checkmark");
			if ((Object)val != (Object)null)
			{
				((Component)val).gameObject.SetActive(false);
			}
		}
	}

	private void OnInviteClicked()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((Object)_partyInviteDropdown == (Object)null || _partyInviteCandidates.Count == 0)
		{
			return;
		}
		string text = _partyInviteDropdown.options[_partyInviteDropdown.value].text;
		if (!_partyInviteCandidates.TryGetValue(text, out var value))
		{
			return;
		}
		if (HasPendingPartyInvite())
		{
			NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null), (ENotificationDurationType)0);
			return;
		}
		if (!EnsureLeaderParty())
		{
			NotificationManager.DisplayWarningNotification(LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null), (ENotificationDurationType)0);
			return;
		}
		Singleton<GUISounds>.Instance.PlayUISound((EUISoundType)3);
		NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.InvitePartyMember(value);
		if (!nekoPartyOperationResponse.Success)
		{
			NotificationManager.DisplayWarningNotification(string.IsNullOrEmpty(nekoPartyOperationResponse.Error) ? LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null) : nekoPartyOperationResponse.Error, (ENotificationDurationType)0);
		}
		else
		{
			NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
		}
		RefreshPartyPanelData();
	}

	private bool EnsureLeaderParty()
	{
		NekoPartyData? currentParty = NekoGroupState.CurrentParty;
		if (currentParty.HasValue && NekoPartyMixedSideRuntime.IsLocalProfileId(currentParty.Value.LeaderProfileId))
		{
			return true;
		}
		NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.CreateParty();
		if (!nekoPartyOperationResponse.Success || !nekoPartyOperationResponse.Party.HasValue)
		{
			return false;
		}
		NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
		return NekoPartyMixedSideRuntime.IsLocalProfileId(nekoPartyOperationResponse.Party.Value.LeaderProfileId);
	}

	private void OnRemoveClicked()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((Object)_partyMemberDropdown == (Object)null || _partyRemovableMembers.Count == 0)
		{
			return;
		}
		string text = _partyMemberDropdown.options[_partyMemberDropdown.value].text;
		if (_partyRemovableMembers.TryGetValue(text, out var value))
		{
			Singleton<GUISounds>.Instance.PlayUISound((EUISoundType)3);
			NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.RemovePartyMember(value);
			if (!nekoPartyOperationResponse.Success)
			{
				NotificationManager.DisplayWarningNotification(string.IsNullOrEmpty(nekoPartyOperationResponse.Error) ? LocalizationExtensions.Localized("NKP_PartyOperationFailed", (string)null) : nekoPartyOperationResponse.Error, (ENotificationDurationType)0);
			}
			else
			{
				NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
			}
			RefreshPartyPanelData();
		}
	}

	private void RefreshPartyPanelData()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((Object)_partyPanel == (Object)null)
		{
			return;
		}
		try
		{
			NekoPartyMixedSideRuntime.SyncCurrentPreferredSideBestEffort();
			NekoPartyOperationResponse currentParty = NekoGroupRequestHandler.GetCurrentParty();
			if (currentParty.Success)
			{
				NekoGroupState.CurrentParty = currentParty.Party;
			}
			NekoPartyData? currentParty2 = NekoGroupState.CurrentParty;
			HashSet<string> localProfileIds = NekoPartyMixedSideRuntime.GetLocalProfileIds();
			_isPartyLeader = currentParty2.HasValue && localProfileIds.Contains(currentParty2.Value.LeaderProfileId);
			if (currentParty2.HasValue && currentParty2.Value.Members != null)
			{
				_ = currentParty2.Value.Members.LongLength;
			}
			_availableReceiversByName.Clear();
			_partyInviteCandidates.Clear();
			foreach (KeyValuePair<string, string> item in NekoGroupRequestHandler.AvailableReceivers(_profileId))
			{
				_partyInviteCandidates[item.Key] = item.Value;
				_availableReceiversByName[item.Key] = item.Value;
			}
			_partyMemberIds.Clear();
			_partyRemovableMembers.Clear();
			if (currentParty2.HasValue && currentParty2.Value.Members != null)
			{
				NekoPartyMemberData[] members = currentParty2.Value.Members;
				for (int i = 0; i < members.Length; i++)
				{
					NekoPartyMemberData member = members[i];
					if (!string.IsNullOrEmpty(member.ProfileId))
					{
						_partyMemberIds.Add(member.ProfileId);
					}
					if (_isPartyLeader)
					{
						if (!member.IsLeader)
						{
							_partyRemovableMembers[NekoPartyMixedSideRuntime.FormatPartyMemberDisplayName(member)] = member.ProfileId;
						}
					}
					else if (localProfileIds.Contains(member.ProfileId))
					{
						_partyRemovableMembers[NekoPartyMixedSideRuntime.FormatPartyMemberDisplayName(member)] = member.ProfileId;
					}
				}
			}
			RebuildOnlinePlayerList();
			RefreshConfirmRecoveryHint();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty RefreshPartyPanelData failed: " + ex.Message, "RefreshPartyPanelData");
		}
	}

	private void RefreshConfirmRecoveryHint()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		EnsureConfirmRecoveryHint();
		if ((Object)_confirmRecoveryHintText == (Object)null)
		{
			return;
		}
		INekoRaidExStatus raidExStatus = NekoCoreRegistry.RaidExStatus;
		if (raidExStatus == null || !raidExStatus.GetPendingRewind().HasValue)
		{
			NekoCoreRegistry.RaidExStatus?.PollPendingRewind(showNotification: false);
		}
		NekoRaidExRewindHint? nekoRaidExRewindHint = NekoCoreRegistry.RaidExStatus?.GetPendingRewind();
		if (nekoRaidExRewindHint.HasValue)
		{
			string arg = LocalizationExtensions.Localized(nekoRaidExRewindHint.Value.LocationId, (string)null);
			string arg2 = FormatSeconds(nekoRaidExRewindHint.Value.RemainingRaidSeconds);
			bool flag = RaidSettings != null && (int)RaidSettings.Side == 0 && RaidSettings.SelectedLocation != null && string.Equals(RaidSettings.SelectedLocation.Id, nekoRaidExRewindHint.Value.LocationId, StringComparison.OrdinalIgnoreCase);
			((TMP_Text)_confirmRecoveryHintText).text = (flag ? string.Format(LocalizationExtensions.Localized("NKP_RewindConfirmReady", (string)null), arg, arg2) : string.Format(LocalizationExtensions.Localized("NKP_RewindConfirmWrongMap", (string)null), arg, arg2));
			((Component)_confirmRecoveryHintText).gameObject.SetActive(true);
			return;
		}
		INekoRaidExStatus raidExStatus2 = NekoCoreRegistry.RaidExStatus;
		if (raidExStatus2 == null || !raidExStatus2.GetPendingRecovery().HasValue)
		{
			NekoCoreRegistry.RaidExStatus?.PollPendingRecovery(showNotification: false);
		}
		NekoRaidExRecoveryHint? nekoRaidExRecoveryHint = NekoCoreRegistry.RaidExStatus?.GetPendingRecovery();
		if (!nekoRaidExRecoveryHint.HasValue)
		{
			((Component)_confirmRecoveryHintText).gameObject.SetActive(false);
			return;
		}
		string arg3 = LocalizationExtensions.Localized(nekoRaidExRecoveryHint.Value.LocationId, (string)null);
		bool flag2 = RaidSettings != null && RaidSettings.IsScav && RaidSettings.SelectedLocation != null && string.Equals(RaidSettings.SelectedLocation.Id, nekoRaidExRecoveryHint.Value.LocationId, StringComparison.OrdinalIgnoreCase);
		((TMP_Text)_confirmRecoveryHintText).text = (flag2 ? string.Format(LocalizationExtensions.Localized("NKP_RecoveryConfirmReady", (string)null), arg3) : string.Format(LocalizationExtensions.Localized("NKP_RecoveryConfirmWrongMap", (string)null), arg3));
		((Component)_confirmRecoveryHintText).gameObject.SetActive(true);
	}

	private static string FormatSeconds(int totalSeconds)
	{
		totalSeconds = Mathf.Max(0, totalSeconds);
		TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);
		if (!(timeSpan.TotalHours >= 1.0))
		{
			return timeSpan.ToString("mm\\:ss");
		}
		return timeSpan.ToString("hh\\:mm\\:ss");
	}

	private void RebuildOnlinePlayerList()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)_playerListContent == (Object)null)
		{
			return;
		}
		foreach (Object playerRow in _playerRows)
		{
			Object.Destroy(playerRow);
		}
		_playerRows.Clear();
		FikaPlayerPresence[] playerPresences;
		try
		{
			playerPresences = NekoGroupRequestHandler.GetPlayerPresences();
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("NekoParty GetPlayerPresences failed: " + ex.Message, "RebuildOnlinePlayerList");
			return;
		}
		foreach (FikaPlayerPresence item2 in playerPresences.OrderBy<FikaPlayerPresence, string>(delegate(FikaPlayerPresence x)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			return x.Nickname;
		}, StringComparer.OrdinalIgnoreCase))
		{
			GameObject item = CreatePlayerRow(item2);
			_playerRows.Add(item);
		}
		((TMP_Text)_partyHeaderText).text = string.Format(LocalizationExtensions.Localized("F_MMUI_OnlinePlayers", (string)null), _playerRows.Count);
	}

	private GameObject CreatePlayerRow(FikaPlayerPresence presence)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("PlayerRow_" + presence.Nickname);
		val.transform.SetParent((Transform)_playerListContent, false);
		val.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
		((Graphic)val.AddComponent<Image>()).color = new Color(0.17f, 0.19f, 0.22f, 0.95f);
		int partyMemberPreferredSide = NekoPartyMixedSideRuntime.GetPartyMemberPreferredSide(null, presence.Nickname);
		string text = (_partyMemberIds.Contains(_availableReceiversByName.TryGetValue(presence.Nickname, out var value) ? value : null) ? NekoPartyMixedSideRuntime.FormatDisplayName(presence.Nickname, partyMemberPreferredSide) : presence.Nickname);
		TextMeshProUGUI obj = CreateRowText("Nickname", val.transform, text, new Vector2(12f, 0f), (TextAlignmentOptions)4097);
		((TMP_Text)obj).rectTransform.anchorMin = new Vector2(0f, 0f);
		((TMP_Text)obj).rectTransform.anchorMax = new Vector2(0.58f, 1f);
		TextMeshProUGUI obj2 = CreateRowText("Status", val.transform, GetPresenceStatusText(presence), new Vector2(-12f, 0f), (TextAlignmentOptions)4100);
		((TMP_Text)obj2).fontSize = 13f;
		((Graphic)obj2).color = new Color(0.84f, 0.86f, 0.9f, 0.92f);
		((TMP_Text)obj2).rectTransform.anchorMin = new Vector2(0.58f, 0f);
		((TMP_Text)obj2).rectTransform.anchorMax = new Vector2(1f, 1f);
		((TMP_Text)obj2).rectTransform.offsetMax = new Vector2(-12f, 0f);
		SetupGroupActionButton(val, presence);
		return val;
	}

	private TextMeshProUGUI CreateRowText(string name, Transform parent, string text, Vector2 anchoredPosition, TextAlignmentOptions alignment)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name);
		val.transform.SetParent(parent, false);
		RectTransform obj = val.AddComponent<RectTransform>();
		obj.offsetMin = Vector2.zero;
		obj.offsetMax = Vector2.zero;
		obj.anchoredPosition = anchoredPosition;
		TextMeshProUGUI obj2 = val.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj2).text = text;
		((TMP_Text)obj2).fontSize = 14f;
		((Graphic)obj2).color = Color.white;
		((TMP_Text)obj2).alignment = alignment;
		((TMP_Text)obj2).enableWordWrapping = false;
		return obj2;
	}

	private string GetPresenceStatusText(FikaPlayerPresence presence)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected I4, but got Unknown
		return (presence.Activity - 1) switch
		{
			0 => LocalizationExtensions.Localized("NKP_PresenceInRaid", (string)null), 
			1 => LocalizationExtensions.Localized("NKP_PresenceInStash", (string)null), 
			2 => LocalizationExtensions.Localized("NKP_PresenceInHideout", (string)null), 
			3 => LocalizationExtensions.Localized("NKP_PresenceInFlea", (string)null), 
			_ => LocalizationExtensions.Localized("NKP_PresenceInMenu", (string)null), 
		};
	}

	private void SetupGroupActionButton(GameObject rowObject, FikaPlayerPresence presence)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Expected O, but got Unknown
		string profileId = FikaBackendUtils.Profile.ProfileId;
		string nickname = FikaBackendUtils.Profile.Nickname;
		bool flag = NekoGroupState.CurrentParty.HasValue && NekoGroupState.CurrentParty.Value.Members != null && NekoGroupState.CurrentParty.Value.Members.Length != 0;
		string targetProfileId = null;
		if (_availableReceiversByName.TryGetValue(presence.Nickname, out var value))
		{
			targetProfileId = value;
		}
		else if (presence.Nickname == nickname)
		{
			targetProfileId = profileId;
		}
		if (string.IsNullOrEmpty(targetProfileId))
		{
			return;
		}
		bool flag2 = targetProfileId == profileId;
		bool flag3 = _partyMemberIds.Contains(targetProfileId);
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
		TextMeshProUGUI val3 = CreateRowText("ActionLabel", val.transform, canInvite ? LocalizationExtensions.Localized("NKP_PartyInvite", (string)null) : ((!flag5) ? LocalizationExtensions.Localized("NKP_PartyRemove", (string)null) : (_isPartyLeader ? LocalizationExtensions.Localized("NKP_PartyDisband", (string)null) : LocalizationExtensions.Localized("NKP_PartyLeave", (string)null))), Vector2.zero, (TextAlignmentOptions)514);
		((TMP_Text)val3).fontSize = 13f;
		((TMP_Text)val3).rectTransform.anchorMin = Vector2.zero;
		((TMP_Text)val3).rectTransform.anchorMax = Vector2.one;
		NekoPartyPlayerRowUi.FitButtonToLabel(val2, val3);
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
						NekoGroupState.CurrentParty = nekoPartyOperationResponse.Party;
						_isPartyLeader = NekoPartyMixedSideRuntime.IsLocalProfileId(nekoPartyOperationResponse.Party.Value.LeaderProfileId);
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
						NekoGroupState.CurrentParty = nekoPartyOperationResponse2.Party;
						RefreshPartyPanelData();
					}
				}
			}
			catch (Exception ex)
			{
				FikaGlobals.LogWarning("NekoParty MatchMaker action failed: " + ex.Message, "SetupGroupActionButton");
			}
		});
	}
}
