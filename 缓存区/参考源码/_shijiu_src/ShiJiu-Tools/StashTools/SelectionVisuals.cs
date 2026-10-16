using System;
using System.Collections.Generic;
using System.Linq;
using EFT.AssetsManager;
using EFT.UI.DragAndDrop;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class SelectionVisuals
{
	private static GameObject? _mark;

	private static GameObject? _background;

	private static readonly List<GameObject> Shown = new List<GameObject>();

	private static bool _lastActive;

	private static int _lastCount = -1;

	private static int _lastViewCount = -1;

	private static string _lastSelection = "";

	internal static void Clear()
	{
		foreach (GameObject item in Shown)
		{
			if ((Object)(object)item != (Object)null)
			{
				item.SetActive(false);
			}
		}
		Shown.Clear();
		_lastActive = false;
		_lastCount = -1;
		_lastViewCount = -1;
		_lastSelection = "";
	}

	private static void Initialize()
	{
		if ((Object)(object)_mark != (Object)null && (Object)(object)_background != (Object)null)
		{
			return;
		}
		RagfairNewOfferItemView val = ItemViewFactory.CreateFromPool<RagfairNewOfferItemView>("ragfair_layout");
		try
		{
			_mark = Object.Instantiate<GameObject>(val._selectedMark);
			_background = Object.Instantiate<GameObject>(val._selectedBackground);
			_mark.SetActive(false);
			_background.SetActive(false);
			Object.DontDestroyOnLoad((Object)(object)_mark);
			Object.DontDestroyOnLoad((Object)(object)_background);
		}
		finally
		{
			((AssetPoolObject)val).ReturnToPool();
		}
	}

	internal static void Refresh(GridItemView[] views, bool active)
	{
		if (!active)
		{
			if (_lastActive || Shown.Count > 0)
			{
				Clear();
			}
			return;
		}
		string text = ((Selection.Items.Count == 0) ? "" : string.Join("\u001f", Selection.Items.Keys.OrderBy((string x) => x)));
		if (!StashBehaviour.Drawing && _lastActive && _lastCount == Selection.Count && _lastViewCount == views.Length && string.Equals(_lastSelection, text, StringComparison.Ordinal))
		{
			return;
		}
		Clear();
		_lastActive = true;
		_lastCount = Selection.Count;
		_lastViewCount = views.Length;
		_lastSelection = text;
		if (Selection.Count == 0)
		{
			return;
		}
		Initialize();
		foreach (GridItemView val in views)
		{
			if (!((Object)(object)val == (Object)null) && !InventoryAccess.Killed((ItemView)(object)val) && ((Component)val).gameObject.activeInHierarchy && Selection.Contains(((ItemView)val).Item))
			{
				Show(val, "StashToolsSelectedMark", _mark, background: false);
				Show(val, "StashToolsSelectedBackground", _background, background: true);
			}
		}
	}

	private static void Show(GridItemView view, string name, GameObject template, bool background)
	{
		Transform val = ((Component)view).transform.Find(name);
		GameObject val2 = (((Object)(object)val != (Object)null) ? ((Component)val).gameObject : Object.Instantiate<GameObject>(template, ((Component)view).transform, false));
		if ((Object)(object)val == (Object)null)
		{
			((Object)val2).name = name;
			Graphic[] componentsInChildren = val2.GetComponentsInChildren<Graphic>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].raycastTarget = false;
			}
			if (background)
			{
				val2.transform.SetAsFirstSibling();
			}
		}
		val2.SetActive(true);
		Shown.Add(val2);
	}
}
