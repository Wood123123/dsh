using System;
using System.Collections.Generic;
using System.Linq;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StashTools;

public sealed class StashBehaviour : MonoBehaviour
{
	internal static InventoryScreen? ScreenView;

	internal static StashBehaviour? Driver;

	internal static bool Drawing;

	private static int _suppressUntil;

	private static readonly ManagedDispatch Dispatch = new ManagedDispatch(null, 0L);

	private Vector2 _origin;

	private Vector2 _end;

	private readonly HashSet<string> _before = new HashSet<string>();

	private GridItemView[] _views = Array.Empty<GridItemView>();

	private float _refreshAt;

	private GUIStyle? _label;

	private static bool _wasActive;

	internal static bool SuppressClick => Time.frameCount <= _suppressUntil;

	internal static bool Shift
	{
		get
		{
			if (!Input.GetKey((KeyCode)304))
			{
				return Input.GetKey((KeyCode)303);
			}
			return true;
		}
	}

	internal static bool Control
	{
		get
		{
			if (!Input.GetKey((KeyCode)306))
			{
				return Input.GetKey((KeyCode)305);
			}
			return true;
		}
	}

	internal static ItemUiContext? UI => ItemUiContext.Instance;

	internal static InventoryController? InventoryController => InventoryAccess.Controller(ScreenView);

	internal static bool InRaid
	{
		get
		{
			if (!TraderSelection.Active && InventoryController != null)
			{
				return InventoryController.TryCast<BackEndInventoryController>() == null;
			}
			return false;
		}
	}

	internal static bool CanSell
	{
		get
		{
			if (Active && !InRaid)
			{
				return !TraderSelection.Active;
			}
			return false;
		}
	}

	internal static bool Active
	{
		get
		{
			if (Plugin.Enabled.Value)
			{
				if (!TraderSelection.Active)
				{
					return InventoryActive;
				}
				return true;
			}
			return false;
		}
	}

