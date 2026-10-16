using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using StashTools.History;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StashTools;

internal static class HistoryUi
{
	private static RectTransform? _root;

	private static RectTransform? _page;

	private static RectTransform? _content;

	private static RectTransform? _tab;

	private static ScrollRect? _detailScroll;

	private static string _shownDetail;

	private static Component? _screen;

	private static Component? _pendingScreen;

	private static float _retryAt;

	private static float _deadline;

	private static float _probeAt;

	private static string _attachReason;

	private static IntPtr _failedScreen;

	private static Tab? _overviewTab;

	private static TMP_Text? _font;

	private static string _profile;

	private static string _search;

	private static int _pageNumber;

	private static int _generation;

	private static bool _open;

	private static bool _favoriteOnly;

	private static HistoryReply? _reply;

	private static RaidRecord? _record;

	private static string _detail;

	private static float _nextIcon;

	private static readonly Queue<(HistoryItem Item, Image Image, int Generation)> IconQueue;

	private static Task<Sprite>? _iconTask;

	private static Image? _iconImage;

	private static Item? _iconSample;

	private static int _iconGeneration;

	private static float _iconDeadline;

	private static RectTransform? _layoutScreen;

	private static RectTransform? _layoutStats;

	private static RectTransform? _layoutToggle;

	private static RectTransform? _layoutHeader;

	private static RectTransform? _headerHost;

	private static RectTransform? _layoutCanvas;

	private static float _layoutAt;

	private static readonly List<RectTransform> HeaderBlockers;

	private static Vector3[]? _corners;

	private static Tab? _nativeHeader;

	private static readonly Dictionary<IntPtr, GameObject> HeaderVisuals;

	private static Image? _headerTarget;

	private static bool _headerHovered;

	private static bool _nativeWasChanged;

	private static bool _nativeWasSelected;

	private static int _headerInputLogs;

	private static Image? _statisticsStyle;

	private static bool _lootList;

	private static readonly Color Ink;

	private static readonly Color Muted;

	private static readonly Color Good;

	private static readonly Color Bad;

	private static readonly Color Surface;

	private static readonly Color Edge;

	private static readonly Color Accent;

