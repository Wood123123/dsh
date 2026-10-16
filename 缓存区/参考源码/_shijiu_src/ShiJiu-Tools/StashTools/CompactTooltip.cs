using System;
using System.Collections.Generic;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class CompactTooltip
{
	private static SimpleTooltip? _owner;

	private static RectTransform? _root;

	private static RectTransform? _parent;

	private static TextMeshProUGUI? _text;

	private static CanvasGroup? _nativeGroup;

	private static CanvasGroup? _panelGroup;

	private static float _alpha;

	private static float _showAt;

	private static bool _raycasts;

	private static bool _nativeHidden;

	private static bool _ready;

	private static bool _failed;

	private static string _last = "";

	private static string _pending = "";

	private static readonly HashSet<string> Warnings = new HashSet<string>();

	private static bool _loggedReady;

	private static Canvas? _canvas;

	private static Vector2 _lastPointer;

	private static bool _positioned;

	private static int _screenWidth;

	private static int _screenHeight;

	private static bool _nameOnly;

	private static RectTransform MakeRect(string name, Transform parent)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = new GameObject(name, new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)component).SetParent(parent, false);
		((Transform)component).localScale = Vector3.one;
		return component;
	}

	internal static void Show(SimpleTooltip owner, string text, bool nameOnly = false)
	{
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_037f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		//IL_039f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = ((Component)((Component)owner).transform.parent).GetComponent<RectTransform>();
		if ((Object)(object)component == (Object)null)
		{
			throw new InvalidOperationException("提示框父级不支持界面坐标");
		}
		bool flag = _owner?.Identity() != owner.Identity();
		if (flag)
		{
			Close();
		}
		if ((Object)(object)_root == (Object)null || _parent?.Identity() != component.Identity())
		{
			Dispose();
			_parent = component;
			_root = MakeRect("StashToolsCompactTooltip", (Transform)(object)_parent);
			_panelGroup = ((Component)_root).gameObject.AddComponent<CanvasGroup>();
			_panelGroup.alpha = 0f;
			_panelGroup.blocksRaycasts = false;
			_panelGroup.interactable = false;
			((Component)_root).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			Vector2 val = (_root.anchorMin = (_root.anchorMax = _parent.pivot));
			_root.pivot = new Vector2(0f, 1f);
			Image obj = ((Component)_root).gameObject.AddComponent<Image>();
			((Graphic)obj).color = new Color(0.25f, 0.27f, 0.26f, 1f);
			((Graphic)obj).raycastTarget = false;
			RectTransform obj2 = MakeRect("Background", (Transform)(object)_root);
			obj2.anchorMin = Vector2.zero;
			obj2.anchorMax = Vector2.one;
			obj2.offsetMin = new Vector2(1f, 1f);
			obj2.offsetMax = new Vector2(-1f, -1f);
			Image obj3 = ((Component)obj2).gameObject.AddComponent<Image>();
			((Graphic)obj3).color = new Color(0f, 0f, 0f, 0.98f);
			((Graphic)obj3).raycastTarget = false;
			_text = ((Component)MakeRect("StashToolsTooltipText", (Transform)(object)_root)).gameObject.AddComponent<TextMeshProUGUI>();
			((TMP_Text)_text).font = ((TMP_Text)owner._label).font;
			((TMP_Text)_text).fontSharedMaterial = ((TMP_Text)owner._label).fontSharedMaterial;
			((TMP_Text)_text).fontSize = ((TMP_Text)owner._label).fontSize;
			((TMP_Text)_text).fontStyle = ((TMP_Text)owner._label).fontStyle;
			((TMP_Text)_text).characterSpacing = ((TMP_Text)owner._label).characterSpacing;
			((TMP_Text)_text).wordSpacing = ((TMP_Text)owner._label).wordSpacing;
			((Graphic)_text).color = ((Graphic)owner._label).color;
			((TMP_Text)_text).spriteAsset = ((TMP_Text)owner._label).spriteAsset;
			((Graphic)_text).raycastTarget = false;
			((TMP_Text)_text).enableAutoSizing = false;
			((TMP_Text)_text).enableWordWrapping = false;
			((TMP_Text)_text).overflowMode = (TextOverflowModes)0;
			((TMP_Text)_text).richText = true;
			((TMP_Text)_text).margin = Vector4.zero;
			((TMP_Text)_text).lineSpacing = 0f;
			((TMP_Text)_text).paragraphSpacing = 0f;
			((TMP_Text)_text).alignment = (TextAlignmentOptions)257;
			Color color = ((Graphic)_text).color;
			((Graphic)_text).color = new Color(color.r, color.g, color.b, 1f);
			RectTransform rectTransform = ((TMP_Text)_text).rectTransform;
			((Transform)rectTransform).localScale = Vector3.one;
			((Transform)rectTransform).localRotation = Quaternion.identity;
			((Vector2)(ref val))._002Ector(0f, 1f);
			rectTransform.anchorMax = val;
			rectTransform.anchorMin = val;
			rectTransform.pivot = new Vector2(0f, 1f);
			rectTransform.anchoredPosition = new Vector2(7f, -5f);
			_canvas = ((Component)_root).GetComponentInParent<Canvas>();
			flag = true;
		}
		if (flag)
		{
			_owner = owner;
			_failed = false;
			_positioned = false;
			IntPtr? intPtr = ((TMP_Text)_text).font?.Identity();
			IntPtr? intPtr2 = ((TMP_Text)owner._label).font?.Identity();
			if (intPtr.HasValue != intPtr2.HasValue || (intPtr.HasValue && intPtr.GetValueOrDefault() != intPtr2.GetValueOrDefault()) || ((TMP_Text)_text).fontSharedMaterial?.Identity() != ((TMP_Text)owner._label).fontSharedMaterial?.Identity())
			{
				((TMP_Text)_text).font = ((TMP_Text)owner._label).font;
				((TMP_Text)_text).fontSharedMaterial = ((TMP_Text)owner._label).fontSharedMaterial;
				_ready = false;
			}
			_showAt = Time.unscaledTime + Plugin.HoverDelay.Value;
			((Transform)_root).SetAsLastSibling();
		}
		if (_nameOnly != nameOnly)
		{
			_nameOnly = nameOnly;
			_ready = false;
		}
		_pending = text;
		Tick();
	}

	private static void Measure()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		bool ready = _ready;
		_ready = false;
		if ((Object)(object)_text == (Object)null || (Object)(object)_root == (Object)null || string.IsNullOrWhiteSpace(_pending))
		{
			throw new InvalidOperationException("提示文字为空");
		}
		if ((Object)(object)((TMP_Text)_text).font == (Object)null || !float.IsFinite(((TMP_Text)_text).fontSize) || ((TMP_Text)_text).fontSize <= 0f)
		{
			throw new InvalidOperationException("提示字体未就绪");
		}
		((TMP_Text)_text).text = _pending;
		((TMP_Text)_text).rectTransform.sizeDelta = new Vector2(32767f, 32767f);
		if (!ready)
		{
			((TMP_Text)_text).ForceMeshUpdate(false, true);
		}
		Vector2 preferredValues = ((TMP_Text)_text).GetPreferredValues();
		if (!float.IsFinite(preferredValues.x) || !float.IsFinite(preferredValues.y) || preferredValues.x <= 1f || preferredValues.y <= 1f)
		{
			throw new InvalidOperationException($"提示文字尺寸无效：{preferredValues.x}×{preferredValues.y}");
		}
		float num = Math.Max(0.1f, ((Transform)_root).lossyScale.x);
		float num2 = (float)Screen.width / num - 22f;
		if (preferredValues.x + 2f > num2)
		{
			TextMeshProUGUI? text = _text;
			((TMP_Text)text).fontSize = ((TMP_Text)text).fontSize * (num2 / (preferredValues.x + 2f));
			preferredValues = ((TMP_Text)_text).GetPreferredValues();
		}
		float num3 = Mathf.Ceil(preferredValues.x) + 2f;
		float num4 = Mathf.Ceil(preferredValues.y) + 2f;
		float num5 = (_nameOnly ? 2 : 5);
		((TMP_Text)_text).rectTransform.anchoredPosition = new Vector2(7f, 0f - num5);
		((TMP_Text)_text).rectTransform.sizeDelta = new Vector2(num3, num4);
		_root.sizeDelta = new Vector2(num3 + 14f, num4 + num5 * 2f);
		((TMP_Text)_text).ForceMeshUpdate(false, true);
		TMP_TextInfo textInfo = ((TMP_Text)_text).textInfo;
		bool flag = false;
		if (textInfo != null && textInfo.characterCount > 0 && textInfo.meshInfo != null)
		{
			for (int i = 0; i < textInfo.meshInfo.Length; i++)
			{
				if (textInfo.meshInfo[i].vertexCount > 0)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			throw new InvalidOperationException("提示文字未生成可见字形");
		}
		_last = _pending;
		_ready = true;
		_screenWidth = Screen.width;
		_screenHeight = Screen.height;
		_positioned = false;
		if (!_loggedReady)
		{
			_loggedReady = true;
			Plugin.Logger.LogInfo((object)$"紧凑悬浮框文字已生成：字号={((TMP_Text)_text).fontSize}，尺寸={num3}×{num4}，字符={textInfo.characterCount}");
		}
	}

	private static void RestoreNative()
	{
		if (_nativeHidden && (Object)(object)_nativeGroup != (Object)null)
		{
			_nativeGroup.alpha = _alpha;
			_nativeGroup.blocksRaycasts = _raycasts;
		}
		_nativeHidden = false;
	}

	internal static void Tick()
	{
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_root == (Object)null || (Object)(object)_owner == (Object)null || (Object)(object)_parent == (Object)null || (Object)(object)_panelGroup == (Object)null || _failed)
		{
			return;
		}
		if (!((Tooltip)_owner).Displayed || !((Component)_owner).gameObject.activeInHierarchy || !(Time.unscaledTime >= _showAt))
		{
			_panelGroup.alpha = 0f;
			RestoreNative();
		}
		else
		{
			if (!((Component)_root).gameObject.activeInHierarchy)
			{
				return;
			}
			try
			{
				if (!_ready || _last != _pending || _screenWidth != Screen.width || _screenHeight != Screen.height)
				{
					((TMP_Text)_text).fontSize = ((TMP_Text)_owner._label).fontSize;
					Measure();
				}
			}
			catch (Exception ex)
			{
				_failed = true;
				_panelGroup.alpha = 0f;
				RestoreNative();
				if (Warnings.Add(ex.Message))
				{
					Plugin.Logger.LogWarning((object)("紧凑提示框回退到游戏原提示框（保留价格文字）：" + ex));
				}
				return;
			}
			float num = _root.sizeDelta.x * ((Transform)_root).lossyScale.x;
			float num2 = _root.sizeDelta.y * ((Transform)_root).lossyScale.y;
			Vector3 mousePosition = Input.mousePosition;
			if (!_positioned || _lastPointer.x != mousePosition.x || _lastPointer.y != mousePosition.y)
			{
				Vector2 val = default(Vector2);
				((Vector2)(ref val))._002Ector(Mathf.Clamp(mousePosition.x + 12f, 4f, Math.Max(4f, (float)Screen.width - num - 4f)), Mathf.Clamp(mousePosition.y - 12f, num2 + 4f, Math.Max(num2 + 4f, Screen.height - 4)));
				Camera val2 = (((Object)(object)_canvas != (Object)null && (int)_canvas.renderMode != 0) ? _canvas.worldCamera : null);
				Vector2 anchoredPosition = default(Vector2);
				if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, val, val2, ref anchoredPosition))
				{
					_root.anchoredPosition = anchoredPosition;
				}
				_lastPointer = Vector2.op_Implicit(mousePosition);
				_positioned = true;
			}
			if (!_nativeHidden)
			{
				_nativeGroup = ((Component)_owner).gameObject.GetComponent<CanvasGroup>() ?? ((Component)_owner).gameObject.AddComponent<CanvasGroup>();
				_alpha = _nativeGroup.alpha;
				_raycasts = _nativeGroup.blocksRaycasts;
				_nativeGroup.alpha = 0f;
				_nativeGroup.blocksRaycasts = false;
				_nativeHidden = true;
			}
			if (_panelGroup.alpha != 1f)
			{
				_panelGroup.alpha = 1f;
			}
		}
	}

	internal static void Close()
	{
		RestoreNative();
		if ((Object)(object)_panelGroup != (Object)null)
		{
			_panelGroup.alpha = 0f;
		}
		_nativeGroup = null;
		_owner = null;
		_pending = "";
		_failed = false;
		_positioned = false;
	}

	internal static void Dispose()
	{
		Close();
		if ((Object)(object)_root != (Object)null)
		{
			((Component)_root).gameObject.SetActive(false);
			Object.Destroy((Object)(object)((Component)_root).gameObject);
		}
		_nativeGroup = null;
		_panelGroup = null;
		_owner = null;
		_root = null;
		_parent = null;
		_text = null;
		_canvas = null;
		_last = "";
		_pending = "";
		_ready = false;
		_failed = false;
	}
}
