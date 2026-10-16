using System;
using System.IO;
using System.Reflection;
using CookingGrenades.Config;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

public class CookIndicator : MonoBehaviour
{
	private Canvas _canvas;

	private RectTransform _indicatorRect;

	private Image _indicatorImage;

	private static Sprite _spriteYellow;

	private static Sprite _spriteRed;

	private static Sprite _spriteGrey;

	private static bool _spritesLoaded;

	private ThrowWeap _throwWeap;

	private float _totalFuseTime;

	private float _cookingStartTime;

	private bool _isActive;

	private float _blinkTimer;

	private bool _visible = true;

	private const float BaseBlinkInterval = 0.5f;

	private const float MinBlinkInterval = 0.08f;

	private bool _inThrowAnimation;

	private float _throwAnimTimer;

	private Quaternion _startRotation;

	private Quaternion _targetRotation;

	private float _iconSizePx;

	private float ThrowAnimDuration => ConfigManager.CookIndicatorAnimDuration.Value;

	private static void LoadSprites()
	{
		if (!_spritesLoaded)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
			_spriteYellow = LoadSprite(executingAssembly, manifestResourceNames, "Yellow.png");
			_spriteRed = LoadSprite(executingAssembly, manifestResourceNames, "Red.png");
			_spriteGrey = LoadSprite(executingAssembly, manifestResourceNames, "Grey.png");
			_spritesLoaded = true;
			if ((Object)(object)_spriteYellow == (Object)null)
			{
				Plugin.log.LogWarning((object)"[CookIndicator] 未加载到 Yellow.png 图标");
			}
			if ((Object)(object)_spriteRed == (Object)null)
			{
				Plugin.log.LogWarning((object)"[CookIndicator] 未加载到 Red.png 图标");
			}
			if ((Object)(object)_spriteGrey == (Object)null)
			{
				Plugin.log.LogWarning((object)"[CookIndicator] 未加载到 Grey.png 图标");
			}
		}
	}

	private static Sprite LoadSprite(Assembly asm, string[] resourceNames, string suffix)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		string text = null;
		foreach (string text2 in resourceNames)
		{
			if (text2.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				text = text2;
				break;
			}
		}
		if (text == null)
		{
			return null;
		}
		using Stream stream = asm.GetManifestResourceStream(text);
		if (stream == null)
		{
			return null;
		}
		byte[] array = new byte[stream.Length];
		stream.Read(array, 0, array.Length);
		Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
		ImageConversion.LoadImage(val, array);
		val.Apply();
		Sprite result = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f));
		Plugin.log.LogInfo((object)$"[CookIndicator] 图标加载成功: {suffix} ({((Texture)val).width}x{((Texture)val).height})");
		return result;
	}

	public void Initialize(Transform grenadeTransform, ThrowWeap throwWeap, float cookingStartTime)
	{
		_throwWeap = throwWeap;
		_cookingStartTime = cookingStartTime;
		_totalFuseTime = throwWeap.GetExplDelay;
		_isActive = true;
		_inThrowAnimation = false;
		LoadSprites();
		CreateCanvasAndIcon();
		UpdateIconSize();
		Plugin.log.LogInfo((object)$"[CookIndicator] 初始化: 引信={_totalFuseTime:F2}s, 手雷={((Item)throwWeap).Name}");
	}

	private void CreateCanvasAndIcon()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject("CookIndicator_Canvas");
		Object.DontDestroyOnLoad((Object)(object)val);
		_canvas = val.AddComponent<Canvas>();
		_canvas.renderMode = (RenderMode)0;
		_canvas.sortingOrder = 32000;
		CanvasScaler obj = val.AddComponent<CanvasScaler>();
		obj.uiScaleMode = (ScaleMode)0;
		obj.scaleFactor = 1f;
		GameObject val2 = new GameObject("CookIndicator_Icon");
		val2.transform.SetParent(((Component)_canvas).transform, false);
		_indicatorImage = val2.AddComponent<Image>();
		_indicatorImage.sprite = _spriteYellow ?? _spriteGrey ?? _spriteRed;
		((Graphic)_indicatorImage).color = Color.white;
		((Graphic)_indicatorImage).raycastTarget = false;
		_indicatorRect = val2.GetComponent<RectTransform>();
		_indicatorRect.pivot = new Vector2(0.5f, 0.5f);
		RectTransform indicatorRect = _indicatorRect;
		RectTransform indicatorRect2 = _indicatorRect;
		Vector2 val3 = default(Vector2);
		((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
		indicatorRect2.anchorMax = val3;
		indicatorRect.anchorMin = val3;
	}

	private void UpdateIconSize()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Min(Screen.width, Screen.height);
		_iconSizePx = Mathf.Clamp(num * 0.08f * ConfigManager.CookIndicatorScale.Value, 20f, 200f);
		if ((Object)(object)_indicatorRect != (Object)null)
		{
			_indicatorRect.sizeDelta = new Vector2(_iconSizePx, _iconSizePx);
		}
	}

	private void LateUpdate()
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_canvas == (Object)null || (Object)(object)_indicatorRect == (Object)null)
		{
			return;
		}
		if (_inThrowAnimation)
		{
			UpdateThrowAnimation();
		}
		else
		{
			if (!_isActive || _throwWeap == null)
			{
				return;
			}
			float num = Time.time - _cookingStartTime;
			float num2 = _totalFuseTime - num;
			if (num2 <= 0f)
			{
				Hide();
				return;
			}
			float value = ConfigManager.CookIndicatorOffsetX.Value;
			float num3 = ConfigManager.CookIndicatorOffsetY.Value + (ConfigManager.CookIndicatorHeight.Value - 0.3f) * 50f;
			_indicatorRect.anchoredPosition = new Vector2(value, num3);
			((Transform)_indicatorRect).localRotation = Quaternion.identity;
			float num5;
			if (num2 > 2f)
			{
				float num4 = Mathf.Clamp01(num / Mathf.Max(0.1f, _totalFuseTime - 2f));
				num5 = Mathf.Lerp(0.5f, 0.25f, num4);
			}
			else
			{
				float num6 = Mathf.Clamp01(1f - num2 / 2f);
				num5 = Mathf.Lerp(0.25f, 0.08f, num6);
			}
			_blinkTimer += Time.deltaTime;
			if (_blinkTimer >= num5)
			{
				_blinkTimer = 0f;
				_visible = !_visible;
			}
			if (num2 > 4f)
			{
				_indicatorImage.sprite = _spriteGrey ?? _spriteYellow ?? _indicatorImage.sprite;
				((Graphic)_indicatorImage).color = Color.white;
				((Behaviour)_indicatorImage).enabled = true;
			}
			else if (num2 > 2f)
			{
				_indicatorImage.sprite = _spriteYellow ?? _indicatorImage.sprite;
				((Graphic)_indicatorImage).color = Color.white;
				((Behaviour)_indicatorImage).enabled = _visible;
			}
			else
			{
				_indicatorImage.sprite = _spriteRed ?? _indicatorImage.sprite;
				((Graphic)_indicatorImage).color = Color.white;
				((Behaviour)_indicatorImage).enabled = _visible;
			}
		}
	}

	public void PlayThrowAnimation()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_canvas == (Object)null) && !((Object)(object)_indicatorRect == (Object)null))
		{
			_isActive = false;
			_inThrowAnimation = true;
			_throwAnimTimer = 0f;
			Vector2 anchoredPosition = _indicatorRect.anchoredPosition;
			Vector2 val = -anchoredPosition;
			Vector2 sizeDelta = _indicatorRect.sizeDelta;
			_indicatorRect.pivot = new Vector2(0.5f - val.x / Mathf.Max(1f, sizeDelta.x), 0.5f - val.y / Mathf.Max(1f, sizeDelta.y));
			_indicatorRect.anchoredPosition = new Vector2(anchoredPosition.x + (0.5f - _indicatorRect.pivot.x) * sizeDelta.x, anchoredPosition.y + (0.5f - _indicatorRect.pivot.y) * sizeDelta.y);
			_startRotation = ((Transform)_indicatorRect).localRotation;
			_targetRotation = _startRotation * Quaternion.Euler(0f, 0f, 45f);
		}
	}

	private void UpdateThrowAnimation()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		_throwAnimTimer += Time.deltaTime;
		float num = Mathf.Clamp01(_throwAnimTimer / ThrowAnimDuration);
		((Transform)_indicatorRect).localRotation = Quaternion.Slerp(_startRotation, _targetRotation, num);
		Color color = ((Graphic)_indicatorImage).color;
		color.a = Mathf.Lerp(1f, 0f, num);
		((Graphic)_indicatorImage).color = color;
		if (num >= 1f)
		{
			Hide();
		}
	}

	public void Hide()
	{
		_isActive = false;
		_inThrowAnimation = false;
		if ((Object)(object)_canvas != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_canvas).gameObject);
			_canvas = null;
			_indicatorRect = null;
			_indicatorImage = null;
		}
	}

	private void OnDestroy()
	{
		Hide();
	}
}