	private static float Width
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			RectTransform? root = _root;
			float val;
			if (root == null)
			{
				val = 900f;
			}
			else
			{
				Rect rect = root.rect;
				val = ((Rect)(ref rect)).width;
			}
			return Math.Max(320f, val);
		}
	}

	internal static void SettingsChanged()
	{
		if (!_open || _record == null)
		{
			return;
		}
		StashBehaviour.Post(delegate
		{
			if (_open && _record != null)
			{
				Detail();
			}
		});
	}

	internal static void Own(OverallScreen screen)
	{
		Schedule((Component)(object)screen);
	}

	internal static void Other(OtherPlayerProfileScreen screen)
	{
		Schedule((Component)(object)screen);
	}

	private static void Schedule(Component screen)
	{
		if (HistoryCapture.Enabled)
		{
			IntPtr? intPtr = _screen?.Identity();
			IntPtr intPtr2 = screen.Identity();
			if ((!intPtr.HasValue || !(intPtr.GetValueOrDefault() == intPtr2) || !((Object)(object)_tab != (Object)null)) && !(_pendingScreen?.Identity() == screen.Identity()))
			{
				_pendingScreen = screen;
				_deadline = Time.unscaledTime + 8f;
				_retryAt = Time.unscaledTime + 0.1f;
				_attachReason = "页面尚未激活";
				_failedScreen = IntPtr.Zero;
				Plugin.Logger.LogInfo((object)("对局记录等待资料页初始化：" + ((object)screen).GetType().Name));
			}
		}
	}

	private static void BuildOwn(OverallScreen screen)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		Component back = (Component)(object)screen._overallToggle;
		InventoryScreen componentInParent = ((Component)screen).GetComponentInParent<InventoryScreen>();
		if (componentInParent?._tabDictionary != null)
		{
			foreach (KeyValuePair<EInventoryTab, Tab> item in componentInParent._tabDictionary)
			{
				if (((object)item.Key/*cast due to constrained. prefix*/).ToString() == "Overall")
				{
					back = (Component)(object)item.Value;
					break;
				}
			}
		}
		Attach((Component)(object)screen, UiValueAccess.Pmc(screen)?.Id ?? "", (Component)(object)screen.OverallStatsPanel, back);
	}

	private static void BuildOther(OtherPlayerProfileScreen screen)
	{
		OtherPlayerProfile val = UiValueAccess.Other(screen);
		string text = ((val != null) ? val.ProfileId : null) ?? "";
		if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(val?.AccountId))
		{
			text = "aid:" + val.AccountId;
		}
		Attach((Component)(object)screen, text, (Component)(object)screen._overallStatsPanel, (Component)(object)screen._eftToggle);
	}

	internal static void Close(Component? screen = null)
	{
		if ((Object)(object)screen != (Object)null && _pendingScreen?.Identity() == screen.Identity())
		{
			_pendingScreen = null;
		}
		if (!((Object)(object)screen != (Object)null) || !(_screen?.Identity() != screen.Identity()))
		{
			_generation++;
			IconQueue.Clear();
			_open = false;
			_record = null;
			HeaderBlockers.Clear();
			if ((Object)(object)_root != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)_root).gameObject);
			}
			if ((Object)(object)_tab != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)_tab).gameObject);
			}
			RestoreNativeHeader();
			_root = null;
			_tab = null;
			_page = null;
			_content = null;
			_screen = null;
			_font = null;
			_overviewTab = null;
			HeaderVisuals.Clear();
			_nativeHeader = null;
			_headerTarget = null;
			_headerInputLogs = 0;
			_headerHovered = false;
			_nativeWasChanged = false;
			_nativeWasSelected = false;
			_statisticsStyle = null;
			_layoutScreen = null;
			_layoutStats = null;
			_layoutToggle = null;
			_layoutHeader = null;
			_headerHost = null;
			_layoutCanvas = null;
			_layoutAt = 0f;
		}
	}

	private static void Attach(Component screen, string profile, Component stats, Component back)
	{
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0304: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		if (!HistoryCapture.Enabled)
		{
			return;
		}
		if (string.IsNullOrEmpty(profile))
		{
			profile = "unresolved";
		}
		TMP_Text font = ((stats != null) ? stats.GetComponentInChildren<TMP_Text>(true) : null) ?? screen.GetComponentInChildren<TMP_Text>(true);
		Close();
		_screen = screen;
		_profile = profile;
		_pageNumber = 1;
		_search = "";
		_favoriteOnly = false;
		_overviewTab = back?.TryCast<Tab>();
		_font = font;
		RectTransform component = screen.GetComponent<RectTransform>();
		Canvas componentInParent = screen.GetComponentInParent<Canvas>();
		object obj;
		if (componentInParent == null)
		{
			obj = null;
		}
		else
		{
			Canvas rootCanvas = componentInParent.rootCanvas;
			obj = ((rootCanvas != null) ? ((Component)rootCanvas).GetComponent<RectTransform>() : null);
		}
		if (obj == null)
		{
			obj = component;
		}
		_layoutCanvas = (RectTransform?)obj;
		_headerHost = component;
		_layoutScreen = component;
		_layoutStats = ((stats != null) ? stats.GetComponent<RectTransform>() : null);
		Component val = (Component)(object)screen.TryCast<OtherPlayerProfileScreen>()?._eftToggle;
		if ((Object)(object)val == (Object)null)
		{
			val = (Component)(object)screen.TryCast<OverallScreen>()?._overallToggle;
		}
		_layoutToggle = ((val != null) ? val.GetComponent<RectTransform>() : null);
		_root = Rect("StashToolsHistory", (Transform)(object)component);
		((Component)_root).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		Image obj2 = ((Component)_root).gameObject.AddComponent<Image>();
		((Graphic)obj2).color = Surface;
		((Graphic)obj2).raycastTarget = true;
		((Component)_root).gameObject.AddComponent<RectMask2D>();
		((Component)_root).gameObject.SetActive(false);
		RectTransform val2 = NativeHeaderRect(_overviewTab) ?? FindNativeHeader(screen);
		_nativeHeader = ((val2 != null) ? ((Component)val2).GetComponent<Tab>() : null) ?? ((val2 != null) ? ((Component)val2).GetComponentInParent<Tab>() : null);
		_layoutHeader = val2;
		if ((Object)(object)val2 == (Object)null)
		{
			throw new InvalidOperationException("未找到可见的原生总览页签，稍后重试");
		}
		_tab = Rect("StashToolsHistoryTab", (Transform)(object)_headerHost);
		((Component)_tab).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		Tab[] componentsInChildren = ((Component)_layoutCanvas).GetComponentsInChildren<Tab>(true);
		foreach (Tab val3 in componentsInChildren)
		{
			if (((Component)val3).gameObject.activeInHierarchy)
			{
				RectTransform val4 = NativeHeaderRect(val3);
				if ((Object)(object)val4 != (Object)null)
				{
					HeaderBlockers.Add(val4);
				}
			}
		}
		Selectable[] componentsInChildren2 = ((Component)_layoutCanvas).GetComponentsInChildren<Selectable>(true);
		foreach (Selectable val5 in componentsInChildren2)
		{
			if (((Component)val5).gameObject.activeInHierarchy)
			{
				RectTransform component2 = ((Component)val5).GetComponent<RectTransform>();
				if (HeaderSized(component2))
				{
					HeaderBlockers.Add(component2);
				}
			}
		}
		OtherPlayerProfileScreen? obj3 = screen.TryCast<OtherPlayerProfileScreen>();
		object obj4;
		if (obj3 == null)
		{
			obj4 = null;
		}
		else
		{
			DefaultUIButton backButton = obj3._backButton;
			obj4 = ((backButton != null) ? ((Component)backButton).GetComponent<RectTransform>() : null);
		}
		RectTransform val6 = (RectTransform)obj4;
		if ((Object)(object)val6 != (Object)null)
		{
			HeaderBlockers.Add(val6);
		}
		HeaderBlockers.Add(val2);
		SyncLayout();
		CreateHeader(componentInParent);
		if ((Object)(object)stats != (Object)null)
		{
			Image[] componentsInChildren3 = stats.GetComponentsInChildren<Image>(true);
			foreach (Image val7 in componentsInChildren3)
			{
				if (!((Object)(object)val7.sprite != (Object)null))
				{
					continue;
				}
				Rect rect = ((Graphic)val7).rectTransform.rect;
				if (((Rect)(ref rect)).width > 400f)
				{
					rect = ((Graphic)val7).rectTransform.rect;
					if (((Rect)(ref rect)).height > 70f)
					{
						_statisticsStyle = val7;
						break;
					}
				}
			}
		}
		Plugin.Logger.LogInfo((object)("对局记录入口输入层级：" + ((Object)screen).name + "；原生视觉节点=" + HeaderVisuals.Count + "；独立GraphicRaycaster已创建"));
		UpdateHeader();
		Plugin.Logger.LogInfo((object)("对局记录入口已附加：" + ((object)screen).GetType().Name));
	}

	private static void EnsureEntry()
	{
		if (!HistoryCapture.Enabled)
		{
			_pendingScreen = null;
			return;
		}
		float unscaledTime = Time.unscaledTime;
		if ((Object)(object)_pendingScreen == (Object)null && (Object)(object)_tab == (Object)null && unscaledTime >= _probeAt)
		{
			_probeAt = unscaledTime + 0.5f;
			OtherPlayerProfileScreen val = MonoBehaviourSingleton<MenuUI>.Instance?.OtherPlayerProfileScreen;
			OverallScreen val2 = MonoBehaviourSingleton<CommonUI>.Instance?.InventoryScreen?._overallScreen;
			Component val3 = (Component)(((Object)(object)val != (Object)null && ((Component)val).gameObject.activeInHierarchy) ? ((object)val) : ((object)(((Object)(object)val2 != (Object)null && ((Component)val2).gameObject.activeInHierarchy) ? val2 : null)));
			if ((Object)(object)val3 == (Object)null)
			{
				_failedScreen = IntPtr.Zero;
			}
			else if (val3.Identity() != _failedScreen)
			{
				Schedule(val3);
			}
		}
		if ((Object)(object)_pendingScreen == (Object)null || unscaledTime < _retryAt)
		{
			return;
		}
		_retryAt = unscaledTime + 0.25f;
		try
		{
			if (_pendingScreen.gameObject.activeInHierarchy)
			{
				OtherPlayerProfileScreen val4 = _pendingScreen.TryCast<OtherPlayerProfileScreen>();
				if ((Object)(object)val4 != (Object)null)
				{
					BuildOther(val4);
				}
				else
				{
					OverallScreen val5 = _pendingScreen.TryCast<OverallScreen>();
					if ((Object)(object)val5 != (Object)null)
					{
						BuildOwn(val5);
					}
				}
				if ((Object)(object)_tab != (Object)null)
				{
					_pendingScreen = null;
					return;
				}
			}
		}
		catch (Exception ex)
		{
			_attachReason = ex.ToString();
			Close();
		}
		if (unscaledTime >= _deadline && (Object)(object)_pendingScreen != (Object)null)
		{
			_failedScreen = _pendingScreen.Identity();
			_pendingScreen = null;
			Plugin.Logger.LogWarning((object)("对局记录入口创建超时：" + _attachReason));
		}
	}

	private static RectTransform? FindNativeHeader(Component screen)
	{
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		Transform val = screen.transform;
		int num = 0;
		while ((Object)(object)val != (Object)null && num < 3)
		{
			TMP_Text[] componentsInChildren = ((Component)val).GetComponentsInChildren<TMP_Text>(true);
			foreach (TMP_Text val2 in componentsInChildren)
			{
				string text = val2.text?.Trim() ?? "";
				if (text != "总览" && !text.Equals("Overall", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				val2.rectTransform.GetWorldCorners(array);
				if (array[2].y < (float)Screen.height * 0.92f || array[0].x > (float)Screen.width * 0.2f)
				{
					continue;
				}
				RectTransform val3 = NativeHeaderRect(((Component)val2).GetComponentInParent<Tab>());
				if ((Object)(object)val3 != (Object)null)
				{
					return val3;
				}
				RectTransform val4 = null;
				for (Transform parent = val2.transform.parent; (Object)(object)parent != (Object)null && parent.Identity() != val.Identity(); parent = parent.parent)
				{
					RectTransform val5 = parent.TryCast<RectTransform>();
					if ((Object)(object)val5 == (Object)null)
					{
						continue;
					}
					Rect rect = val5.rect;
					if (((Rect)(ref rect)).width >= 80f)
					{
						rect = val5.rect;
						if (((Rect)(ref rect)).width <= 320f)
						{
							rect = val5.rect;
							if (((Rect)(ref rect)).height >= 24f)
							{
								rect = val5.rect;
								if (((Rect)(ref rect)).height <= 70f)
								{
									val4 = val5;
									continue;
								}
							}
						}
					}
					if ((Object)(object)val4 != (Object)null)
					{
						break;
					}
				}
				if ((Object)(object)val4 != (Object)null)
				{
					return val4;
				}
			}
			num++;
			val = val.parent;
		}
		return null;
	}

	private static bool HeaderSized(RectTransform? rect)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)rect != (Object)null)
		{
			Rect rect2 = rect.rect;
			if (((Rect)(ref rect2)).width >= 60f)
			{
				rect2 = rect.rect;
				if (((Rect)(ref rect2)).width <= 420f)
				{
					rect2 = rect.rect;
					if (((Rect)(ref rect2)).height >= 20f)
					{
						rect2 = rect.rect;
						return ((Rect)(ref rect2)).height <= 80f;
					}
				}
			}
		}
		return false;
	}

	private static RectTransform? NativeHeaderRect(Tab? tab)
	{
		if ((Object)(object)tab == (Object)null)
		{
			return null;
		}
		RectTransform component = ((Component)tab).GetComponent<RectTransform>();
		if (HeaderSized(component))
		{
			return component;
		}
		Image targetImage = tab._targetImage;
		component = ((targetImage != null) ? ((Graphic)targetImage).rectTransform : null);
		if (HeaderSized(component))
		{
			return component;
		}
		Image[] componentsInChildren = ((Component)tab).GetComponentsInChildren<Image>(true);
		foreach (Image val in componentsInChildren)
		{
			if (HeaderSized(((Graphic)val).rectTransform) && (Object)(object)val.sprite != (Object)null)
			{
				return ((Graphic)val).rectTransform;
			}
		}
		return null;
	}

	private static HistoryBox Bounds(RectTransform source, RectTransform host)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		if (_corners == null)
		{
			_corners = (Vector3[]?)(object)new Vector3[4];
		}
		source.GetWorldCorners(_corners);
		float num = float.PositiveInfinity;
		float num2 = float.PositiveInfinity;
		float num3 = float.NegativeInfinity;
		float num4 = float.NegativeInfinity;
		for (int i = 0; i < 4; i++)
		{
			Vector3 val = ((Transform)host).InverseTransformPoint(_corners[i]);
			num = Math.Min(num, val.x);
			num2 = Math.Min(num2, val.y);
			num3 = Math.Max(num3, val.x);
			num4 = Math.Max(num4, val.y);
		}
		float num5 = num;
		Rect rect = host.rect;
		float x = num5 - ((Rect)(ref rect)).xMin;
		rect = host.rect;
		return new HistoryBox(x, ((Rect)(ref rect)).yMax - num4, num3 - num, num4 - num2);
	}

	private static void SyncLayout()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root == (Object)null || (Object)(object)_tab == (Object)null || (Object)(object)_layoutScreen == (Object)null || (Object)(object)_layoutStats == (Object)null || (Object)(object)_layoutHeader == (Object)null || (Object)(object)_headerHost == (Object)null)
		{
			return;
		}
		RectTransform layoutScreen = _layoutScreen;
		Rect rect = layoutScreen.rect;
		float width = ((Rect)(ref rect)).width;
		rect = layoutScreen.rect;
		HistoryBox historyBox = HistoryLayout.A(new HistoryBox(0f, 0f, width, ((Rect)(ref rect)).height), Bounds(_layoutCanvas ?? _headerHost, layoutScreen));
		HistoryBox historyBox2 = Bounds(_layoutStats, layoutScreen);
		HistoryBox historyBox3 = Bounds(_layoutHeader, _headerHost);
		rect = _headerHost.rect;
		float width2 = ((Rect)(ref rect)).width;
		rect = _headerHost.rect;
		HistoryBox historyBox4 = HistoryLayout.A(new HistoryBox(0f, 0f, width2, ((Rect)(ref rect)).height), Bounds(_layoutCanvas ?? _headerHost, _headerHost));
		HistoryBox[] array = (from r in HeaderBlockers
			where (Object)(object)r != (Object)null && ((Component)r).gameObject.activeInHierarchy
			select Bounds(r, _headerHost)).ToArray();
		HistoryBox? historyBox5 = HistoryLayout.A(historyBox4, historyBox3, array);
		if (!historyBox5.HasValue)
		{
			throw new InvalidOperationException("原生页签行没有足够空位，未移动或覆盖原生按钮");
		}
		float val = historyBox.Y + historyBox.Height * 0.04f;
		if ((Object)(object)_layoutToggle != (Object)null)
		{
			HistoryBox historyBox6 = Bounds(_layoutToggle, layoutScreen);
			if (historyBox6.Valid && historyBox6.Right > historyBox2.X && historyBox6.X < historyBox2.Right && historyBox6.Y >= historyBox.Y)
			{
				val = Math.Min(val, historyBox6.Y);
			}
		}
		val = Math.Max(val, Bounds(_layoutHeader, layoutScreen).Bottom + 8f);
		HistoryBox historyBox7 = HistoryLayout.A(historyBox, historyBox2, val);
		if (historyBox7.Width < 320f || historyBox7.Height < 340f)
		{
			throw new InvalidOperationException("资料页布局尚未准备好，稍后重试");
		}
		rect = _root.rect;
		float width3 = ((Rect)(ref rect)).width;
		int num;
		if (!(Math.Abs(width3 - historyBox7.Width) > 1f))
		{
			rect = _root.rect;
			if (!(Math.Abs(((Rect)(ref rect)).height - historyBox7.Height) > 1f) && !(Math.Abs(_root.anchoredPosition.x - historyBox7.X) > 1f))
			{
				num = ((Math.Abs(_root.anchoredPosition.y + historyBox7.Y) > 1f) ? 1 : 0);
				goto IL_02e6;
			}
		}
		num = 1;
		goto IL_02e6;
		IL_02e6:
		Top(_root, historyBox7.X, historyBox7.Y, historyBox7.Width, historyBox7.Height);
		HistoryBox value = historyBox5.Value;
		Top(_tab, value.X, value.Y, value.Width, value.Height);
		if (num == 0)
		{
			return;
		}
		Plugin.Logger.LogInfo((object)$"对局记录布局：页面 ({historyBox7.X:F0},{historyBox7.Y:F0}) {historyBox7.Width:F0}x{historyBox7.Height:F0}；入口 ({value.X:F0},{value.Y:F0}) {value.Width:F0}x{value.Height:F0}；可见区域 {historyBox.Width:F0}x{historyBox.Height:F0}，未改动原生控件位置");
		if (_open && (Object)(object)_page != (Object)null && Math.Abs(width3 - historyBox7.Width) > 1f)
		{
			if (_record != null)
			{
				Detail();
			}
			else
			{
				Fetch();
			}
		}
	}

	private static Image? HeaderImage(RectTransform? rect)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)rect == (Object)null)
		{
			return null;
		}
		Image result = null;
		float num = 0f;
		Image[] componentsInChildren = ((Component)rect).GetComponentsInChildren<Image>(true);
		foreach (Image val in componentsInChildren)
		{
			Rect rect2 = ((Graphic)val).rectTransform.rect;
			float width = ((Rect)(ref rect2)).width;
			rect2 = ((Graphic)val).rectTransform.rect;
			float num2 = width * ((Rect)(ref rect2)).height;
			if (!((Object)(object)val.sprite == (Object)null))
			{
				rect2 = ((Graphic)val).rectTransform.rect;
				float width2 = ((Rect)(ref rect2)).width;
				rect2 = rect.rect;
				if (!(width2 < ((Rect)(ref rect2)).width * 0.55f) && !(num2 <= num))
				{
					result = val;
					num = num2;
				}
			}
		}
		return result;
	}

	private static void CreateHeader(Canvas? profileCanvas)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		RectTransform tab = _tab;
		Canvas val = ((Component)tab).gameObject.AddComponent<Canvas>();
		val.overrideSorting = true;
		if ((Object)(object)profileCanvas != (Object)null)
		{
			val.sortingLayerID = profileCanvas.sortingLayerID;
			val.sortingOrder = profileCanvas.sortingOrder + 1;
			val.additionalShaderChannels = profileCanvas.additionalShaderChannels;
		}
		((Component)tab).gameObject.AddComponent<GraphicRaycaster>().blockingObjects = (BlockingObjects)0;
		Image val2 = ((Component)tab).gameObject.AddComponent<Image>();
		((Graphic)val2).color = Color.clear;
		((Graphic)val2).raycastTarget = true;
		Button obj = ((Component)tab).gameObject.AddComponent<Button>();
		((Selectable)obj).targetGraphic = (Graphic)(object)val2;
		((Selectable)obj).interactable = true;
		((Selectable)obj).transition = (Transition)0;
		((UnityEvent)obj.onClick).AddListener(new UnityAction(ToggleHistory));
		Tab? nativeHeader = _nativeHeader;
		RectTransform source = ((nativeHeader != null) ? ((Component)nativeHeader).GetComponent<RectTransform>() : null) ?? _layoutHeader;
		RectTransform val3 = Rect("NativeTabVisual", (Transform)(object)tab);
		val3.anchorMin = Vector2.zero;
		val3.anchorMax = Vector2.one;
		Vector2 offsetMin = (val3.offsetMax = Vector2.zero);
		val3.offsetMin = offsetMin;
		CopyHeaderVisual(source, val3, 0, root: true);
		EventTrigger events = ((Component)tab).gameObject.AddComponent<EventTrigger>();
		events.triggers = new List<Entry>();
		Hover((EventTriggerType)0, hovered: true);
		Hover((EventTriggerType)1, hovered: false);
		void Hover(EventTriggerType id, bool hovered)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected O, but got Unknown
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			Entry val4 = new Entry();
			val4.eventID = id;
			val4.callback = new TriggerEvent();
			((UnityEvent<BaseEventData>)(object)val4.callback).AddListener((UnityAction<BaseEventData>)delegate
			{
				_headerHovered = hovered;
				UpdateHeader();
			});
			events.triggers.Add(val4);
		}
	}

	private static void CopyHeaderVisual(RectTransform source, RectTransform target, int depth, bool root = false)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		if (depth > 14)
		{
			return;
		}
		HeaderVisuals[((Component)source).gameObject.Identity()] = ((Component)target).gameObject;
		if (!root)
		{
			target.anchorMin = source.anchorMin;
			target.anchorMax = source.anchorMax;
			target.pivot = source.pivot;
			target.sizeDelta = source.sizeDelta;
			target.anchoredPosition = source.anchoredPosition;
			((Transform)target).localScale = ((Transform)source).localScale;
			((Transform)target).localRotation = ((Transform)source).localRotation;
			((Component)target).gameObject.SetActive(((Component)source).gameObject.activeSelf);
		}
		Image component = ((Component)source).GetComponent<Image>();
		if ((Object)(object)component != (Object)null)
		{
			Image val = ((Component)target).gameObject.AddComponent<Image>();
			val.sprite = component.sprite;
			val.overrideSprite = component.overrideSprite;
			val.type = component.type;
			((Graphic)val).color = ((Graphic)component).color;
			((Graphic)val).material = ((Graphic)component).material;
			val.preserveAspect = component.preserveAspect;
			val.fillCenter = component.fillCenter;
			val.fillMethod = component.fillMethod;
			val.fillAmount = component.fillAmount;
			val.fillClockwise = component.fillClockwise;
			val.fillOrigin = component.fillOrigin;
			val.pixelsPerUnitMultiplier = component.pixelsPerUnitMultiplier;
			((Graphic)val).raycastTarget = false;
			IntPtr value = component.Identity();
			IntPtr? obj = _nativeHeader?._targetImage?.Identity();
			if (value == obj)
			{
				_headerTarget = val;
			}
		}
		RawImage component2 = ((Component)source).GetComponent<RawImage>();
		if ((Object)(object)component2 != (Object)null)
		{
			RawImage obj2 = ((Component)target).gameObject.AddComponent<RawImage>();
			obj2.texture = component2.texture;
			obj2.uvRect = component2.uvRect;
			((Graphic)obj2).color = ((Graphic)component2).color;
			((Graphic)obj2).material = ((Graphic)component2).material;
			((Graphic)obj2).raycastTarget = false;
		}
		TMP_Text component3 = ((Component)source).GetComponent<TMP_Text>();
		if ((Object)(object)component3 != (Object)null)
		{
			TextMeshProUGUI obj3 = ((Component)target).gameObject.AddComponent<TextMeshProUGUI>();
			((TMP_Text)obj3).font = component3.font;
			((TMP_Text)obj3).fontSharedMaterial = component3.fontSharedMaterial;
			((TMP_Text)obj3).spriteAsset = component3.spriteAsset;
			((TMP_Text)obj3).fontSize = component3.fontSize;
			((TMP_Text)obj3).fontStyle = component3.fontStyle;
			((Graphic)obj3).color = ((Graphic)component3).color;
			((TMP_Text)obj3).alignment = component3.alignment;
			((TMP_Text)obj3).richText = component3.richText;
			((TMP_Text)obj3).enableWordWrapping = component3.enableWordWrapping;
			((TMP_Text)obj3).overflowMode = component3.overflowMode;
			((TMP_Text)obj3).margin = component3.margin;
			((TMP_Text)obj3).characterSpacing = component3.characterSpacing;
			((Graphic)obj3).raycastTarget = false;
			((TMP_Text)obj3).text = (component3.text ?? "").Replace("总览", "对局记录").Replace("OVERALL", "对局记录").Replace("Overall", "对局记录");
		}
		Mask component4 = ((Component)source).GetComponent<Mask>();
		if ((Object)(object)component4 != (Object)null)
		{
			Mask obj4 = ((Component)target).gameObject.AddComponent<Mask>();
			obj4.showMaskGraphic = component4.showMaskGraphic;
			((Behaviour)obj4).enabled = ((Behaviour)component4).enabled;
		}
		RectMask2D component5 = ((Component)source).GetComponent<RectMask2D>();
		if ((Object)(object)component5 != (Object)null)
		{
			RectMask2D obj5 = ((Component)target).gameObject.AddComponent<RectMask2D>();
			obj5.padding = component5.padding;
			obj5.softness = component5.softness;
			((Behaviour)obj5).enabled = ((Behaviour)component5).enabled;
		}
		CanvasGroup component6 = ((Component)source).GetComponent<CanvasGroup>();
		if ((Object)(object)component6 != (Object)null)
		{
			CanvasGroup obj6 = ((Component)target).gameObject.AddComponent<CanvasGroup>();
			obj6.alpha = component6.alpha;
			obj6.blocksRaycasts = false;
		}
		for (int i = 0; i < ((Transform)source).childCount; i++)
		{
			RectTransform val2 = ((Transform)source).GetChild(i).TryCast<RectTransform>();
			if (!((Object)(object)val2 == (Object)null))
			{
				CopyHeaderVisual(val2, Rect(((Object)val2).name, (Transform)(object)target), depth + 1);
			}
		}
	}

	private static void ProbeHeaderClick()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_tab == (Object)null || !Input.GetMouseButtonDown(0) || _headerInputLogs >= 5)
		{
			return;
		}
		GraphicRaycaster component = ((Component)_tab).GetComponent<GraphicRaycaster>();
		if (!RectTransformUtility.RectangleContainsScreenPoint(_tab, Vector2.op_Implicit(Input.mousePosition), (component != null) ? ((BaseRaycaster)component).eventCamera : null))
		{
			return;
		}
		_headerInputLogs++;
		EventSystem current = EventSystem.current;
		if ((Object)(object)current == (Object)null)
		{
			Plugin.Logger.LogWarning((object)"对局记录点击诊断：EventSystem不存在");
			return;
		}
		PointerEventData val = new PointerEventData(current);
		val.position = Vector2.op_Implicit(Input.mousePosition);
		List<RaycastResult> list = new List<RaycastResult>();
		current.RaycastAll(val, list);
		object obj;
		RaycastResult val2;
		if (list.Count <= 0)
		{
			obj = "无命中";
		}
		else
		{
			val2 = list[0];
			obj = ((Object)((RaycastResult)(ref val2)).gameObject).name;
		}
		string text = (string)obj;
		bool flag = false;
		for (int i = 0; i < list.Count; i++)
		{
			val2 = list[i];
			if (((RaycastResult)(ref val2)).gameObject.transform.IsChildOf((Transform)(object)_tab))
			{
				flag = true;
			}
		}
		Plugin.Logger.LogInfo((object)("对局记录点击诊断：首个命中=" + text + "，入口命中=" + flag + "，可交互=" + ((Selectable)((Component)_tab).GetComponent<Button>()).IsInteractable()));
	}

	private static void ToggleHistory()
	{
		try
		{
			if (!((Object)(object)_root == (Object)null) && !((Object)(object)_screen == (Object)null))
			{
				_open = !_open;
				((Component)_root).gameObject.SetActive(_open);
				UpdateHeader();
				Plugin.Logger.LogInfo((object)("对局记录入口点击已收到：" + (_open ? "打开" : "关闭")));
				if (_open)
				{
					((Transform)_root).SetAsLastSibling();
					Canvas.ForceUpdateCanvases();
					SyncLayout();
					Fetch();
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError((object)("对局记录入口打开失败：" + ex));
			_open = false;
			RectTransform? root = _root;
			if (root != null)
			{
				((Component)root).gameObject.SetActive(false);
			}
			RestoreNativeHeader();
		}
	}

	private static void RestoreNativeHeader()
	{
		if (!_nativeWasChanged)
		{
			return;
		}
		try
		{
			if ((Object)(object)_nativeHeader != (Object)null)
			{
				_nativeHeader.UpdateVisual(_nativeWasSelected || UiValueAccess.Selected(_nativeHeader), true);
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogDebug((object)("对局记录原生页签已关闭：" + ex.Message));
		}
		_nativeWasChanged = false;
	}

	private static void UpdateHeader()
	{
		if (!((Object)(object)_tab == (Object)null) && (Object)(object)_nativeHeader != (Object)null)
		{
			State(_nativeHeader._normalVersion, !_open);
			State(_nativeHeader._selectedVersion, _open);
			State(_nativeHeader._hoverGraphic, !_open && _headerHovered);
			State(_nativeHeader._idleGraphic, _open || !_headerHovered);
			if ((Object)(object)_headerTarget != (Object)null)
			{
				_headerTarget.sprite = (Sprite)((!_open && _headerHovered) ? ((object)(_nativeHeader._hoverSprite ?? UiValueAccess.NormalSprite(_nativeHeader))) : ((object)UiValueAccess.NormalSprite(_nativeHeader)));
			}
			if (_open && !_nativeWasChanged)
			{
				GameObject selectedVersion = _nativeHeader._selectedVersion;
				_nativeWasSelected = (selectedVersion != null && selectedVersion.activeSelf) || UiValueAccess.UiSelected(_nativeHeader) || UiValueAccess.Selected(_nativeHeader);
				_nativeWasChanged = true;
				_nativeHeader.UpdateVisual(false, true);
			}
			else if (!_open)
			{
				RestoreNativeHeader();
			}
		}
		static void State(GameObject? native, bool on)
		{
			if ((Object)(object)native != (Object)null && HeaderVisuals.TryGetValue(native.Identity(), out GameObject value))
			{
				value.SetActive(on);
			}
		}
	}

	private static Image Fill(Transform parent, string name, float x, float y, float w, float h, Color color)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		RectTransform obj = Rect(name, parent);
		Top(obj, x, y, w, h);
		Image obj2 = ((Component)obj).gameObject.AddComponent<Image>();
		((Graphic)obj2).color = color;
		((Graphic)obj2).raycastTarget = false;
		return obj2;
	}

	private static void Line(Transform parent, float x, float y, float width)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		Fill(parent, "Divider", x, y, width, 1f, Edge);
	}

	private static void Frame(RectTransform rect)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Outline obj = ((Component)rect).gameObject.AddComponent<Outline>();
		((Shadow)obj).effectColor = Edge;
		((Shadow)obj).effectDistance = new Vector2(1f, -1f);
		((Shadow)obj).useGraphicAlpha = false;
	}

	private static void Section(Transform parent, string label, float x, float y, float width)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Fill(parent, "SectionStrip", x, y, width, 29f, new Color(0.13f, 0.14f, 0.13f, 0.65f));
		Text(parent, label, x + 9f, y + 3f, width - 18f, 24f, 17);
		Line(parent, x, y, width);
		Line(parent, x, y + 29f, width);
	}

	private static void Metric(Transform parent, string label, string value, float x, float y, float width, Color? color = null)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		Text(parent, label, x + 14f, y + 10f, width - 28f, 22f, 14, Muted);
		Text(parent, value, x + 14f, y + 36f, width - 28f, 38f, 27, color).fontStyle = (FontStyles)0;
		if (width > 0f)
		{
			Fill(parent, "MetricSeparator", x + width - 1f, y + 12f, 1f, 60f, Edge);
		}
	}

	private static void SummaryStrip(Transform parent, float x, float y, float width)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		Image val = Fill(parent, "SummaryStrip", x, y, width, 88f, new Color(0.075f, 0.08f, 0.075f));
		if ((Object)(object)_statisticsStyle != (Object)null)
		{
			val.sprite = _statisticsStyle.sprite;
			val.type = _statisticsStyle.type;
			((Graphic)val).material = ((Graphic)_statisticsStyle).material;
			((Graphic)val).color = new Color(0.45f, 0.45f, 0.43f);
		}
		Frame(((Graphic)val).rectTransform);
	}

	private static void Chosen(Button button, bool selected)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		((Graphic)((Selectable)button).image).color = (Color)(selected ? Accent : new Color(0.055f, 0.06f, 0.055f));
		((Graphic)((Component)button).GetComponentInChildren<TMP_Text>()).color = (Color)(selected ? new Color(0.045f, 0.05f, 0.045f) : Ink);
	}

	internal static void NativeOverview(Tab tab)
	{
		IntPtr value = tab.Identity();
		IntPtr? obj = _overviewTab?.Identity();
		if (value != obj)
		{
			value = tab.Identity();
			IntPtr? obj2 = _nativeHeader?.Identity();
			if (value != obj2)
			{
				return;
			}
		}
		_generation++;
		IconQueue.Clear();
		_open = false;
		RectTransform? root = _root;
		if (root != null)
		{
			((Component)root).gameObject.SetActive(false);
		}
		UpdateHeader();
	}

	internal static void Tick()
	{
		try
		{
			if (HistoryCapture.Enabled)
			{
				HistoryCapture.Tick();
				HistoryNetwork.Tick();
			}
			if ((Object)(object)_screen != (Object)null && (!HistoryCapture.Enabled || !_screen.gameObject.activeInHierarchy))
			{
				Close();
			}
			EnsureEntry();
			if ((Object)(object)_tab != (Object)null && Time.unscaledTime >= _layoutAt)
			{
				_layoutAt = Time.unscaledTime + 0.5f;
				SyncLayout();
			}
			ProbeHeaderClick();
			if (!_open)
			{
				return;
			}
			if (Input.GetKeyDown((KeyCode)27))
			{
				if (_record != null)
				{
					_record = null;
					Fetch();
				}
				else
				{
					_open = false;
					RectTransform? root = _root;
					if (root != null)
					{
						((Component)root).gameObject.SetActive(false);
					}
					UpdateHeader();
				}
			}
			TickIcon();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("对局记录模块异常，原功能保持运行：" + ex.Message));
			Close();
		}
	}

	private static RectTransform Rect(string name, Transform parent)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = new GameObject(name, new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)component).SetParent(parent, false);
		return component;
	}

	private static void Top(RectTransform rt, float x, float y, float w, float h)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = default(Vector2);
		((Vector2)(ref val))._002Ector(0f, 1f);
		rt.anchorMax = val;
		rt.anchorMin = val;
		rt.pivot = new Vector2(0f, 1f);
		rt.anchoredPosition = new Vector2(x, 0f - y);
		rt.sizeDelta = new Vector2(w, h);
	}

	private static TMP_Text Text(Transform parent, string value, float x, float y, float w, float h, int size = 18, Color? color = null)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		RectTransform obj = Rect("Text", parent);
		Top(obj, x, y, w, h);
		TextMeshProUGUI val = ((Component)obj).gameObject.AddComponent<TextMeshProUGUI>();
		if ((Object)(object)_font != (Object)null && (Object)(object)_font.font != (Object)null)
		{
			((TMP_Text)val).font = _font.font;
			((TMP_Text)val).fontSharedMaterial = _font.fontSharedMaterial;
			((TMP_Text)val).spriteAsset = _font.spriteAsset;
		}
		else if ((Object)(object)TMP_Settings.defaultFontAsset != (Object)null)
		{
			((TMP_Text)val).font = TMP_Settings.defaultFontAsset;
		}
		((TMP_Text)val).fontSize = size;
		((Graphic)val).color = (Color)(((_003F?)color) ?? Ink);
		((TMP_Text)val).alignment = (TextAlignmentOptions)4097;
		((TMP_Text)val).richText = false;
		((TMP_Text)val).text = value;
		((Graphic)val).raycastTarget = false;
		((TMP_Text)val).enableWordWrapping = false;
		((TMP_Text)val).overflowMode = (TextOverflowModes)1;
		return (TMP_Text)(object)val;
	}

	private static Button MakeButton(RectTransform rt, string label, Action action)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		Image val = ((Component)rt).gameObject.AddComponent<Image>();
		((Graphic)val).color = new Color(0.055f, 0.06f, 0.055f);
		Frame(rt);
		Button obj = ((Component)rt).gameObject.AddComponent<Button>();
		((Selectable)obj).targetGraphic = (Graphic)(object)val;
		((UnityEvent)obj.onClick).AddListener(new UnityAction(action.Invoke));
		ColorBlock colors = ((Selectable)obj).colors;
		((ColorBlock)(ref colors)).normalColor = Color.white;
		((ColorBlock)(ref colors)).highlightedColor = new Color(1.6f, 1.6f, 1.5f);
		((ColorBlock)(ref colors)).pressedColor = new Color(0.8f, 0.8f, 0.75f);
		((ColorBlock)(ref colors)).selectedColor = Color.white;
		((ColorBlock)(ref colors)).disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.45f);
		((ColorBlock)(ref colors)).fadeDuration = 0.08f;
		((Selectable)obj).colors = colors;
		Text((Transform)(object)rt, label, 8f, 2f, Math.Max(30f, rt.sizeDelta.x - 16f), Math.Max(24f, rt.sizeDelta.y - 4f), 16).alignment = (TextAlignmentOptions)514;
		return obj;
	}

	private static Button Button(Transform parent, string label, float x, float y, float w, float h, Action action)
	{
		RectTransform rt = Rect("Button", parent);
		Top(rt, x, y, w, h);
		return MakeButton(rt, label, action);
	}

	private static void Clear()
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		_generation++;
		IconQueue.Clear();
		_content = null;
		_detailScroll = null;
		if ((Object)(object)_page != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_page).gameObject);
		}
		_page = Rect("HistoryPage", (Transform)(object)_root);
		_page.anchorMin = Vector2.zero;
		_page.anchorMax = Vector2.one;
		RectTransform? page = _page;
		Vector2 offsetMin = (_page.offsetMax = Vector2.zero);
		page.offsetMin = offsetMin;
	}

	private static void Scroll(float top, float bottom = 12f)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = Rect("Scroll", (Transform)(object)_page);
		val.anchorMin = Vector2.zero;
		val.anchorMax = Vector2.one;
		val.offsetMin = new Vector2(18f, bottom);
		val.offsetMax = new Vector2(-18f, 0f - top);
		ScrollRect obj = ((Component)val).gameObject.AddComponent<ScrollRect>();
		obj.horizontal = false;
		obj.movementType = (MovementType)2;
		obj.scrollSensitivity = 35f;
		_detailScroll = obj;
		RectTransform val2 = Rect("Viewport", (Transform)(object)val);
		val2.anchorMin = Vector2.zero;
		val2.anchorMax = Vector2.one;
		Vector2 offsetMin = (val2.offsetMax = Vector2.zero);
		val2.offsetMin = offsetMin;
		((Component)val2).gameObject.AddComponent<RectMask2D>();
		((Graphic)((Component)val2).gameObject.AddComponent<Image>()).color = new Color(0f, 0f, 0f, 0.03f);
		_content = Rect("Content", (Transform)(object)val2);
		_content.anchorMin = new Vector2(0f, 1f);
		_content.anchorMax = new Vector2(1f, 1f);
		_content.pivot = new Vector2(0f, 1f);
		_content.sizeDelta = new Vector2(0f, 100f);
		obj.viewport = val2;
		obj.content = _content;
	}

	private static void Message(string message)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		_shownDetail = "";
		Clear();
		Text((Transform)(object)_page, "对局记录", 14f, 10f, 300f, 40f, 26);
		Text((Transform)(object)_page, message, 14f, 68f, Width - 30f, 90f, 18, Muted).enableWordWrapping = true;
		Button((Transform)(object)_page, "重试", 14f, 164f, 100f, 35f, Fetch);
	}

	private static void Fetch()
	{
		if (_profile == "unresolved")
		{
			Message("当前资料未提供可识别的角色或账号编号，请返回后重新打开资料页。");
			return;
		}
		_record = null;
		Message("正在读取对局记录…");
		int token = _generation;
		HistoryQuery q = new HistoryQuery
		{
			Profile = _profile,
			Search = _search,
			Page = _pageNumber,
			FavoritesOnly = _favoriteOnly
		};
		Load();
		async Task Load()
		{
			HistoryReply reply = await HistoryNetwork.Request("query", q);
			StashBehaviour.Post(delegate
			{
				if (token == _generation && _open)
				{
					if (!reply.Ok)
					{
						Message(reply.Error);
					}
					else
					{
						_reply = reply;
						_pageNumber = reply.Page;
						List();
					}
				}
			});
		}
	}

	private static void SearchInput(float x, float y, float w)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = Rect("Search", (Transform)(object)_page);
		Top(val, x, y, w, 34f);
		((Graphic)((Component)val).gameObject.AddComponent<Image>()).color = new Color(0.015f, 0.02f, 0.015f);
		Frame(val);
		TMP_InputField obj = ((Component)val).gameObject.AddComponent<TMP_InputField>();
		TMP_Text textComponent = Text((Transform)(object)val, _search, 8f, 2f, w - 16f, 32f, 16);
		obj.textComponent = textComponent;
		obj.textViewport = val;
		obj.text = _search;
		obj.placeholder = (Graphic)(object)Text((Transform)(object)val, "搜索地图、日期、物品", 8f, 2f, w - 16f, 32f, 16, Muted);
		obj.characterLimit = 100;
		((UnityEvent<string>)(object)obj.onEndEdit).AddListener((UnityAction<string>)delegate(string v)
		{
			if (!(_search == v))
			{
				_search = v;
				_pageNumber = 1;
				Fetch();
			}
		});
	}

	private static string Amount(double n)
	{
		return n.ToString("N0", CultureInfo.InvariantCulture);
	}

	internal static string Status(string s)
	{
		return s switch
		{
			"Survived" => "撤离成功", 
			"Runner" => "匆匆逃离", 
			"Killed" => "阵亡", 
			"MissingInAction" => "失踪", 
			"Left" => "离开对局", 
			"Transit" => "转图中，尚未结束", 
			_ => s, 
		};
	}

	private static string Map(string id)
	{
		try
		{
			return LocalizationExtensions.Localized(id, (string)null);
		}
		catch
		{
			return id;
		}
	}

	private static void List()
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0497: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_088c: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_08da: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0904: Unknown result type (might be due to invalid IL or missing references)
		//IL_093b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0860: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_07b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_068a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0683: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_07db: Unknown result type (might be due to invalid IL or missing references)
		//IL_07d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_064e: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0647: Unknown result type (might be due to invalid IL or missing references)
		//IL_0640: Unknown result type (might be due to invalid IL or missing references)
		Clear();
		HistoryReply reply = _reply;
		float num = Width - 36f;
		Text((Transform)(object)_page, "对局记录", 18f, 12f, num * 0.55f, 36f, 23);
		Text((Transform)(object)_page, "共 " + reply.Total + " 条记录", 18f + num * 0.65f, 17f, num * 0.35f, 26f, 14, Muted).alignment = (TextAlignmentOptions)4100;
		Line((Transform)(object)_page, 18f, 55f, num);
		SummaryStrip((Transform)(object)_page, 18f, 70f, num);
		float num2 = num / 4f;
		Metric((Transform)(object)_page, "记录对局", reply.Total.ToString(), 18f, 70f, num2);
		Metric((Transform)(object)_page, "幸存率", reply.SurvivalRate.ToString("0") + "%", 18f + num2, 70f, num2);
		Metric((Transform)(object)_page, "击杀数", reply.KillCount.ToString(), 18f + 2f * num2, 70f, num2);
		Metric((Transform)(object)_page, "累计净收益 · ₽", Amount(reply.NetTotal), 18f + 3f * num2, 70f, num2, (reply.NetTotal < 0.0) ? Bad : Good);
		SearchInput(18f, 178f, Math.Max(120f, num - 262f));
		Chosen(Button((Transform)(object)_page, _favoriteOnly ? "★ 只看收藏" : "☆ 只看收藏", 18f + num - 248f, 178f, 138f, 34f, delegate
		{
			_favoriteOnly = !_favoriteOnly;
			_pageNumber = 1;
			Fetch();
		}), _favoriteOnly);
		Button((Transform)(object)_page, "刷新", 18f + num - 98f, 178f, 98f, 34f, Fetch);
		Fill((Transform)(object)_page, "TableHeading", 18f, 232f, num, 31f, new Color(0.1f, 0.11f, 0.1f));
		Line((Transform)(object)_page, 18f, 232f, num);
		Line((Transform)(object)_page, 18f, 263f, num);
		string[] array = new string[9] { "", "日期 / 时间", "地图", "结局", "时长", "击杀", "总收益", "本局消耗", "净收益" };
		float[] array2 = new float[9] { 0f, 0.035f, 0.19f, 0.35f, 0.47f, 0.55f, 0.61f, 0.74f, 0.87f };
		float[] array3 = new float[9] { 0.035f, 0.155f, 0.16f, 0.12f, 0.08f, 0.06f, 0.13f, 0.13f, 0.13f };
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			TMP_Text val = Text((Transform)(object)_page, array[num3], 18f + num * array2[num3] + 5f, 235f, num * array3[num3] - 12f, 24f, 14, Muted);
			if (num3 >= 5)
			{
				val.alignment = (TextAlignmentOptions)4100;
			}
		}
		Scroll(264f, 60f);
		float num4 = 0f;
		int num5 = 0;
		foreach (RaidRecord r in reply.Records)
		{
			Button val2 = Button((Transform)(object)_content, "", 0f, num4, num, 60f, delegate
			{
				Open(r.Id);
			});
			((Graphic)((Selectable)val2).image).color = ((num5++ % 2 == 0) ? new Color(0.065f, 0.07f, 0.065f) : new Color(0.035f, 0.04f, 0.035f));
			((Behaviour)((Component)val2).GetComponent<Outline>()).enabled = false;
			string[] array4 = new string[9]
			{
				"",
				r.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd\nHH:mm"),
				string.Join(" → ", r.Route.Select(Map)),
				Status(r.Status),
				r.ActiveDuration.ToString("hh\\:mm\\:ss"),
				r.Kills.Count.ToString(),
				Amount(r.Income),
				Amount(r.Expense),
				Amount(r.Income + r.Expense)
			};
			for (int num6 = 1; num6 < array4.Length; num6++)
			{
				Color value = (Color)(num6 switch
				{
					3 => (r.Status == "Survived") ? Good : ((r.Status == "Killed") ? Bad : Muted), 
					8 => (r.Income + r.Expense < 0.0) ? Bad : Good, 
					7 => (r.Expense < 0.0) ? Bad : Muted, 
					_ => Ink, 
				});
				TMP_Text val3 = Text(((Component)val2).transform, array4[num6], num * array2[num6] + 5f, 5f, num * array3[num6] - 12f, 50f, (num6 >= 6) ? 16 : 14, value);
				if (num6 >= 5)
				{
					val3.alignment = (TextAlignmentOptions)4100;
				}
				if (num6 == 8)
				{
					val3.fontStyle = (FontStyles)1;
				}
			}
			bool saved = reply.Favorites.Contains(r.Id);
			Button obj = Button(((Component)val2).transform, saved ? "★" : "☆", 2f, 14f, Math.Max(24f, num * 0.035f - 4f), 30f, delegate
			{
				Favorite(r.Id, !saved);
			});
			((Graphic)((Selectable)obj).image).color = Color.clear;
			((Behaviour)((Component)obj).GetComponent<Outline>()).enabled = false;
			((Graphic)((Component)obj).GetComponentInChildren<TMP_Text>()).color = (saved ? Accent : Muted);
			Line((Transform)(object)_content, 0f, num4 + 59f, num);
			num4 += 60f;
		}
		if (reply.Records.Count == 0)
		{
			Text((Transform)(object)_content, _favoriteOnly ? "尚无收藏的对局" : "暂无匹配的对局记录", 8f, 50f, num - 20f, 35f, 19, Muted).alignment = (TextAlignmentOptions)514;
			num4 = 130f;
		}
		_content.sizeDelta = new Vector2(0f, num4);
		RectTransform obj2 = Rect("HistoryFooter", (Transform)(object)_page);
		obj2.anchorMin = new Vector2(0f, 0f);
		obj2.anchorMax = new Vector2(1f, 0f);
		obj2.pivot = new Vector2(0f, 0f);
		obj2.anchoredPosition = new Vector2(18f, 8f);
		obj2.sizeDelta = new Vector2(-36f, 42f);
		Line((Transform)(object)obj2, 0f, 0f, num);
		Text((Transform)(object)obj2, "每页 20 条", 0f, 9f, 120f, 25f, 14, Muted);
		((Selectable)Button((Transform)(object)obj2, "上一页", num - 268f, 7f, 90f, 30f, delegate
		{
			if (_pageNumber > 1)
			{
				_pageNumber--;
				Fetch();
			}
		})).interactable = _pageNumber > 1;
		Text((Transform)(object)obj2, _pageNumber + " / " + Math.Max(1, (reply.Total + 19) / 20), num - 173f, 9f, 76f, 25f, 14).alignment = (TextAlignmentOptions)514;
		((Selectable)Button((Transform)(object)obj2, "下一页", num - 92f, 7f, 90f, 30f, delegate
		{
			_pageNumber++;
			Fetch();
		})).interactable = _pageNumber * 20 < reply.Total;
	}

	private static void Open(string id, string? profile = null)
	{
		Message("正在加载对局详情…");
		int token = _generation;
		string owner = profile ?? _profile;
		Load();
		async Task Load()
		{
			HistoryReply response = await HistoryNetwork.Request("query", new HistoryQuery
			{
				Profile = owner,
				Id = id
			});
			StashBehaviour.Post(delegate
			{
				if (token == _generation && _open)
				{
					if (!response.Ok || response.Records.Count == 0)
					{
						Message((response.Error.Length > 0) ? response.Error : "记录不存在");
					}
					else
					{
						_reply = response;
						_record = response.Records[0];
						_detail = "概况";
						Detail();
					}
				}
			});
		}
	}

	private static void Favorite(string id, bool saved)
	{
		int token = _generation;
		string owner = _record?.Profile ?? _profile;
		Save();
		async Task Save()
		{
			HistoryReply r = await HistoryNetwork.Request("favorite", new HistoryQuery
			{
				Profile = owner,
				Id = id,
				Favorite = saved
			});
			StashBehaviour.Post(delegate
			{
				if (token == _generation)
				{
					if (!r.Ok)
					{
						StashBehaviour.Notify(r.Error);
					}
					else
					{
						if (saved)
						{
							_reply.Favorites.Add(id);
						}
						else
						{
							_reply.Favorites.Remove(id);
						}
						if (_record == null)
						{
							List();
						}
						else
						{
							Detail();
						}
					}
				}
			});
		}
	}

	private static void Detail()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0429: Unknown result type (might be due to invalid IL or missing references)
		//IL_0422: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_0daa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b3b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dff: Unknown result type (might be due to invalid IL or missing references)
		//IL_1389: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0729: Unknown result type (might be due to invalid IL or missing references)
		//IL_0776: Unknown result type (might be due to invalid IL or missing references)
		//IL_13e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_13e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1412: Unknown result type (might be due to invalid IL or missing references)
		//IL_1429: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e7c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c5f: Unknown result type (might be due to invalid IL or missing references)
		//IL_108d: Unknown result type (might be due to invalid IL or missing references)
		//IL_12e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_11bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0862: Unknown result type (might be due to invalid IL or missing references)
		//IL_0952: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a04: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fe8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fe1: Unknown result type (might be due to invalid IL or missing references)
		//IL_09a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_099e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a47: Unknown result type (might be due to invalid IL or missing references)
		RaidRecord r = _record;
		string text = r.Profile + "/" + r.Id;
		float value = ((_shownDetail == text && (Object)(object)_content != (Object)null) ? _content.anchoredPosition.y : 0f);
		Clear();
		_shownDetail = text;
		float num = Width - 36f;
		Button((Transform)(object)_page, "‹ 返回记录", 18f, 12f, 124f, 32f, Fetch);
		bool saved = _reply.Favorites.Contains(r.Id);
		Button((Transform)(object)_page, "刷新", 152f, 12f, 56f, 32f, delegate
		{
			Open(r.Id, r.Profile);
		});
		float num2 = ((num < 440f) ? 36 : 132);
		Chosen(Button((Transform)(object)_page, (!(num < 440f)) ? (saved ? "★ 已收藏" : "☆ 收藏对局") : (saved ? "★" : "☆"), 18f + num - num2, 12f, num2, 32f, delegate
		{
			Favorite(r.Id, !saved);
		}), saved);
		Text((Transform)(object)_page, string.Join(" → ", r.Route.Select(Map)), 18f, 58f, num * 0.68f, 35f, 23);
		Text((Transform)(object)_page, Status(r.Status), 18f + num * 0.7f, 62f, num * 0.3f, 27f, 17, (r.Status == "Survived") ? Good : ((r.Status == "Killed") ? Bad : Muted)).alignment = (TextAlignmentOptions)4100;
		TeamMemberSummary teamMemberSummary = TeamRules.Summary(r.Profile, r.Name, r);
		Text((Transform)(object)_page, r.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "    " + r.Name + "    " + r.Side + "    PMC " + teamMemberSummary.PmcKills + "    Scav/头目 " + teamMemberSummary.ScavKills + "    时长 " + r.ActiveDuration.ToString("hh\\:mm\\:ss"), 18f, 99f, num, 24f, 14, Muted);
		Scroll(132f);
		RectTransform content = _content;
		float num3 = TeamTable((Transform)(object)content, r, num);
		SummaryStrip((Transform)(object)content, 0f, num3, num);
		float num4 = num / 4f;
		Metric((Transform)(object)content, "总收益 · ₽", Amount(r.Income), 0f, num3, num4);
		Metric((Transform)(object)content, "本局消耗 · ₽", Amount(r.Expense), num4, num3, num4, (r.Expense < 0.0) ? Bad : Muted);
		Metric((Transform)(object)content, "净收益 · ₽", Amount(r.Income + r.Expense), 2f * num4, num3, num4, (r.Income + r.Expense < 0.0) ? Bad : Good);
		Metric((Transform)(object)content, "入局总战备 · ₽", Amount(r.Loadout), 3f * num4, num3, num4);
		int num5 = 0;
		float num6 = num / 4f;
		string[] array = new string[4] { "概况", "带出物资", "击杀记录", "任务与成就" };
		foreach (string text2 in array)
		{
			string chosen = text2;
			Chosen(Button((Transform)(object)content, (text2 == "概况") ? "行动概况" : text2, (float)num5 * num6, num3 + 108f, num6 - 3f, 34f, delegate
			{
				_detail = chosen;
				Detail();
			}), _detail == text2);
			num5++;
		}
		RectTransform val = Rect("MemberDetail", (Transform)(object)content);
		float num8 = num3 + 162f;
		Top(val, 0f, num8, num, 100f);
		_content = val;
		float num9 = 0f;
		if (_detail == "概况")
		{
			float num10 = num * 0.56f;
			float num11 = num * 0.6f;
			float num12 = num - num11 - 12f;
			Section((Transform)(object)_content, "最高价值物资", 0f, 0f, num10 - 16f);
			num9 = 44f;
			foreach (HistoryItem item in (from i in r.Items
				where i.Valued && i.Destination != "遗失或已消耗" && i.Units > i.BroughtUnits
				orderby i.Price * (i.Units - i.BroughtUnits) descending
				select i).Take(3))
			{
				Picture(item, 8f, num9, 60f, 55f);
				Text((Transform)(object)_content, item.Name, 80f, num9 + 2f, num10 - 220f, 28f, 17);
				Text((Transform)(object)_content, "×" + item.Count + " · " + item.Destination, 80f, num9 + 32f, num10 - 220f, 24f, 14, Muted);
				Text((Transform)(object)_content, Amount(item.Price * (item.Units - item.BroughtUnits)), num10 - 136f, num9 + 15f, 128f, 30f, 18, Good).alignment = (TextAlignmentOptions)260;
				num9 += 78f;
			}
			Button((Transform)(object)_content, "查看全部物资 →", 8f, num9, num10 - 20f, 32f, delegate
			{
				_detail = "带出物资";
				Detail();
			});
			num9 += 58f;
			Section((Transform)(object)_content, "行动事件", 0f, num9, num10 - 16f);
			num9 += 40f;
			foreach (string @event in r.Events)
			{
				Text((Transform)(object)_content, @event, 10f, num9, num10 - 20f, 42f, 16, Muted).enableWordWrapping = true;
				num9 += 48f;
			}
			Section((Transform)(object)_content, "收益明细", num11, 0f, num12);
			string[] array2 = new string[4] { "总收益", "带入物品损耗", "净收益", "入局战备" };
			double[] array3 = new double[4]
			{
				r.Income,
				r.Expense,
				r.Income + r.Expense,
				r.Loadout
			};
			for (int num13 = 0; num13 < 4; num13++)
			{
				Text((Transform)(object)_content, array2[num13], num11, 52 + num13 * 54, num12 * 0.5f, 28f, 16, Muted);
				Text((Transform)(object)_content, Amount(array3[num13]), num11 + num12 * 0.5f, 52 + num13 * 54, num12 * 0.5f, 28f, 19, (Color)(num13 switch
				{
					2 => Good, 
					1 => Bad, 
					_ => Ink, 
				})).alignment = (TextAlignmentOptions)260;
				Line((Transform)(object)_content, num11, 94 + num13 * 54, num12);
			}
			Text((Transform)(object)_content, "本局拾取后又使用或丢弃的物资，不留在总收益中，也不计入带入损耗。", num11, 290f, num12, 82f, 15, Muted).enableWordWrapping = true;
			Text((Transform)(object)_content, r.Reconciled ? "已核对结算后物品" : "尚未核对最终物品", num11, 390f, num12, 30f, 15, Muted);
			num9 = Math.Max(num9, 435f);
		}
		else if (_detail == "带出物资")
		{
			Chosen(Button((Transform)(object)_content, "装备总览", 0f, 0f, 124f, 32f, delegate
			{
				_lootList = false;
				Detail();
			}), !_lootList);
			Chosen(Button((Transform)(object)_content, "物品列表", 132f, 0f, 124f, 32f, delegate
			{
				_lootList = true;
				Detail();
			}), _lootList);
			Text((Transform)(object)_content, "行动结束时的物品快照", 272f, 3f, num - 284f, 25f, 14, Muted).alignment = (TextAlignmentOptions)4100;
			RectTransform val2 = Rect("LootBody", (Transform)(object)_content);
			Top(val2, 0f, 52f, num, 100f);
			RectTransform content2 = _content;
			_content = val2;
			if (_lootList)
			{
				foreach (HistoryItem item2 in r.Items.Where((HistoryItem i) => i.Valued))
				{
					Picture(item2, 8f, num9, 52f, 46f);
					Text((Transform)(object)_content, item2.Name + " ×" + item2.Count, 76f, num9, num * 0.43f, 28f, 17);
					Text((Transform)(object)_content, item2.Destination, num * 0.52f, num9, num * 0.23f, 28f, 15, Muted);
					Text((Transform)(object)_content, Amount(item2.Price * item2.Units), num * 0.77f, num9, num * 0.22f, 28f).alignment = (TextAlignmentOptions)260;
					num9 += 58f;
				}
			}
			else
			{
				num9 = Inventory(r, num);
			}
			val2.sizeDelta = new Vector2(num, num9);
			_content = content2;
			num9 += 52f;
		}
		else if (_detail == "击杀记录")
		{
			string[] array4 = new string[6] { "时间", "目标", "类型", "等级", "武器", "距离" };
			float[] array5 = new float[6] { 0f, 0.09f, 0.34f, 0.47f, 0.55f, 0.87f };
			float[] array6 = new float[6] { 0.09f, 0.25f, 0.13f, 0.08f, 0.32f, 0.13f };
			Fill((Transform)(object)_content, "KillHeading", 0f, 0f, num, 32f, new Color(0.1f, 0.11f, 0.1f));
			Line((Transform)(object)_content, 0f, 32f, num);
			for (int num14 = 0; num14 < array4.Length; num14++)
			{
				TMP_Text val3 = Text((Transform)(object)_content, array4[num14], num * array5[num14] + 8f, 3f, num * array6[num14] - 16f, 26f, 14, Muted);
				if (num14 == 5)
				{
					val3.alignment = (TextAlignmentOptions)4100;
				}
			}
			num9 = 34f;
			foreach (HistoryKill kill in r.Kills)
			{
				Fill((Transform)(object)_content, "KillRow", 0f, num9, num, 48f, new Color(0.045f, 0.05f, 0.045f));
				string text3 = ((!(kill.Side == "Savage")) ? kill.Side : (string.IsNullOrEmpty(kill.Role) ? "Scav" : Map(kill.Role)));
				if (kill.IsAi.HasValue)
				{
					text3 = text3 + " · " + (kill.IsAi.Value ? "AI" : "玩家");
				}
				string[] array7 = new string[6]
				{
					TimeSpan.FromSeconds(kill.Seconds).ToString("mm\\:ss"),
					kill.Name,
					text3,
					(kill.Side == "Savage" || kill.Level <= 0) ? "—" : ("Lv. " + kill.Level),
					kill.Weapon,
					kill.Distance.ToString("F0") + " m"
				};
				for (int num15 = 0; num15 < array7.Length; num15++)
				{
					TMP_Text val4 = Text((Transform)(object)_content, array7[num15], num * array5[num15] + 8f, num9 + 6f, num * array6[num15] - 16f, 34f, 15, (num15 == 0) ? Muted : Ink);
					if (num15 == 5)
					{
						val4.alignment = (TextAlignmentOptions)4100;
					}
				}
				Line((Transform)(object)_content, 0f, num9 + 47f, num);
				num9 += 48f;
			}
			if (r.Kills.Count == 0)
			{
				Text((Transform)(object)_content, "本局无击杀记录", 8f, 70f, num - 20f, 35f, 18, Muted).alignment = (TextAlignmentOptions)514;
				num9 = 130f;
			}
		}
		else if (_detail == "任务与成就")
		{
			foreach (string item3 in r.Tasks.Select((string x) => "任务: " + x).Concat(r.Achievements.Select((string x) => "成就: " + Map(x))))
			{
				Text((Transform)(object)_content, item3, 8f, num9, num - 20f, 55f).enableWordWrapping = true;
				num9 += 60f;
			}
			if (num9 == 0f)
			{
				Text((Transform)(object)_content, "本局未记录到任务或成就变化", 8f, 15f, num - 20f, 35f, 18, Muted);
				num9 = 65f;
			}
		}
		else if (_detail == "物品列表")
		{
			foreach (HistoryItem item4 in r.Items.Where((HistoryItem i) => i.Valued))
			{
				Picture(item4, 6f, num9, 60f, 54f);
				Text((Transform)(object)_content, item4.Name + " ×" + item4.Count, 80f, num9 + 2f, num - 250f, 27f);
				Text((Transform)(object)_content, item4.Destination + " · " + ((item4.BroughtUnits > 0.0) ? "含带入物品" : "本局获得"), 80f, num9 + 29f, num - 250f, 24f, 14, Muted);
				Text((Transform)(object)_content, item4.Priced ? Amount(item4.Price * item4.Units) : "暂无报价", num - 162f, num9 + 12f, 155f, 28f);
				num9 += 65f;
			}
		}
		else
		{
			num9 = Inventory(r, num);
		}
		val.sizeDelta = new Vector2(num, num9 + 30f);
		_content = content;
		content.sizeDelta = new Vector2(0f, num8 + num9 + 30f);
		ScrollRect? detailScroll = _detailScroll;
		float? num16;
		if (detailScroll == null)
		{
			num16 = null;
		}
		else
		{
			RectTransform viewport = detailScroll.viewport;
			if (viewport == null)
			{
				num16 = null;
			}
			else
			{
				Rect rect = viewport.rect;
				num16 = ((Rect)(ref rect)).height;
			}
		}
		float? num17 = num16;
		float valueOrDefault = num17.GetValueOrDefault();
		content.anchoredPosition = new Vector2(0f, Math.Clamp(value, 0f, Math.Max(0f, content.sizeDelta.y - valueOrDefault)));
	}

	private static float TeamTable(Transform parent, RaidRecord selected, float w)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0960: Unknown result type (might be due to invalid IL or missing references)
		//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0754: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0862: Unknown result type (might be due to invalid IL or missing references)
		//IL_085b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0590: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_058c: Unknown result type (might be due to invalid IL or missing references)
		//IL_067f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0585: Unknown result type (might be due to invalid IL or missing references)
		//IL_057e: Unknown result type (might be due to invalid IL or missing references)
		//IL_08f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d6: Unknown result type (might be due to invalid IL or missing references)
		ConfigEntry<bool> showTeam = HistorySettings.ShowTeam;
		if ((showTeam != null && !showTeam.Value) || selected.Team == null)
		{
			return 0f;
		}
		TeamRaidView teamRaidView = _reply?.Team;
		if (teamRaidView == null)
		{
			Text(parent, "队伍统计暂不可用，请确认配套服务端已更新后刷新。", 0f, 0f, w, 40f, 15, Muted);
			return 56f;
		}
		if (teamRaidView.Members.Count < 2)
		{
			return 0f;
		}
		Section(parent, "队伍成员 · " + teamRaidView.Members.Count + " 人", 0f, 0f, w);
		Text(parent, "点击成员查看明细；未上传显示等待，不按零收益计算。", 0f, 35f, w, 28f, 14, Muted);
		bool flag = HistoryTeamLayout.A(w);
		float num = HistoryTeamLayout.a(w);
		float num2 = 70f;
		HistoryBox[] array = HistoryTeamLayout.B(w);
		if (flag)
		{
			string[] array2 = new string[7] { "玩家", "总收益", "本局消耗", "净收益", "PMC", "Scav/头目", "结果" };
			for (int i = 0; i < array2.Length; i++)
			{
				HistoryBox historyBox = array[i];
				TMP_Text val = Text(parent, array2[i], historyBox.X + 8f, num2, historyBox.Width - 16f, 28f, 14, Muted);
				if (i > 0 && i < 6)
				{
					val.alignment = (TextAlignmentOptions)4100;
				}
			}
			num2 += 32f;
		}
		foreach (TeamMemberSummary member in teamRaidView.Members)
		{
			Button val2 = Button(parent, "", 0f, num2, w, num, delegate
			{
				Open(member.RecordId, member.Profile);
			});
			((Selectable)val2).interactable = member.HasRecord;
			((Graphic)((Selectable)val2).image).color = ((member.Profile == selected.Profile) ? new Color(0.11f, 0.12f, 0.105f) : new Color(0.045f, 0.05f, 0.045f));
			((Behaviour)((Component)val2).GetComponent<Outline>()).enabled = false;
			string text = HistoryTeamLayout.A(member.HasRecord, member.Joined, member.Final, member.Reconciled, member.Status);
			string text2 = (member.HasRecord ? Status(member.Status) : text);
			string text3 = member.Name + ((member.Profile == selected.Profile) ? " · 当前" : "");
			string text4 = (member.Income.HasValue ? Amount(member.Income.Value) : "—");
			string text5 = (member.Expense.HasValue ? Amount(member.Expense.Value) : "—");
			string text6 = ((member.Income.HasValue && member.Expense.HasValue) ? Amount(member.Income.Value + member.Expense.Value) : "—");
			string text7 = member.PmcKills?.ToString() ?? "—";
			string text8 = member.ScavKills?.ToString() ?? "—";
			Color val3 = ((member.Income.HasValue && member.Expense.HasValue && member.Income + member.Expense < 0.0) ? Bad : Good);
			if (flag)
			{
				string[] array3 = new string[7] { text3, text4, text5, text6, text7, text8, text2 };
				for (int num3 = 0; num3 < array3.Length; num3++)
				{
					HistoryBox historyBox2 = array[num3];
					TMP_Text val4 = Text(((Component)val2).transform, array3[num3], historyBox2.X + 8f, 6f, historyBox2.Width - 16f, 28f, 15, (Color)(num3 switch
					{
						6 => Muted, 
						3 => val3, 
						2 => Bad, 
						_ => Ink, 
					}));
					if (num3 > 0 && num3 < 6)
					{
						val4.alignment = (TextAlignmentOptions)4100;
					}
					if (num3 == 3)
					{
						val4.fontStyle = (FontStyles)1;
					}
				}
				string value = ((!member.HasRecord) ? "" : ((member.UnknownIdentityKills > 0) ? "真人/AI身份未完整提供" : ("AI " + member.AiKills + " · 玩家 " + member.PlayerKills)));
				Text(((Component)val2).transform, value, 8f, 34f, w * 0.45f, 24f, 12, Muted);
				if (member.HasRecord && text.Length > 0)
				{
					Text(((Component)val2).transform, text, w * 0.5f, 34f, w * 0.5f - 8f, 24f, 12, Muted).alignment = (TextAlignmentOptions)4100;
				}
			}
			else
			{
				Text(((Component)val2).transform, text3, 8f, 2f, w * 0.55f - 16f, 25f, 16);
				Text(((Component)val2).transform, text2, w * 0.55f, 2f, w * 0.45f - 8f, 25f, 14, Muted).alignment = (TextAlignmentOptions)4100;
				float num4 = w / 3f;
				Text(((Component)val2).transform, "收益 " + text4, 8f, 30f, num4 - 12f, 24f, 14);
				Text(((Component)val2).transform, "消耗 " + text5, num4 + 4f, 30f, num4 - 8f, 24f, 14, member.Expense.HasValue ? Bad : Muted);
				Text(((Component)val2).transform, "净收益 " + text6, 2f * num4 + 4f, 30f, num4 - 12f, 24f, 14, member.Income.HasValue ? val3 : Muted);
				Text(((Component)val2).transform, "PMC " + text7 + "    Scav/头目 " + text8, 8f, 57f, w * 0.5f - 12f, 24f, 14, Muted);
				if (member.HasRecord && text.Length > 0)
				{
					Text(((Component)val2).transform, text, w * 0.5f, 57f, w * 0.5f - 8f, 24f, 12, Muted).alignment = (TextAlignmentOptions)4100;
				}
			}
			Line(parent, 0f, num2 + num - 1f, w);
			num2 += num;
		}
		Text(parent, "PMC 包含 AI PMC；击杀阵营与真人/AI身份分别统计。", 0f, num2 + 8f, w, 26f, 13, Muted);
		return num2 + 54f;
	}

	private static float Inventory(RaidRecord r, float w)
	{
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0995: Unknown result type (might be due to invalid IL or missing references)
		InventoryEquipment val = (((Object)(object)ItemUiContext.Instance == (Object)null) ? null : TradeAccess.Profile(ItemUiContext.Instance)?.Inventory?.Equipment);
		if (val != null)
		{
			Slot[] slots = ((CompoundItem)val).Slots;
			foreach (Slot slot in slots)
			{
				HistoryItem historyItem = r.Items.FirstOrDefault(delegate(HistoryItem historyItem3)
				{
					string id = historyItem3.Id;
					Item containedItem = slot.ContainedItem;
					return id == ((containedItem != null) ? containedItem.Id : null);
				});
				if (historyItem != null && !historyItem.Storage)
				{
					historyItem.Slot = slot.ID;
				}
			}
		}
		foreach (HistoryItem item in r.Items.Where((HistoryItem historyItem3) => !historyItem3.Storage && r.Items.Any((HistoryItem c) => c.Parent == historyItem3.Id && c.Slot.StartsWith("pocket", StringComparison.Ordinal))))
		{
			item.Slot = "Pockets";
			item.Valued = false;
		}
		List<HistoryItem> list = r.Items.Where((HistoryItem historyItem3) => historyItem3.Destination != "遗失或已消耗").ToList();
		Dictionary<string, HistoryItem> lookup = list.ToDictionary((HistoryItem historyItem3) => historyItem3.Id);
		if (list.Count == 0)
		{
			Text((Transform)(object)_content, "无保留物品快照", 8f, 15f, w - 20f, 35f, 18, Muted);
			return 65f;
		}
		bool flag = w >= 1080f;
		float num = Math.Min(280f, w * 0.32f);
		int columns = (flag ? 3 : 2);
		float num2 = (w - num - 32f) / (float)(columns - 1);
		float[] array = ((!flag) ? new float[2] { num, num2 } : new float[3] { num, num2, num2 });
		float[] array2 = ((!flag) ? new float[2]
		{
			0f,
			num + 16f
		} : new float[3]
		{
			0f,
			num + 16f,
			num + num2 + 32f
		});
		float[] array3 = new float[columns];
		List<IGrouping<(string, string), HistoryItem>> source = (from historyItem3 in list
			where historyItem3.Storage
			group historyItem3 by (Parent: historyItem3.Parent, Slot: historyItem3.Slot)).ToList();
		HashSet<(string Parent, string Slot)> drawn = new HashSet<(string, string)>();
		List<HistoryItem> list2 = list.Where((HistoryItem historyItem3) => !historyItem3.Storage && !lookup.ContainsKey(historyItem3.Parent) && historyItem3.Slot != "Pockets" && historyItem3.Slot != "Dogtag" && !string.IsNullOrWhiteSpace(historyItem3.Name)).ToList();
		List<HistoryItem> list3 = list2.Where(delegate(HistoryItem historyItem3)
		{
			bool flag2;
			switch (historyItem3.Slot)
			{
			case "TacticalVest":
			case "Backpack":
			case "SecuredContainer":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			return flag2 || historyItem3.Destination.StartsWith("已送出");
		}).ToList();
		List<HistoryItem> list4 = list2.Where(delegate(HistoryItem historyItem3)
		{
			string slot2 = historyItem3.Slot;
			return (slot2 == "FirstPrimaryWeapon" || slot2 == "SecondPrimaryWeapon") ? true : false;
		}).ToList();
		string[] order = new string[8] { "Earpiece", "Headwear", "FaceCover", "Eyewear", "ArmorVest", "ArmBand", "Holster", "Scabbard" };
		List<HistoryItem> list5 = list2.Except(list3).Except(list4).OrderBy(delegate(HistoryItem historyItem3)
		{
			int num24 = Array.IndexOf(order, historyItem3.Slot);
			return (num24 >= 0) ? num24 : 99;
		})
			.ToList();
		Section((Transform)(object)_content, "随身装备", 0f, 0f, num);
		array3[0] = 40f;
		for (int num3 = 0; num3 < list5.Count; num3 += 2)
		{
			float num4 = 0f;
			float num5 = (num - 10f) / 2f;
			foreach (HistoryItem item2 in list5.Skip(num3).Take(2))
			{
				float x = ((item2 == list5[num3]) ? 0f : (num5 + 10f));
				float num6 = array3[0];
				Section((Transform)(object)_content, SlotName(item2.Slot), x, num6, num5);
				float num7 = Math.Min(46f, Math.Min(num5 / (float)Math.Max(1, item2.Width), 164f / (float)Math.Max(1, item2.Height)));
				float num8 = Math.Max(82f, (float)item2.Height * num7);
				Tile(item2, x, num6 + 34f, num5, num8);
				num4 = Math.Max(num4, num8 + 47f);
			}
			array3[0] += num4;
		}
		foreach (HistoryItem item3 in list4)
		{
			float num9 = array3[0];
			Section((Transform)(object)_content, SlotName(item3.Slot), 0f, num9, num);
			float num10 = Math.Clamp((float)item3.Height * Math.Min(46f, num / (float)Math.Max(1, item3.Width)), 70f, 140f);
			Tile(item3, 0f, num9 + 34f, num, num10);
			array3[0] += num10 + 50f;
		}
		List<IGrouping<(string, string), HistoryItem>> list6 = (from g in source
			where lookup.TryGetValue(g.Key.Parent, out var value2) && value2.Slot == "Pockets"
			orderby g.Key.Slot
			select g).ToList();
		if (list6.Count > 0)
		{
			Section((Transform)(object)_content, "口袋", 0f, array3[0], num);
			array3[0] += 36f;
			float num11 = 0f;
			float num12 = Math.Min(52f, (num - (float)((list6.Count - 1) * 7)) / (float)list6.Count);
			for (int num13 = 0; num13 < list6.Count; num13++)
			{
				num11 = Math.Max(num11, Container(null, new IGrouping<(string, string), HistoryItem>[1] { list6[num13] }, (float)num13 * (num12 + 7f), array3[0], num12));
			}
			array3[0] += num11 + 20f;
		}
		foreach (HistoryItem owner in list3.OrderBy((HistoryItem historyItem3) => (!(historyItem3.Slot == "TacticalVest")) ? ((historyItem3.Slot == "SecuredContainer") ? 1 : 2) : 0))
		{
			int num14 = Math.Max(1, Column(owner));
			float num15 = array2[num14];
			float num16 = array3[num14];
			float num17 = array[num14];
			Section((Transform)(object)_content, owner.Destination.StartsWith("已送出") ? "已送出物资" : SlotName(owner.Slot), num15, num16, num17);
			num16 += 36f;
			float num18 = Math.Min(106f, num17 * 0.3f);
			float num19 = Math.Clamp((float)owner.Height * num18 / (float)Math.Max(1, owner.Width), 82f, 164f);
			Tile(owner, num15, num16, num18, num19);
			float val2 = Container(owner, source.Where((IGrouping<(string Parent, string Slot), HistoryItem> g) => g.Key.Parent == owner.Id), num15 + num18 + 12f, num16, num17 - num18 - 12f);
			array3[num14] = num16 + Math.Max(num19, val2) + 22f;
		}
		foreach (IGrouping<string, IGrouping<(string, string), HistoryItem>> item4 in from g in source
			group g by g.Key.Parent)
		{
			IGrouping<(string, string), HistoryItem>[] array4 = item4.Where((IGrouping<(string Parent, string Slot), HistoryItem> g) => !drawn.Contains(g.Key)).ToArray();
			if (array4.Length != 0)
			{
				HistoryItem historyItem2 = array4[0].First();
				int num20 = Column(historyItem2);
				float x2 = array2[num20];
				float num21 = array3[num20];
				float num22 = array[num20];
				lookup.TryGetValue(historyItem2.Parent, out var value);
				Section((Transform)(object)_content, value?.Name ?? "寄送物资", x2, num21, num22);
				num21 += 36f;
				array3[num20] = num21 + Container(value, array4, x2, num21, num22) + 22f;
			}
		}
		float num23 = array3.Max() + 12f;
		Line((Transform)(object)_content, 0f, num23, w);
		Text((Transform)(object)_content, "带入物品不重复计入本局收益", 0f, num23 + 7f, w, 24f, 13, Muted);
		return num23 + 36f;
		int Column(HistoryItem historyItem3)
		{
			if (historyItem3.Destination.StartsWith("已送出"))
			{
				return columns - 1;
			}
			HashSet<string> hashSet = new HashSet<string>();
			HistoryItem historyItem4 = historyItem3;
			HistoryItem value2;
			while (lookup.TryGetValue(historyItem4.Parent, out value2) && hashSet.Add(historyItem4.Id))
			{
				historyItem4 = value2;
			}
			string slot2 = historyItem4.Slot;
			if ((!(slot2 == "TacticalVest") && !(slot2 == "SecuredContainer")) || 1 == 0)
			{
				if (!(historyItem4.Slot == "Backpack"))
				{
					return 0;
				}
				return columns - 1;
			}
			return 1;
		}
		float Container(HistoryItem? historyItem3, IEnumerable<IGrouping<(string Parent, string Slot), HistoryItem>> children, float x3, float y, float available)
		{
			IGrouping<(string, string), HistoryItem>[] array5 = children.ToArray();
			List<HistoryGridSpec> list7 = ((historyItem3 == null) ? Array.Empty<HistoryGridSpec>() : HistoryContainerLayout.Read(historyItem3.Template)).ToList();
			IGrouping<(string, string), HistoryItem>[] array6 = array5;
			foreach (IGrouping<(string Parent, string Slot), HistoryItem> group in array6)
			{
				drawn.Add(group.Key);
				int width = Math.Max(1, group.Max((HistoryItem historyItem4) => Math.Max(historyItem4.GridWidth, historyItem4.X + (historyItem4.Rotated ? historyItem4.Height : historyItem4.Width))));
				int height = Math.Max(1, group.Max((HistoryItem historyItem4) => Math.Max(historyItem4.GridHeight, historyItem4.Y + (historyItem4.Rotated ? historyItem4.Width : historyItem4.Height))));
				int num24 = list7.FindIndex((HistoryGridSpec g) => g.Slot == group.Key.Slot);
				if (num24 < 0)
				{
					list7.Add(new HistoryGridSpec(group.Key.Slot, width, height));
				}
				else
				{
					list7[num24] = list7[num24]with
					{
						Width = width,
						Height = height
					};
				}
			}
			HistoryGridPlan historyGridPlan = HistoryGridLayout.Build(list7, available);
			Dictionary<string, HistoryItem[]> dictionary = array5.ToDictionary((IGrouping<(string Parent, string Slot), HistoryItem> g) => g.Key.Slot, (IGrouping<(string Parent, string Slot), HistoryItem> g) => g.ToArray());
			HistoryGridPlacement[] grids = historyGridPlan.Grids;
			for (int j = 0; j < grids.Length; j++)
			{
				HistoryGridPlacement historyGridPlacement = grids[j];
				Grid(dictionary.GetValueOrDefault(historyGridPlacement.Slot) ?? Array.Empty<HistoryItem>(), historyGridPlacement.Box, x3, y, historyGridPlan.Cell);
			}
			return historyGridPlan.Height;
		}
		static void Grid(HistoryItem[] items, HistoryBox box, float num24, float y, float cell)
		{
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			num24 += box.X;
			y += box.Y;
			float width = box.Width;
			float height = box.Height;
			int num25 = (int)Math.Round(width / cell);
			int num26 = (int)Math.Round(height / cell);
			Frame(((Graphic)Fill((Transform)(object)_content, "InventoryGrid", num24, y, width, height, new Color(0.035f, 0.045f, 0.035f))).rectTransform);
			if (num25 <= 64 && num26 <= 128)
			{
				for (int j = 1; j < num25; j++)
				{
					Fill((Transform)(object)_content, "GridLine", num24 + (float)j * cell, y, 1f, height, new Color(0.19f, 0.21f, 0.18f, 0.65f));
				}
				for (int k = 1; k < num26; k++)
				{
					Fill((Transform)(object)_content, "GridLine", num24, y + (float)k * cell, width, 1f, new Color(0.19f, 0.21f, 0.18f, 0.65f));
				}
			}
			foreach (HistoryItem historyItem3 in items)
			{
				Tile(historyItem3, num24 + (float)historyItem3.X * cell, y + (float)historyItem3.Y * cell, (float)(historyItem3.Rotated ? historyItem3.Height : historyItem3.Width) * cell, (float)(historyItem3.Rotated ? historyItem3.Width : historyItem3.Height) * cell);
			}
		}
		static string SlotName(string text)
		{
			return text switch
			{
				"Headwear" => "头部装备", 
				"Earpiece" => "耳机", 
				"FaceCover" => "面部装备", 
				"Eyewear" => "眼部装备", 
				"ArmorVest" => "身体护甲", 
				"TacticalVest" => "战术胸挂", 
				"Backpack" => "背包", 
				"SecuredContainer" => "保险箱", 
				"Pockets" => "口袋", 
				"FirstPrimaryWeapon" => "主武器", 
				"SecondPrimaryWeapon" => "副武器", 
				"Holster" => "手枪", 
				"Scabbard" => "近战武器", 
				"Dogtag" => "狗牌", 
				"ArmBand" => "臂带", 
				_ => Map(text), 
			};
		}
	}

	private static void Tile(HistoryItem i, float x, float y, float w, float h)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		RectTransform obj = Rect("ItemSlot", (Transform)(object)_content);
		Top(obj, x, y, w - 2f, h - 2f);
		((Graphic)((Component)obj).gameObject.AddComponent<Image>()).color = new Color(0.16f, 0.175f, 0.16f, 0.8f);
		Frame(obj);
		Picture(i, x + 3f, y + 3f, Math.Max(1f, w - 8f), Math.Max(1f, h - 8f), i.Rotated);
		Text((Transform)(object)_content, i.Name, x + 4f, y + 1f, w - 8f, 20f, 12);
		Text((Transform)(object)_content, (i.Count > 1) ? ("×" + i.Count) : "", x + 4f, y + h - 21f, w - 8f, 20f, 12).alignment = (TextAlignmentOptions)4100;
	}

	private static void Picture(HistoryItem item, float x, float y, float w, float h, bool rotate = false)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = Rect("ItemIcon", (Transform)(object)_content);
		Top(val, x, y, w, h);
		Image val2 = ((Component)val).gameObject.AddComponent<Image>();
		val2.preserveAspect = true;
		((Graphic)val2).raycastTarget = false;
		((Graphic)val2).color = new Color(0.3f, 0.32f, 0.29f, 0.2f);
		if (rotate)
		{
			val.pivot = new Vector2(0.5f, 0.5f);
			val.anchoredPosition = new Vector2(x + w / 2f, 0f - y - h / 2f);
			val.sizeDelta = new Vector2(h, w);
			((Transform)val).localEulerAngles = new Vector3(0f, 0f, 90f);
		}
		IconQueue.Enqueue((item, val2, _generation));
	}

	private static void TickIcon()
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		if (_iconTask != null)
		{
			if (!_iconTask.IsCompleted)
			{
				if (!(Time.unscaledTime < _iconDeadline))
				{
					_iconTask = null;
					_iconImage = null;
					_iconSample = null;
				}
				return;
			}
			if (!_iconTask.IsFaulted && !_iconTask.IsCanceled && (Object)(object)_iconImage != (Object)null && _iconGeneration == _generation)
			{
				Sprite result = _iconTask.Result;
				if ((Object)(object)result != (Object)null)
				{
					_iconImage.sprite = result;
					((Graphic)_iconImage).color = Color.white;
				}
			}
			_iconTask = null;
			_iconImage = null;
			_iconSample = null;
		}
		if (Time.unscaledTime < _nextIcon || IconQueue.Count == 0)
		{
			return;
		}
		_nextIcon = Time.unscaledTime + 0.035f;
		(HistoryItem, Image, int) tuple = IconQueue.Dequeue();
		if (tuple.Item3 != _generation || (Object)(object)tuple.Item2 == (Object)null)
		{
			return;
		}
		try
		{
			ItemFactory factory = Singleton<ItemFactory>.Instance;
			if (factory != null)
			{
				_iconSample = factory.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), tuple.Item1.Template, (UnparsedData)null);
				Mount(_iconSample, tuple.Item1.Id, 0);
				_iconImage = tuple.Item2;
				_iconGeneration = tuple.Item3;
				_iconDeadline = Time.unscaledTime + 8f;
				_iconTask = ItemViewFactory.GetItemSpriteAsync(_iconSample, 1);
			}
			void Mount(Item sample, string id, int depth)
			{
				if (depth <= 12 && _record != null)
				{
					CompoundItem val = sample.TryCast<CompoundItem>();
					if (val?.Slots != null)
					{
						foreach (HistoryItem child in _record.Items.Where((HistoryItem i) => i.Parent == id && !i.Storage))
						{
							Slot val2 = val.Slots.FirstOrDefault((Slot s) => s.ID == child.Slot);
							if (val2 != null)
							{
								Item sample2 = (val2.ContainedItem = factory.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), child.Template, (UnparsedData)null));
								Mount(sample2, child.Id, depth + 1);
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			_iconTask = null;
			_iconSample = null;
			Plugin.Logger.LogInfo((object)("历史物品图标不可用：" + tuple.Item1.Template + " " + ex.Message));
		}
	}

	static HistoryUi()
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		_shownDetail = "";
		_attachReason = "";
		_profile = "";
		_search = "";
		_pageNumber = 1;
		_detail = "概况";
		IconQueue = new Queue<(HistoryItem, Image, int)>();
		HeaderBlockers = new List<RectTransform>();
		HeaderVisuals = new Dictionary<IntPtr, GameObject>();
		Ink = new Color(0.77f, 0.76f, 0.69f);
		Muted = new Color(0.51f, 0.52f, 0.48f);
		Good = new Color(0.65f, 0.69f, 0.5f);
		Bad = new Color(0.72f, 0.48f, 0.42f);
		Surface = new Color(0.025f, 0.028f, 0.026f);
		Edge = new Color(0.27f, 0.28f, 0.25f);
		Accent = new Color(0.77f, 0.76f, 0.69f);
	}
}
