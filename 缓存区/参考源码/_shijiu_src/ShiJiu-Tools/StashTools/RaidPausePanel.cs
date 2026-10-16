using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class RaidPausePanel
{
	private static GameObject? _root;

	private static RectTransform? _box;

	private static Image? _mask;

	private static TextMeshProUGUI? _text;

	private static string _last = "";

	private static bool _blocking;

	private static bool _placed;

	private static float _retryAt;

	private static RectTransform Rect(string name, Transform? parent)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = new GameObject(name, new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		if ((Object)(object)parent != (Object)null)
		{
			((Transform)component).SetParent(parent, false);
		}
		((Transform)component).localScale = Vector3.one;
		return component;
	}

	private static void Fill(RectTransform rect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		Vector2 offsetMin = (rect.offsetMax = Vector2.zero);
		rect.offsetMin = offsetMin;
	}

	internal static void Show(string message, bool paused)
	{
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root == (Object)null)
		{
			if (Time.unscaledTime < _retryAt)
			{
				return;
			}
			_retryAt = Time.unscaledTime + 1f;
			AbstractGame obj = (Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				GameUI gameUi = obj.GameUi;
				obj2 = ((gameUi != null) ? ((Component)gameUi).GetComponentInChildren<TextMeshProUGUI>(true) : null);
			}
			TextMeshProUGUI val = (TextMeshProUGUI)obj2;
			if ((Object)(object)((val != null) ? ((TMP_Text)val).font : null) == (Object)null)
			{
				val = Object.FindObjectsOfType<TextMeshProUGUI>(true).FirstOrDefault((TextMeshProUGUI text) => (Object)(object)((text != null) ? ((TMP_Text)text).font : null) != (Object)null);
			}
			if ((Object)(object)((val != null) ? ((TMP_Text)val).font : null) == (Object)null)
			{
				return;
			}
			_root = ((Component)Rect("ShiJiuRaidPause", null)).gameObject;
			Canvas obj3 = _root.AddComponent<Canvas>();
			obj3.renderMode = (RenderMode)0;
			obj3.sortingOrder = 30000;
			CanvasScaler obj4 = _root.AddComponent<CanvasScaler>();
			obj4.uiScaleMode = (ScaleMode)1;
			obj4.referenceResolution = new Vector2(1920f, 1080f);
			obj4.matchWidthOrHeight = 0.5f;
			_root.AddComponent<GraphicRaycaster>();
			RectTransform obj5 = Rect("PauseInputMask", _root.transform);
			Fill(obj5);
			_mask = ((Component)obj5).gameObject.AddComponent<Image>();
			((Graphic)_mask).color = new Color(0f, 0f, 0f, 0.22f);
			_box = Rect("PauseNotice", _root.transform);
			_box.sizeDelta = new Vector2(640f, 105f);
			Image obj6 = ((Component)_box).gameObject.AddComponent<Image>();
			((Graphic)obj6).color = new Color(0.045f, 0.05f, 0.04f, 0.88f);
			((Graphic)obj6).raycastTarget = false;
			RectTransform obj7 = Rect("Message", (Transform?)(object)_box);
			Fill(obj7);
			obj7.offsetMin = new Vector2(16f, 10f);
			obj7.offsetMax = new Vector2(-16f, -10f);
			_text = ((Component)obj7).gameObject.AddComponent<TextMeshProUGUI>();
			((TMP_Text)_text).font = ((TMP_Text)val).font;
			((TMP_Text)_text).fontSharedMaterial = ((TMP_Text)val).fontSharedMaterial;
			((TMP_Text)_text).fontSize = 27f;
			((TMP_Text)_text).fontStyle = (FontStyles)1;
			((Graphic)_text).color = new Color(0.83f, 0.83f, 0.77f);
			((TMP_Text)_text).alignment = (TextAlignmentOptions)514;
			((TMP_Text)_text).enableWordWrapping = false;
			((TMP_Text)_text).richText = true;
			((Graphic)_text).raycastTarget = false;
			((TMP_Text)_text).overflowMode = (TextOverflowModes)1;
		}
		if (!_root.activeSelf)
		{
			_root.SetActive(true);
		}
		if (_last != message)
		{
			_last = message;
			((TMP_Text)_text).text = message;
		}
		if (!_placed || _blocking != paused)
		{
			_placed = true;
			_blocking = paused;
			((Component)_mask).gameObject.SetActive(paused);
			((Graphic)_mask).raycastTarget = paused;
			RectTransform? box = _box;
			RectTransform? box2 = _box;
			RectTransform? box3 = _box;
			Vector2 val2 = default(Vector2);
			((Vector2)(ref val2))._002Ector(0.5f, paused ? 0.5f : 1f);
			box3.pivot = val2;
			Vector2 anchorMin = (box2.anchorMax = val2);
			box.anchorMin = anchorMin;
			_box.anchoredPosition = new Vector2(0f, (float)((!paused) ? (-100) : 0));
		}
	}

	internal static void Hide()
	{
		if ((Object)(object)_root != (Object)null && _root.activeSelf)
		{
			_root.SetActive(false);
		}
	}

	internal static void Dispose()
	{
		if ((Object)(object)_root != (Object)null)
		{
			Object.Destroy((Object)(object)_root);
		}
		_root = null;
		_box = null;
		_mask = null;
		_text = null;
		_last = "";
		_blocking = false;
		_placed = false;
		_retryAt = 0f;
	}
}
