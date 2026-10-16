using System;
using System.Collections.Generic;
using System.Diagnostics;
using Comfort.Common;
using EFT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

public abstract class WheelBase<T> : MonoBehaviour where T : WheelBase<T>
{
	protected Player _player;

	protected int _selectedIndex = -1;

	private Canvas _canvas;

	private TextMeshProUGUI _centerLabel;

	private GameWorld _cachedGameWorld;

	private GameObject _highlightObj;

	private GameObject _sectorHighlightObj;

	private readonly List<Graphic> _slotGraphics = new List<Graphic>();

	private readonly List<GameObject> _dividerObjects = new List<GameObject>();

	private float _currentRotZ;

	private float _targetRotZ;

	private float _sectorCurrentRotZ;

	private float _sectorTargetRotZ;

	private float _raidStartTime = -1f;

	private float _keyHoldStartTime = -1f;

	private bool _keyWasHeld;

	private bool _wheelSelectionLock;

	private TextMeshProUGUI _cancelLabel;

	private bool _prewarmPending;

	private float _prewarmAt = -1f;

	private const float CenterDeadZone = 35f;

	private const float RaidDelay = 3f;

	private const float PrewarmDelay = 0.35f;

	private const float HoldDuration = 0.5f;

	private const int RingTexSize = 700;

	private const float CircleInnerR = 160f;

	private const float CircleOuterR = 163f;

	private const float DividerInnerR = 163f;

	private const float DividerOuterR = 366f;

	private const float RingDisplaySize = 620f;

	private const float HighlightSmoothing = 15f;

	public static T Instance { get; protected set; }

	public bool IsOpen { get; protected set; }

	protected abstract string WheelLogName { get; }

	protected abstract bool EnabledByConfig { get; }

	protected abstract KeyCode HoldKey { get; }

	protected abstract int DisplayCount { get; }

	protected static string GetNoSelectionText()
	{
		try
		{
			LocalizationManager instance = LocalizationManager.Instance;
			string text = ((instance != null) ? instance.Culture : null);
			if (!string.IsNullOrEmpty(text) && text.StartsWith("ch", StringComparison.OrdinalIgnoreCase))
			{
				return "无";
			}
		}
		catch
		{
		}
		return "None";
	}

