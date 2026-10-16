using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class DragPreview
{
	private sealed record Icon(Texture Texture, Rect UV, PreviewIconGeometry.Shape Shape, float Angle, ItemRotation Rotation)
	{
		[CompilerGenerated]
		public void Deconstruct(out Texture Texture, out Rect UV, out PreviewIconGeometry.Shape Shape, out float Angle, out ItemRotation Rotation)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Expected I4, but got Unknown
			Texture = this.Texture;
			UV = this.UV;
			Shape = this.Shape;
			Angle = this.Angle;
			Rotation = (ItemRotation)(int)this.Rotation;
		}
	}

	private static DraggedItemView? _drag;

	private static GridView? _view;

	private static GroupMovePlan? _plan;

	private static GroupMovePlan? _shown;

	private static GroupMovePlan.Snapshot? _snapshot;

	private static readonly Dictionary<string, Icon> Icons = new Dictionary<string, Icon>();

	private static string _key = "";

	private static bool _failed;

	private static GUIStyle? _countStyle;

	internal static void Begin(ItemView view)
	{
		Clear();
		_failed = false;
		if (StashBehaviour.Active && Selection.Count > 1 && Selection.Contains(view.Item))
		{
			_drag = view.DraggedItemView;
		}
	}

	internal static GroupMovePlan Ensure(GridView view, DragItemContext lead, bool force = false)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		LocationInGrid val = view.CalculateItemLocation(lead);
		string text = $"{view.Identity()}:{((ItemContext)lead).Item.Id}:{val?.x}:{val?.y}:{lead.ItemRotation}:" + string.Join(",", Selection.Items.Keys);
		if (!force && text == _key && _plan != null)
		{
			return _plan;
		}
		Item[] items = Selection.ItemsFor(((ItemContext)lead).Item);
		if (force || _snapshot?.Grid != view.Grid.Identity())
		{
			_snapshot = GroupMovePlan.Capture(view.Grid, items);
		}
		_plan = GroupMovePlan.Build(items, lead, view, _snapshot);
		_key = text;
		_view = view;
		return _plan;
	}

	internal static bool SameAsShown(GroupMovePlan plan)
	{
		if (_shown != null)
		{
			return LayoutKey(_shown) == LayoutKey(plan);
		}
		return true;
	}

	private static string LayoutKey(GroupMovePlan p)
	{
		return p.Error + "/" + p.Skipped + "/" + string.Join(";", p.Steps.Select(delegate(GroupMovePlan.Step s)
		{
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			return $"{s.Item.Id}:{s.Address.Grid.Identity()}:{s.Address.LocationInGrid.x}:{s.Address.LocationInGrid.y}:{s.Address.LocationInGrid.r}:{s.Unchanged}";
		}));
	}

	internal static void Tick(GridItemView[] sources)
	{
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		if (_failed)
		{
			return;
		}
		try
		{
			if ((Object)(object)_drag == (Object)null || !((Component)_drag).gameObject.activeInHierarchy || !Input.GetMouseButton(0) || !StashBehaviour.Active || ChineseSettings.IsOpen || Batch.Busy || Selection.Count < 2)
			{
				Clear();
				return;
			}
			foreach (GridItemView val in sources)
			{
				if (!((Object)(object)val == (Object)null) && !InventoryAccess.Killed((ItemView)(object)val) && Selection.Contains(((ItemView)val).Item) && !Icons.ContainsKey(((ItemView)val).Item.Id) && !(((ItemView)val).Item.Id == ((ItemContext)_drag.ItemContext).Item.Id))
				{
					Image mainImage = ((ItemView)val).MainImage;
					Sprite val2 = ((mainImage != null) ? mainImage.overrideSprite : null) ?? ((mainImage != null) ? mainImage.sprite : null);
					if (!((Object)(object)val2 == (Object)null) && !((Object)(object)val2.texture == (Object)null))
					{
						Rect textureRect = val2.textureRect;
						Texture2D texture = val2.texture;
						Rect rect = val2.rect;
						Vector2 textureRectOffset = val2.textureRectOffset;
						PreviewIconGeometry.Shape shape = PreviewIconGeometry.A(((Rect)(ref rect)).width, ((Rect)(ref rect)).height, ((Rect)(ref textureRect)).width, ((Rect)(ref textureRect)).height, textureRectOffset.x, textureRectOffset.y, mainImage.pixelsPerUnit);
						((Rect)(ref textureRect))._002Ector(((Rect)(ref textureRect)).x / (float)((Texture)texture).width, ((Rect)(ref textureRect)).y / (float)((Texture)texture).height, ((Rect)(ref textureRect)).width / (float)((Texture)texture).width, ((Rect)(ref textureRect)).height / (float)((Texture)texture).height);
						Quaternion val3 = (((int)((ItemView)val).ItemRotation == 0) ? ItemViewFactory.HorizontalRotation : ItemViewFactory.VerticalRotation);
						Icons[((ItemView)val).Item.Id] = new Icon((Texture)(object)texture, textureRect, shape, 0f - ((Quaternion)(ref val3)).eulerAngles.z, ((ItemView)val).ItemRotation);
					}
				}
			}
			GridView val4 = InventoryAccess.Container(_drag)?.TryCast<GridView>();
			if ((Object)(object)val4 == (Object)null || val4.Grid == null || val4._nonInteractable || (Object)(object)val4.TryCast<TradingTableGridView>() != (Object)null || InventoryAccess.Target(_drag) != null)
			{
				_shown = null;
				_view = null;
			}
			else
			{
				_shown = Ensure(val4, _drag.ItemContext);
			}
		}
		catch (Exception ex)
		{
			_shown = null;
			_failed = true;
			Plugin.Logger.LogError((object)("只读拖动预览停止：" + ex));
		}
	}

	internal static void DrawCount()
	{
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Invalid comparison between Unknown and I4
		if (_failed || (Object)(object)_drag == (Object)null || !Input.GetMouseButton(0) || Selection.Count < 2)
		{
			return;
		}
		Matrix4x4 matrix = GUI.matrix;
		Color color = GUI.color;
		try
		{
			if ((Object)(object)_view != (Object)null && _shown != null)
			{
				RectTransform component = ((Component)_view).GetComponent<RectTransform>();
				Rect val = StashBehaviour.ScreenRect(component);
				float x = ((Transform)component).lossyScale.x;
				float y = ((Transform)component).lossyScale.y;
				float num = 63f * x;
				float num2 = 63f * y;
				Rect val3 = default(Rect);
				foreach (GroupMovePlan.Step step in _shown.Steps)
				{
					if (!PreviewIconGeometry.A(step.Item.Id, ((ItemContext)_drag.ItemContext).Item.Id, step.Unchanged))
					{
						continue;
					}
					LocationInGrid locationInGrid = step.Address.LocationInGrid;
					IntVec2 val2 = step.Item.CalculateRotatedSize(locationInGrid.r);
					((Rect)(ref val3))._002Ector(((Rect)(ref val)).xMin + (float)locationInGrid.x * num, (float)Screen.height - ((Rect)(ref val)).yMax + (float)locationInGrid.y * num2, (float)val2.X * num, (float)val2.Y * num2);
					if (((Rect)(ref val3)).yMin < 0f || ((Rect)(ref val3)).yMax > (float)Screen.height)
					{
						continue;
					}
					GUI.color = new Color(0.1f, 0.55f, 0.1f, 0.5f);
					GUI.DrawTexture(val3, (Texture)(object)Texture2D.whiteTexture);
					if (Icons.TryGetValue(step.Item.Id, out Icon value))
					{
						GUI.color = Color.white;
						PreviewIconGeometry.Shape shape = value.Shape;
						Rect val4 = new Rect(((Rect)(ref val3)).center.x + (shape.OffsetX - shape.Width / 2f) * x, ((Rect)(ref val3)).center.y + (shape.OffsetY - shape.Height / 2f) * y, shape.Width * x, shape.Height * y);
						float num3 = value.Angle;
						if (locationInGrid.r != value.Rotation)
						{
							num3 += (float)(((int)locationInGrid.r == 1) ? 90 : (-90));
						}
						GUIUtility.RotateAroundPivot(num3, ((Rect)(ref val3)).center);
						GUI.DrawTextureWithTexCoords(val4, value.Texture, value.UV, true);
						GUI.matrix = matrix;
					}
				}
			}
			GUI.color = Color.white;
			if (_countStyle == null)
			{
				_countStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 32,
					fontStyle = (FontStyle)1
				};
			}
			int num4 = _shown?.Steps.Count((GroupMovePlan.Step s) => !s.Unchanged) ?? Selection.Count;
			GroupMovePlan? shown = _shown;
			string text = ((shown != null && shown.Skipped > 0) ? $"{num4}/{Selection.Count}" : Selection.Count.ToString());
			GUI.Label(new Rect(Input.mousePosition.x + 12f, (float)Screen.height - Input.mousePosition.y + 12f, 160f, 48f), text, _countStyle);
		}
		catch (Exception ex)
		{
			_failed = true;
			Plugin.Logger.LogError((object)("预览绘制停止：" + ex));
		}
		finally
		{
			GUI.matrix = matrix;
			GUI.color = color;
		}
	}

	internal static void Clear()
	{
		Icons.Clear();
		_drag = null;
		_plan = null;
		_shown = null;
		_snapshot = null;
		_view = null;
		_key = "";
	}
}
