using System;
using System.Collections.Generic;
using CookingGrenades.Config;
using UnityEngine;

namespace CookingGrenades.Utils;

public class DebugDisplay : MonoBehaviour
{
	private class DisplayItem
	{
		public string Label;

		public Func<object> ValueProvider;

		public DisplayItem(string label, Func<object> valueProvider)
		{
			Label = label;
			ValueProvider = valueProvider;
		}

		public object GetValue()
		{
			try
			{
				return ValueProvider();
			}
			catch (Exception)
			{
				return "System.Exception";
			}
		}
	}

	private static DebugDisplay _instance;

	public bool Enable;

	private Rect _windowRect;

	private Vector2 _scrollPosition;

	private List<DisplayItem> _displayItems;

	public static DebugDisplay Instance
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Expected O, but got Unknown
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Expected O, but got Unknown
			if ((Object)_instance == (Object)null)
			{
				GameObject val = new GameObject("DebugDisplay");
				_instance = val.AddComponent<DebugDisplay>();
				Object.DontDestroyOnLoad((Object)val);
			}
			return _instance;
		}
	}

	public void InsertDisplayObject(string label, Func<object> valueProvider, bool checkDuplicate = true)
	{
		if (!checkDuplicate)
		{
			_displayItems.Add(new DisplayItem(label, valueProvider));
		}
		else if (_displayItems.Find((DisplayItem x) => x.Label == label) == null)
		{
			_displayItems.Add(new DisplayItem(label, valueProvider));
		}
	}

	public void RemoveDisplayObject(string label)
	{
		_displayItems.RemoveAll((DisplayItem item) => item.Label == label);
	}

	public void ClearDisplayObjects()
	{
		_displayItems.Clear();
	}

	private void Awake()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0027: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		if ((Object)_instance != (Object)null && (Object)_instance != (Object)this)
		{
			Object.Destroy((Object)((Component)this).gameObject);
			return;
		}
		_instance = this;
		Object.DontDestroyOnLoad((Object)((Component)this).gameObject);
	}

	private void OnGUI()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (ConfigManager.DebugGUI.Value)
		{
			_windowRect = GUI.Window(0, _windowRect, new WindowFunction(DrawWindow), "Debug Window");
		}
	}

	private void DrawWindow(int windowId)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
		float num = CalculateContentHeight();
		float num2 = Mathf.Clamp(num + 60f, 100f, 400f);
		((Rect)(ref _windowRect)).height = num2;
		if (num > 340f)
		{
			_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(280f),
				GUILayout.Height(num2 - 60f)
			});
		}
		if (_displayItems.Count == 0)
		{
			GUILayout.Label("No objects to display.", Array.Empty<GUILayoutOption>());
		}
		else
		{
			foreach (DisplayItem displayItem in _displayItems)
			{
				object value = displayItem.GetValue();
				GUILayout.Label(FormatDisplayText(displayItem.Label, value), Array.Empty<GUILayoutOption>());
			}
		}
		if (num > 340f)
		{
			GUILayout.EndScrollView();
		}
		if (GUILayout.Button("Clear All", Array.Empty<GUILayoutOption>()))
		{
			ClearDisplayObjects();
		}
		GUILayout.EndVertical();
		GUI.DragWindow();
	}

	private float CalculateContentHeight()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		float num = GUI.skin.label.CalcHeight(new GUIContent("A"), 280f);
		int num2 = ((_displayItems.Count <= 0) ? 1 : _displayItems.Count);
		return num * (float)num2 + 10f;
	}

	private string FormatDisplayText(string label, object value)
	{
		if (value == null)
		{
			return label + ": Null";
		}
		return $"{label}: {value}";
	}

	public DebugDisplay()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		_windowRect = new Rect(20f, 20f, 300f, 50f);
		_scrollPosition = Vector2.zero;
		_displayItems = new List<DisplayItem>();
		((MonoBehaviour)this)._002Ector();
	}
}