	public static TW GetOrCreateInstance<TW>() where TW : WheelBase<TW>
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		if ((Object)(object)WheelBase<TW>.Instance != (Object)null)
		{
			return WheelBase<TW>.Instance;
		}
		GameObject val = new GameObject(typeof(TW).Name);
		Object.DontDestroyOnLoad((Object)val);
		return val.AddComponent<TW>();
	}

	protected abstract bool IsOtherWheelOpen();

	protected abstract void ScanItems();

	protected abstract string GetSlotName(int index);

	protected abstract string GetSlotFullName(int index);

	protected abstract int GetSlotCount(int index);

	protected abstract void LoadSlotIcon(int index, Image iconImage, TextMeshProUGUI fallbackText);

	protected abstract void OnSelect(int index);

	protected virtual int GetInitialSelectedIndex()
	{
		return -1;
	}

	protected virtual void Awake()
	{
		if ((Object)(object)Instance != (Object)null)
		{
			Object.Destroy((Object)(object)this);
			return;
		}
		Instance = (T)this;
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		Plugin.log.LogInfo((object)("[" + WheelLogName + "] 已初始化"));
	}

	private void Update()
	{
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		GameWorld val = (Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null);
		if (_cachedGameWorld != val)
		{
			_cachedGameWorld = val;
		}
		GameWorld cachedGameWorld = _cachedGameWorld;
		if ((Object)(object)cachedGameWorld?.MainPlayer == (Object)null)
		{
			bool num = _raidStartTime >= 0f;
			_raidStartTime = -1f;
			_prewarmPending = false;
			_prewarmAt = -1f;
			if (IsOpen)
			{
				ForceClose();
			}
			if (num)
			{
				WheelTexCache.Reset();
			}
			return;
		}
		_player = cachedGameWorld.MainPlayer;
		if (!_player.HealthController.IsAlive)
		{
			if (IsOpen)
			{
				ForceClose();
			}
			return;
		}
		if (_raidStartTime < 0f)
		{
			_raidStartTime = Time.time;
			_prewarmPending = true;
			_prewarmAt = Time.time + 0.35f;
			Plugin.log.LogInfo((object)$"[{WheelLogName}] 战局开始，{3f}秒后可使用轮盘");
		}
		if (GrenadeCookingManager.Timer.IsCooking)
		{
			if (IsOpen)
			{
				ForceClose();
			}
		}
		else if (IsOpen)
		{
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			if (!Input.GetKey(HoldKey))
			{
				CloseAndApply();
			}
		}
		else
		{
			if (!EnabledByConfig || IsOtherWheelOpen())
			{
				return;
			}
			if (_prewarmPending && Time.time >= _prewarmAt)
			{
				TryPreWarm();
			}
			if (!(Time.time - _raidStartTime < 3f))
			{
				KeyCode holdKey = HoldKey;
				bool keyDown = Input.GetKeyDown(holdKey);
				bool key = Input.GetKey(holdKey);
				if (keyDown)
				{
					_keyHoldStartTime = Time.time;
					_keyWasHeld = false;
					Plugin.log.LogInfo((object)$"[{WheelLogName}] 按键 {holdKey} 按下，开始计时长按");
				}
				if (key && !_keyWasHeld && _keyHoldStartTime > 0f && Time.time - _keyHoldStartTime >= 0.5f)
				{
					_keyWasHeld = true;
					_keyHoldStartTime = -1f;
					Plugin.log.LogInfo((object)$"[{WheelLogName}] 按键 {holdKey} 已长按{0.5f}秒，呼出轮盘");
					OpenWheel();
				}
				if (!key)
				{
					_keyHoldStartTime = -1f;
				}
			}
		}
	}

	private void LateUpdate()
	{
		if (IsOpen)
		{
			Cursor.lockState = (CursorLockMode)0;
			Cursor.visible = true;
			UpdateSelection();
		}
	}

	public void OpenWheel()
	{
		if (IsOpen || !EnabledByConfig || IsOtherWheelOpen())
		{
			return;
		}
		_player = Singleton<GameWorld>.Instance?.MainPlayer;
		if ((Object)(object)_player == (Object)null)
		{
			return;
		}
		if (_player.InventoryController == null)
		{
			Plugin.log.LogWarning((object)("[" + WheelLogName + "] InventoryController 为空"));
			return;
		}
		Plugin.log.LogInfo((object)("[" + WheelLogName + "] 扫描物品..."));
		ScanItems();
		IsOpen = true;
		_selectedIndex = -1;
		_wheelSelectionLock = false;
		CreateWheelUI();
		int initialSelectedIndex = GetInitialSelectedIndex();
		if (initialSelectedIndex >= 0 && initialSelectedIndex < DisplayCount)
		{
			_selectedIndex = initialSelectedIndex;
			float num = 360f / (float)(DisplayCount + 1);
			_targetRotZ = (_currentRotZ = (0f - num) * (float)initialSelectedIndex);
			_wheelSelectionLock = true;
			Plugin.log.LogInfo((object)("[" + WheelLogName + "] 初始选中当前手雷: " + GetSlotName(initialSelectedIndex)));
		}
		Plugin.log.LogInfo((object)$"[{WheelLogName}] 轮盘已开启 ({DisplayCount} 种)");
	}

	public void CloseAndApply()
	{
		if (IsOpen)
		{
			IsOpen = false;
			DestroyWheelUI();
			Cursor.lockState = (CursorLockMode)1;
			Cursor.visible = false;
			if (_selectedIndex >= 0 && _selectedIndex < DisplayCount)
			{
				OnSelect(_selectedIndex);
			}
		}
	}

	public void ForceClose()
	{
		IsOpen = false;
		DestroyWheelUI();
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
	}

	private void TryPreWarm()
	{
		_prewarmPending = false;
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			ScanItems();
			int displayCount = DisplayCount;
			if (displayCount <= 0)
			{
				Plugin.log.LogInfo((object)$"[{WheelLogName}] 预热：无可用物品（{displayCount} 组），跳过纹理生成");
				return;
			}
			int num = displayCount + 1;
			float num2 = 360f / (float)num;
			WheelTexCache.Circle(700, 160f, 163f);
			WheelTexCache.Divider(700, 163f, 366f, 2f);
			WheelTexCache.Sector(num, 700, 161f, 406f, num2);
			WheelTexCache.HighlightArc(num, 700, 150f, 157f, num2 * 0.75f);
			stopwatch.Stop();
			Plugin.log.LogInfo((object)$"[{WheelLogName}] 预热完成：{displayCount} 组 → {num} 扇区，耗时 {stopwatch.Elapsed.TotalMilliseconds:0}ms");
		}
		catch (Exception ex)
		{
			Plugin.log.LogWarning((object)("[" + WheelLogName + "] 预热失败: " + ex.Message));
		}
	}

	private void CreateWheelUI()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Expected O, but got Unknown
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Expected O, but got Unknown
		//IL_033a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_0387: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Expected O, but got Unknown
		//IL_0411: Unknown result type (might be due to invalid IL or missing references)
		//IL_043b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0442: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_045e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0545: Unknown result type (might be due to invalid IL or missing references)
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_055e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0586: Unknown result type (might be due to invalid IL or missing references)
		//IL_058b: Unknown result type (might be due to invalid IL or missing references)
		//IL_059d: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_060f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0616: Unknown result type (might be due to invalid IL or missing references)
		//IL_061e: Unknown result type (might be due to invalid IL or missing references)
		//IL_062a: Unknown result type (might be due to invalid IL or missing references)
		//IL_062f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0643: Unknown result type (might be due to invalid IL or missing references)
		//IL_066b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0670: Unknown result type (might be due to invalid IL or missing references)
		//IL_0682: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0707: Unknown result type (might be due to invalid IL or missing references)
		//IL_0713: Unknown result type (might be due to invalid IL or missing references)
		//IL_0718: Unknown result type (might be due to invalid IL or missing references)
		//IL_072c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0789: Unknown result type (might be due to invalid IL or missing references)
		//IL_0790: Expected O, but got Unknown
		//IL_0801: Unknown result type (might be due to invalid IL or missing references)
		//IL_0831: Unknown result type (might be due to invalid IL or missing references)
		//IL_0838: Unknown result type (might be due to invalid IL or missing references)
		//IL_0840: Unknown result type (might be due to invalid IL or missing references)
		//IL_0851: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(WheelLogName + "_Canvas");
		Object.DontDestroyOnLoad((Object)(object)val);
		_canvas = val.AddComponent<Canvas>();
		_canvas.renderMode = (RenderMode)0;
		_canvas.sortingOrder = 31000;
		val.AddComponent<CanvasScaler>();
		int displayCount = DisplayCount;
		GameObject val2 = new GameObject("CenterLabel");
		val2.transform.SetParent(val.transform, false);
		_centerLabel = val2.AddComponent<TextMeshProUGUI>();
		((TMP_Text)_centerLabel).text = GetNoSelectionText();
		((TMP_Text)_centerLabel).fontSize = 24f;
		((TMP_Text)_centerLabel).alignment = (TextAlignmentOptions)514;
		((Graphic)_centerLabel).color = new Color(1f, 1f, 1f, 0.6f);
		((Graphic)_centerLabel).raycastTarget = false;
		RectTransform component = val2.GetComponent<RectTransform>();
		Vector2 val3 = default(Vector2);
		((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
		component.anchorMax = val3;
		component.anchorMin = val3;
		component.anchoredPosition = Vector2.zero;
		component.sizeDelta = new Vector2(120f, 36f);
		if (displayCount != 0)
		{
			int num = displayCount + 1;
			float num2 = 31f / 35f;
			Sprite sprite = WheelTexCache.Circle(700, 160f, 163f);
			GameObject val4 = new GameObject("CircleRing");
			val4.transform.SetParent(val.transform, false);
			Image obj = val4.AddComponent<Image>();
			obj.sprite = sprite;
			((Graphic)obj).color = Color.white;
			((Graphic)obj).raycastTarget = false;
			RectTransform component2 = val4.GetComponent<RectTransform>();
			((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
			component2.anchorMax = val3;
			component2.anchorMin = val3;
			component2.anchoredPosition = Vector2.zero;
			component2.sizeDelta = new Vector2(620f, 620f);
			Sprite sprite2 = WheelTexCache.Divider(700, 163f, 366f, 2f);
			_dividerObjects.Clear();
			for (int i = 0; i < num; i++)
			{
				GameObject val5 = new GameObject($"Divider_{i}");
				val5.transform.SetParent(val.transform, false);
				Image obj2 = val5.AddComponent<Image>();
				obj2.sprite = sprite2;
				((Graphic)obj2).color = Color.white;
				((Graphic)obj2).raycastTarget = false;
				RectTransform component3 = val5.GetComponent<RectTransform>();
				((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
				component3.anchorMax = val3;
				component3.anchorMin = val3;
				component3.anchoredPosition = Vector2.zero;
				component3.sizeDelta = new Vector2(620f, 620f);
				float num3 = 90f - 360f / (float)num * ((float)i + 0.5f) - 90f;
				((Transform)component3).localRotation = Quaternion.Euler(0f, 0f, num3);
				val5.SetActive(false);
				_dividerObjects.Add(val5);
			}
			float arcDeg = 360f / (float)num;
			float innerR = 161f;
			float outerR = 406f;
			Sprite sprite3 = WheelTexCache.Sector(num, 700, innerR, outerR, arcDeg);
			GameObject val6 = new GameObject("SectorHighlight");
			val6.transform.SetParent(val.transform, false);
			Image obj3 = val6.AddComponent<Image>();
			obj3.sprite = sprite3;
			((Graphic)obj3).color = Color.white;
			((Graphic)obj3).raycastTarget = false;
			RectTransform component4 = val6.GetComponent<RectTransform>();
			((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
			component4.anchorMax = val3;
			component4.anchorMin = val3;
			component4.anchoredPosition = Vector2.zero;
			component4.sizeDelta = new Vector2(620f, 620f);
			_sectorHighlightObj = val6;
			_sectorHighlightObj.SetActive(false);
			float arcDeg2 = 360f / (float)num * 0.75f;
			Sprite sprite4 = WheelTexCache.HighlightArc(num, 700, 150f, 157f, arcDeg2);
			GameObject val7 = new GameObject("Highlight");
			val7.transform.SetParent(val.transform, false);
			Image obj4 = val7.AddComponent<Image>();
			obj4.sprite = sprite4;
			((Graphic)obj4).color = new Color(1f, 1f, 1f, 0.85f);
			((Graphic)obj4).raycastTarget = false;
			RectTransform component5 = val7.GetComponent<RectTransform>();
			((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
			component5.anchorMax = val3;
			component5.anchorMin = val3;
			component5.anchoredPosition = Vector2.zero;
			component5.sizeDelta = new Vector2(620f, 620f);
			_highlightObj = val7;
			_highlightObj.SetActive(false);
			float num4 = 264.5f * num2;
			Vector2 val8 = default(Vector2);
			for (int j = 0; j < displayCount; j++)
			{
				float num5 = (90f - 360f / (float)num * (float)j) * ((float)Math.PI / 180f);
				((Vector2)(ref val8))._002Ector(Mathf.Cos(num5) * num4, Mathf.Sin(num5) * num4);
				GameObject val9 = new GameObject($"Icon_{j}");
				val9.transform.SetParent(val.transform, false);
				Image val10 = val9.AddComponent<Image>();
				((Graphic)val10).color = new Color(0.6f, 0.6f, 0.6f);
				((Graphic)val10).raycastTarget = false;
				val10.preserveAspect = true;
				((Behaviour)val10).enabled = false;
				RectTransform component6 = val9.GetComponent<RectTransform>();
				((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
				component6.anchorMax = val3;
				component6.anchorMin = val3;
				component6.anchoredPosition = val8;
				component6.sizeDelta = new Vector2(112f, 112f);
				_slotGraphics.Add((Graphic)(object)val10);
				GameObject val11 = new GameObject($"Name_{j}");
				val11.transform.SetParent(val.transform, false);
				TextMeshProUGUI val12 = val11.AddComponent<TextMeshProUGUI>();
				((TMP_Text)val12).text = GetSlotName(j);
				((TMP_Text)val12).fontSize = 20f;
				((TMP_Text)val12).fontStyle = (FontStyles)1;
				((TMP_Text)val12).alignment = (TextAlignmentOptions)514;
				((Graphic)val12).color = new Color(0.6f, 0.6f, 0.6f);
				((Graphic)val12).raycastTarget = false;
				RectTransform component7 = val11.GetComponent<RectTransform>();
				((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
				component7.anchorMax = val3;
				component7.anchorMin = val3;
				component7.anchoredPosition = val8 + new Vector2(0f, 4f);
				component7.sizeDelta = new Vector2(100f, 28f);
				_slotGraphics.Add((Graphic)(object)val12);
				GameObject val13 = new GameObject($"Count_{j}");
				val13.transform.SetParent(val.transform, false);
				TextMeshProUGUI obj5 = val13.AddComponent<TextMeshProUGUI>();
				((TMP_Text)obj5).text = $"{GetSlotCount(j)}";
				((TMP_Text)obj5).fontSize = 24f;
				((TMP_Text)obj5).alignment = (TextAlignmentOptions)514;
				((Graphic)obj5).color = new Color(1f, 1f, 1f, 0.5f);
				((Graphic)obj5).raycastTarget = false;
				RectTransform component8 = val13.GetComponent<RectTransform>();
				((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
				component8.anchorMax = val3;
				component8.anchorMin = val3;
				component8.anchoredPosition = val8 + new Vector2(0f, -42f);
				component8.sizeDelta = new Vector2(70f, 24f);
				LoadSlotIcon(j, val10, val12);
			}
			float num6 = (90f - 360f / (float)num * (float)displayCount) * ((float)Math.PI / 180f);
			Vector2 anchoredPosition = default(Vector2);
			((Vector2)(ref anchoredPosition))._002Ector(Mathf.Cos(num6) * num4, Mathf.Sin(num6) * num4);
			GameObject val14 = new GameObject("Cancel_Icon");
			val14.transform.SetParent(val.transform, false);
			_cancelLabel = val14.AddComponent<TextMeshProUGUI>();
			((TMP_Text)_cancelLabel).text = "✕";
			((TMP_Text)_cancelLabel).fontSize = 40f;
			((TMP_Text)_cancelLabel).fontStyle = (FontStyles)1;
			((TMP_Text)_cancelLabel).alignment = (TextAlignmentOptions)514;
			((Graphic)_cancelLabel).color = new Color(0.6f, 0.6f, 0.6f);
			((Graphic)_cancelLabel).raycastTarget = false;
			RectTransform component9 = val14.GetComponent<RectTransform>();
			((Vector2)(ref val3))._002Ector(0.5f, 0.5f);
			component9.anchorMax = val3;
			component9.anchorMin = val3;
			component9.anchoredPosition = anchoredPosition;
			component9.sizeDelta = new Vector2(60f, 60f);
			_currentRotZ = 0f;
			_targetRotZ = 0f;
			_sectorCurrentRotZ = 0f;
			_sectorTargetRotZ = 0f;
		}
	}

	private void DestroyWheelUI()
	{
		if ((Object)(object)_canvas != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_canvas).gameObject);
			_canvas = null;
		}
		_centerLabel = null;
		_cancelLabel = null;
		_highlightObj = null;
		_sectorHighlightObj = null;
		_slotGraphics.Clear();
		_dividerObjects.Clear();
	}

	private void UpdateSelection()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0496: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0515: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0571: Unknown result type (might be due to invalid IL or missing references)
		//IL_0576: Unknown result type (might be due to invalid IL or missing references)
		//IL_057d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0582: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0623: Unknown result type (might be due to invalid IL or missing references)
		//IL_062a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0641: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0740: Unknown result type (might be due to invalid IL or missing references)
		//IL_070b: Unknown result type (might be due to invalid IL or missing references)
		if (_slotGraphics.Count == 0)
		{
			return;
		}
		Vector2 val = Vector2.op_Implicit(Input.mousePosition);
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
		Vector2 val3 = val - val2;
		int displayCount = DisplayCount;
		if (displayCount <= 0)
		{
			return;
		}
		int num = displayCount + 1;
		float num2 = 360f / (float)num;
		float num3 = num2 * 0.5f;
		bool flag = ((Vector2)(ref val3)).magnitude < 35f;
		float y = Input.mouseScrollDelta.y;
		bool flag2 = Mathf.Abs(y) > 0f && displayCount > 0;
		if (flag2)
		{
			int num4 = ((!(y > 0f)) ? 1 : (-1));
			int selectedIndex = _selectedIndex;
			selectedIndex = (_selectedIndex = ((selectedIndex >= 0) ? (((selectedIndex + num4) % num + num) % num) : ((num4 > 0) ? (displayCount - 1) : 0)));
			_targetRotZ = (0f - num2) * (float)selectedIndex;
			UpdateCancelVisual(selectedIndex >= displayCount);
			_wheelSelectionLock = true;
		}
		if (_wheelSelectionLock && !flag && !flag2)
		{
			float num5 = Mathf.Atan2(val3.y, val3.x) * 57.29578f;
			if (num5 < 0f)
			{
				num5 += 360f;
			}
			if ((Mathf.RoundToInt((90f - num5) / num2) % num + num) % num != _selectedIndex)
			{
				_wheelSelectionLock = false;
			}
		}
		if (flag && !_wheelSelectionLock)
		{
			_selectedIndex = -1;
			UpdateCancelVisual(active: false);
			ShowNoSelectionVisuals();
			return;
		}
		int num6;
		if (_wheelSelectionLock)
		{
			num6 = _selectedIndex;
		}
		else
		{
			float num7 = Mathf.Atan2(val3.y, val3.x) * 57.29578f;
			if (num7 < 0f)
			{
				num7 += 360f;
			}
			num6 = Mathf.RoundToInt((90f - num7) / num2);
			num6 = (num6 % num + num) % num;
		}
		if (num6 >= displayCount)
		{
			_selectedIndex = num6;
			UpdateCancelVisual(active: true);
			if (!_wheelSelectionLock)
			{
				_targetRotZ = (0f - num2) * (float)num6;
			}
			_currentRotZ = Mathf.LerpAngle(_currentRotZ, _targetRotZ, Time.deltaTime * 15f);
			if ((Object)(object)_highlightObj != (Object)null)
			{
				if (!_highlightObj.activeSelf)
				{
					_highlightObj.SetActive(true);
				}
				_highlightObj.transform.localRotation = Quaternion.Euler(0f, 0f, _currentRotZ);
			}
			_sectorTargetRotZ = (0f - num2) * (float)num6;
			_sectorCurrentRotZ = Mathf.LerpAngle(_sectorCurrentRotZ, _sectorTargetRotZ, Time.deltaTime * 15f);
			if ((Object)(object)_sectorHighlightObj != (Object)null)
			{
				if (!_sectorHighlightObj.activeSelf)
				{
					_sectorHighlightObj.SetActive(true);
				}
				_sectorHighlightObj.transform.localRotation = Quaternion.Euler(0f, 0f, _sectorCurrentRotZ);
			}
			for (int i = 0; i < _slotGraphics.Count; i++)
			{
				if ((Object)(object)_slotGraphics[i] != (Object)null)
				{
					_slotGraphics[i].color = new Color(0.6f, 0.6f, 0.6f);
					((Transform)_slotGraphics[i].rectTransform).localScale = Vector3.one;
				}
			}
			for (int j = 0; j < _dividerObjects.Count; j++)
			{
				if (_dividerObjects[j].activeSelf)
				{
					_dividerObjects[j].SetActive(false);
				}
			}
			if ((Object)(object)_centerLabel != (Object)null)
			{
				((TMP_Text)_centerLabel).text = GetNoSelectionText();
				((Graphic)_centerLabel).color = new Color(1f, 1f, 1f, 0.6f);
			}
			return;
		}
		_selectedIndex = num6;
		UpdateCancelVisual(active: false);
		if (!_wheelSelectionLock)
		{
			float num8 = Mathf.Atan2(val3.y, val3.x) * 57.29578f;
			if (num8 < 0f)
			{
				num8 += 360f;
			}
			_targetRotZ = num8 - 90f;
		}
		_currentRotZ = Mathf.LerpAngle(_currentRotZ, _targetRotZ, Time.deltaTime * 15f);
		if ((Object)(object)_highlightObj != (Object)null)
		{
			if (!_highlightObj.activeSelf)
			{
				_highlightObj.SetActive(true);
			}
			_highlightObj.transform.localRotation = Quaternion.Euler(0f, 0f, _currentRotZ);
		}
		_sectorTargetRotZ = (0f - num2) * (float)_selectedIndex;
		_sectorCurrentRotZ = Mathf.LerpAngle(_sectorCurrentRotZ, _sectorTargetRotZ, Time.deltaTime * 15f);
		if ((Object)(object)_sectorHighlightObj != (Object)null)
		{
			if (!_sectorHighlightObj.activeSelf)
			{
				_sectorHighlightObj.SetActive(true);
			}
			_sectorHighlightObj.transform.localRotation = Quaternion.Euler(0f, 0f, _sectorCurrentRotZ);
		}
		for (int k = 0; k < displayCount; k++)
		{
			float num9 = Mathf.Abs(Mathf.DeltaAngle((0f - num2) * (float)k, _currentRotZ));
			float num10 = Mathf.Clamp01(1f - num9 / num3);
			float num11 = 1f + 0.3f * num10;
			Color color = Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.white, num10);
			int num12 = k * 2;
			int num13 = k * 2 + 1;
			if (num12 < _slotGraphics.Count && (Object)(object)_slotGraphics[num12] != (Object)null)
			{
				((Transform)_slotGraphics[num12].rectTransform).localScale = Vector3.one * num11;
				_slotGraphics[num12].color = color;
			}
			if (num13 < _slotGraphics.Count && (Object)(object)_slotGraphics[num13] != (Object)null)
			{
				((Transform)_slotGraphics[num13].rectTransform).localScale = Vector3.one * num11;
				_slotGraphics[num13].color = color;
			}
		}
		for (int l = 0; l < _dividerObjects.Count; l++)
		{
			bool flag3 = _selectedIndex >= 0 && _selectedIndex < displayCount && (l == _selectedIndex || l == (_selectedIndex - 1 + num) % num);
			if (_dividerObjects[l].activeSelf != flag3)
			{
				_dividerObjects[l].SetActive(flag3);
			}
		}
		if ((Object)(object)_centerLabel != (Object)null)
		{
			if (_selectedIndex >= 0 && _selectedIndex < displayCount)
			{
				((TMP_Text)_centerLabel).text = GetSlotFullName(_selectedIndex);
				((Graphic)_centerLabel).color = Color.white;
			}
			else
			{
				((TMP_Text)_centerLabel).text = GetNoSelectionText();
				((Graphic)_centerLabel).color = new Color(1f, 1f, 1f, 0.6f);
			}
		}
	}

	private void UpdateCancelVisual(bool active)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_cancelLabel == (Object)null))
		{
			if (active)
			{
				((Graphic)_cancelLabel).color = new Color(1f, 0.3f, 0.3f, 1f);
				((Transform)((TMP_Text)_cancelLabel).rectTransform).localScale = Vector3.one * 1.25f;
			}
			else
			{
				((Graphic)_cancelLabel).color = new Color(0.6f, 0.6f, 0.6f);
				((Transform)((TMP_Text)_cancelLabel).rectTransform).localScale = Vector3.one;
			}
		}
	}

	private void ShowNoSelectionVisuals()
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_highlightObj != (Object)null)
		{
			_highlightObj.SetActive(false);
		}
		if ((Object)(object)_sectorHighlightObj != (Object)null)
		{
			_sectorHighlightObj.SetActive(false);
		}
		for (int i = 0; i < _slotGraphics.Count; i++)
		{
			if ((Object)(object)_slotGraphics[i] != (Object)null)
			{
				_slotGraphics[i].color = new Color(0.6f, 0.6f, 0.6f);
				((Transform)_slotGraphics[i].rectTransform).localScale = Vector3.one;
			}
		}
		for (int j = 0; j < _dividerObjects.Count; j++)
		{
			if (_dividerObjects[j].activeSelf)
			{
				_dividerObjects[j].SetActive(false);
			}
		}
		if ((Object)(object)_centerLabel != (Object)null)
		{
			((TMP_Text)_centerLabel).text = GetNoSelectionText();
			((Graphic)_centerLabel).color = new Color(1f, 1f, 1f, 0.6f);
		}
		_selectedIndex = -1;
	}

	protected virtual void OnDestroy()
	{
		if ((Object)(object)Instance != (Object)null && (Object)(object)Instance == (Object)(object)(T)this)
		{
			Instance = null;
		}
	}
}