	private static bool InventoryActive
	{
		get
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Invalid comparison between Unknown and I4
			if ((Object)(object)ScreenView != (Object)null && ((Component)ScreenView).gameObject.activeInHierarchy && (Object)(object)UI != (Object)null && (int)UI.ContextType == 2 && InventoryController != null && InventoryAccess.InventoryTabActive(ScreenView))
			{
				if (InRaid)
				{
					return Plugin.SelectInRaid.Value;
				}
				return true;
			}
			return false;
		}
	}

	internal static void Open(InventoryScreen screen)
	{
		TraderSelection.Reset();
		ScreenView = screen;
		if (InventoryAccess.Controller(screen)?.TryCast<BackEndInventoryController>() != null)
		{
			MarketQuotes.Resume();
		}
		ChineseSettings.FindManager();
		if (Plugin.DiagnosticMode.Value)
		{
			Plugin.Logger.LogInfo((object)("Inventory screen opened; controller=" + ((object)InventoryAccess.Controller(screen))?.GetType().FullName));
		}
	}

	internal static void Close()
	{
		Batch.Cancel("物品界面已关闭，停止剩余操作。");
		ScreenView = null;
		Selection.Clear();
		Drawing = false;
		DragPreview.Clear();
		MovementEnhancements.End();
		HoverPrices.Suspend();
		LoadoutValue.SuspendPrices();
	}

	internal static void Post(Action action)
	{
		Dispatch.Post(action);
	}

	internal static void Notify(string message)
	{
		Plugin.Logger.LogInfo((object)message);
		try
		{
			NotificationManager.DisplayWarningNotification(message, (ENotificationDurationType)0);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("通知显示失败：" + ex.Message));
		}
	}

	internal static void Error(string message, Exception ex)
	{
		Plugin.Logger.LogError((object)(message + ": " + ex));
		Notify(message + "，已停止；详情见模组日志。");
	}

	internal static void Completed(string message)
	{
		Plugin.Logger.LogInfo((object)message);
	}

	public void Awake()
	{
		Dispatch.Start();
		Driver = this;
	}

	private static void Cleanup(string name, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			try
			{
				Plugin.Logger.LogWarning((object)(name + "清理失败：" + ex));
			}
			catch
			{
			}
		}
	}

	public void OnDestroy()
	{
		if (!((Object)(object)Driver != (Object)null) || Driver == this)
		{
			Dispatch.Stop();
			Driver = null;
			ScreenView = null;
			Drawing = false;
			_wasActive = false;
			Selection.Clear();
			_views = Array.Empty<GridItemView>();
			_before.Clear();
			Cleanup("市场报价请求", MarketQuotes.Stop);
			Cleanup("悬浮价格", HoverPrices.Suspend);
			Cleanup("悬浮提示框", CompactTooltip.Dispose);
			Cleanup("对局收益面板", RaidPanel.Dispose);
			Cleanup("战备面板", LoadoutValue.Dispose);
			Cleanup("结算面板", RaidSummary.Dispose);
		}
	}

	public void Update()
	{
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0374: Unknown result type (might be due to invalid IL or missing references)
		//IL_037f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Dispatch.Drain(32, 2.0, delegate(Exception ex2)
			{
				Plugin.Logger.LogWarning((object)("已忽略失败的异步回调，其他模块继续运行：" + ex2));
			});
			MarketQuotes.Tick();
			if (RaidPause.BlockGameplayUi)
			{
				Drawing = false;
				return;
			}
			FleaRules.Tick();
			Batch.Tick();
			Sales.Tick();
			HoverPrices.Tick();
			RaidEarnings.Tick();
			LoadoutValue.Tick();
			RaidSummary.Tick();
			HistoryUi.Tick();
			if (!Active)
			{
				if (_wasActive)
				{
					Selection.Clear();
					Drawing = false;
					Batch.Cancel("已离开物品界面，停止剩余操作。");
				}
				_wasActive = false;
				return;
			}
			_wasActive = true;
			if (ChineseSettings.IsOpen)
			{
				Drawing = false;
				return;
			}
			if (Input.GetKeyDown((KeyCode)27))
			{
				Selection.Clear();
				Drawing = false;
				Batch.Cancel("已取消剩余操作。");
			}
			if (Time.unscaledTime >= _refreshAt)
			{
				_views = Object.FindObjectsOfType<GridItemView>().Where(Selection.Allowed).ToArray();
				_refreshAt = Time.unscaledTime + 2f;
				HashSet<string> hashSet = _views.Select((GridItemView v) => ((ItemView)v).Item.Id).ToHashSet();
				string[] array = Selection.Items.Keys.ToArray();
				foreach (string text in array)
				{
					if (!hashSet.Contains(text))
					{
						Selection.Items.Remove(text);
					}
				}
			}
			if (Batch.Busy)
			{
				Drawing = false;
				return;
			}
			Vector2 val2;
			if (Input.GetMouseButtonDown(0) && (!CanSell || !Input.GetKey(Plugin.SellKey.Value)) && !Control)
			{
				GameObject val = Raycast(Vector2.op_Implicit(Input.mousePosition));
				if (CanStartSelection(val))
				{
					val2 = (_origin = (_end = Vector2.op_Implicit(Input.mousePosition)));
					_before.Clear();
					if (Shift)
					{
						foreach (string key in Selection.Items.Keys)
						{
							_before.Add(key);
						}
					}
					else
					{
						Selection.Clear();
					}
					Drawing = true;
					if (Plugin.DiagnosticMode.Value)
					{
						Plugin.Logger.LogInfo((object)string.Format("开始框选：对局={0}，起点={1}，候选物品={2}", InRaid, ((val != null) ? ((Object)val).name : null) ?? "空白背景", _views.Length));
					}
				}
			}
			if (!Drawing)
			{
				return;
			}
			_end = Vector2.op_Implicit(Input.mousePosition);
			val2 = _end - _origin;
			bool flag = ((Vector2)(ref val2)).sqrMagnitude >= 36f;
			if (flag)
			{
				_suppressUntil = Time.frameCount + 1;
				Rect val3 = Bounds(_origin, _end);
				GridItemView[] views = _views;
				foreach (GridItemView val4 in views)
				{
					if (!((Object)(object)val4 == (Object)null) && !InventoryAccess.Killed((ItemView)(object)val4) && ((Component)val4).gameObject.activeInHierarchy && ((ItemView)val4).ItemContext != null)
					{
						Rect val5 = ScreenRect(((Component)val4).GetComponent<RectTransform>());
						if ((((Rect)(ref val3)).Overlaps(val5) && Visible(val4, val5)) || _before.Contains(((ItemView)val4).Item.Id))
						{
							Selection.Items[((ItemView)val4).Item.Id] = ((ItemView)val4).Item;
						}
						else
						{
							Selection.Items.Remove(((ItemView)val4).Item.Id);
						}
					}
				}
			}
			if (Input.GetMouseButton(0))
			{
				return;
			}
			Drawing = false;
			if (!flag)
			{
				return;
			}
			Plugin.Logger.LogInfo((object)$"框选结束：{Selection.Count} 件，对局={InRaid}，涉及容器={Selection.Items.Values.Select(delegate(Item i)
			{
				ItemAddress currentAddress = i.CurrentAddress;
				if (currentAddress == null)
				{
					return (string)null;
				}
				GridItemAddress? obj = currentAddress.TryCast<GridItemAddress>();
				if (obj == null)
				{
					return (string)null;
				}
				Grid grid = obj.Grid;
				if (grid == null)
				{
					return (string)null;
				}
				CompoundItem parentItem = grid.ParentItem;
				return (parentItem == null) ? null : ((Item)parentItem).Id;
			}).Distinct().Count()}");
		}
		catch (Exception ex)
		{
			Drawing = false;
			_refreshAt = Time.unscaledTime + 1f;
			Error("仓库交互异常", ex);
		}
	}

	public void LateUpdate()
	{
		if (RaidPause.BlockGameplayUi)
		{
			return;
		}
		try
		{
			SelectionVisuals.Refresh(_views, Active && !ChineseSettings.IsOpen);
			DragPreview.Tick(_views);
		}
		catch (Exception ex)
		{
			SelectionVisuals.Clear();
			Plugin.Logger.LogError((object)("选中标记更新失败：" + ex));
		}
	}

	public void OnGUI()
	{
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		if (!Active || RaidPause.BlockGameplayUi || ChineseSettings.IsOpen || (int)Event.current.type != 7)
		{
			return;
		}
		if (_label == null)
		{
			_label = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				wordWrap = true,
				clipping = (TextClipping)1
			};
		}
		Color color = GUI.color;
		try
		{
			if (Drawing)
			{
				Rect r = Bounds(_origin, _end);
				((Rect)(ref r)).y = (float)Screen.height - ((Rect)(ref r)).yMax;
				GUI.color = new Color(1f, 1f, 1f, 0.6f);
				Outline(r);
			}
			GUI.color = Color.white;
			DragPreview.DrawCount();
			if (Plugin.ShowStatus.Value && (Selection.Count > 0 || Batch.Busy))
			{
				bool busy = Batch.Busy;
				float num = (busy ? 64 : 30);
				Rect val = default(Rect);
				((Rect)(ref val))._002Ector((float)(Screen.width - 448), (float)(Screen.height - 65) - num, 430f, num);
				GUI.color = new Color(0f, 0f, 0f, 0.65f);
				GUI.DrawTexture(val, (Texture)(object)Texture2D.whiteTexture);
				GUI.color = Color.white;
				string text = $"已选 {Selection.Count} 件";
				if (busy)
				{
					text = text + "\n" + Batch.Status;
				}
				GUI.Label(new Rect(((Rect)(ref val)).x + 10f, ((Rect)(ref val)).y + 4f, ((Rect)(ref val)).width - 20f, ((Rect)(ref val)).height - 8f), text, _label);
			}
		}
		finally
		{
			GUI.color = color;
		}
	}

	private static Rect Bounds(Vector2 a, Vector2 b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	internal static bool InInventoryArea(GameObject target)
	{
		if (!TraderSelection.Active)
		{
			if ((Object)(object)ScreenView != (Object)null)
			{
				if (!target.transform.IsChildOf(((Component)ScreenView).transform))
				{
					return (Object)(object)target.GetComponentInParent<GridWindow>() != (Object)null;
				}
				return true;
			}
			return false;
		}
		return TraderSelection.InArea(target);
	}

	private static bool CanStartSelection(GameObject? hit)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)hit == (Object)null)
		{
			object obj;
			if (!TraderSelection.Active)
			{
				InventoryScreen? screenView = ScreenView;
				obj = ((screenView != null) ? ((Component)screenView).gameObject : null);
			}
			else
			{
				TraderDealScreen? screen = TraderSelection.Screen;
				obj = ((screen != null) ? ((Component)screen).gameObject : null);
			}
			GameObject val = (GameObject)obj;
			if ((Object)(object)val != (Object)null)
			{
				Rect val2 = ScreenRect(val.GetComponent<RectTransform>());
				return ((Rect)(ref val2)).Contains(Input.mousePosition);
			}
			return false;
		}
		if (!InInventoryArea(hit))
		{
			return false;
		}
		if ((Object)(object)hit.GetComponentInParent<Selectable>() != (Object)null || (Object)(object)hit.GetComponentInParent<TMP_InputField>() != (Object)null)
		{
			return false;
		}
		ItemView componentInParent = hit.GetComponentInParent<ItemView>();
		if ((Object)(object)componentInParent != (Object)null)
		{
			if (Shift)
			{
				GridItemView val3 = componentInParent.TryCast<GridItemView>();
				if (val3 != null)
				{
					return Selection.Allowed(val3);
				}
			}
			return false;
		}
		GridView componentInParent2 = hit.GetComponentInParent<GridView>();
		if (TraderSelection.Active)
		{
			object obj2;
			if (componentInParent2 == null)
			{
				obj2 = null;
			}
			else
			{
				Grid grid = componentInParent2.Grid;
				obj2 = ((grid != null) ? grid.ParentItem : null);
			}
			CompoundItem val4 = (CompoundItem)obj2;
			if (val4 != null && Selection.Owned((Item?)(object)val4))
			{
				return true;
			}
		}
		MonoBehaviour[] componentsInParent = hit.GetComponentsInParent<MonoBehaviour>();
		foreach (MonoBehaviour val5 in componentsInParent)
		{
			if ((Object)(object)val5 == (Object)null)
			{
				continue;
			}
			string name = ((object)val5).GetType().Name;
			if (name == "ScrollRectNoDrag" || name == "EmptySlotMenuTrigger" || ((Object)val5).name == "Inner")
			{
				continue;
			}
			Type type = ((object)val5).GetType();
			bool flag = false;
			while (type != null)
			{
				if (type.Name.StartsWith("Window`"))
				{
					flag = true;
					break;
				}
				type = type.BaseType;
			}
			if (!flag && (name == "ChatScreen" || ((Object)val5).name == "Search Icon" || (Object)(object)val5.TryCast<TextMeshProUGUI>() != (Object)null || val5.TryCast<IDragHandler>() != null || val5.TryCast<IBeginDragHandler>() != null || val5.TryCast<IPointerClickHandler>() != null || val5.TryCast<IPointerDownHandler>() != null || val5.TryCast<IPointerUpHandler>() != null))
			{
				return false;
			}
		}
		return true;
	}

	private static void Outline(Rect r)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		GUI.DrawTexture(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), (Texture)(object)Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), (Texture)(object)Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), (Texture)(object)Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), (Texture)(object)Texture2D.whiteTexture);
	}

	internal static Rect ScreenRect(RectTransform rt)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		rt.GetWorldCorners(array);
		Canvas componentInParent = ((Component)rt).GetComponentInParent<Canvas>();
		Camera val = (((Object)(object)componentInParent != (Object)null && (int)componentInParent.renderMode != 0) ? componentInParent.worldCamera : null);
		return Bounds(RectTransformUtility.WorldToScreenPoint(val, array[0]), RectTransformUtility.WorldToScreenPoint(val, array[2]));
	}

	private static bool Visible(GridItemView view, Rect rect)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (((Rect)(ref rect)).width < 1f || ((Rect)(ref rect)).height < 1f || ((Rect)(ref rect)).xMax < 0f || ((Rect)(ref rect)).yMax < 0f || ((Rect)(ref rect)).xMin >= (float)Screen.width || ((Rect)(ref rect)).yMin >= (float)Screen.height)
		{
			return false;
		}
		GameObject? obj = Raycast(((Rect)(ref rect)).center);
		GridItemView val = ((obj != null) ? obj.GetComponentInParent<GridItemView>() : null);
		if ((Object)(object)val != (Object)null)
		{
			Item item = ((ItemView)val).Item;
			return ((item != null) ? item.Id : null) == ((ItemView)view).Item.Id;
		}
		return false;
	}

	private static GameObject? Raycast(Vector2 p)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)EventSystem.current == (Object)null)
		{
			return null;
		}
		PointerEventData val = new PointerEventData(EventSystem.current)
		{
			position = p
		};
		List<RaycastResult> list = new List<RaycastResult>();
		EventSystem.current.RaycastAll(val, list);
		if (list.Count <= 0)
		{
			return null;
		}
		RaycastResult val2 = list[0];
		return ((RaycastResult)(ref val2)).gameObject;
	}
}
