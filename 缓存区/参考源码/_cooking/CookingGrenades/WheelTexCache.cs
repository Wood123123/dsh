using System.Collections.Generic;
using UnityEngine;

namespace CookingGrenades;

internal static class WheelTexCache
{
	private static Sprite _circleSprite;

	private static Sprite _dividerSprite;

	private static readonly Dictionary<int, Sprite> _sectorSprites = new Dictionary<int, Sprite>();

	private static readonly Dictionary<int, Sprite> _highlightArcSprites = new Dictionary<int, Sprite>();

	public static Sprite Circle(int texSize, float innerR, float outerR)
	{
		if ((Object)(object)_circleSprite != (Object)null)
		{
			return _circleSprite;
		}
		_circleSprite = CreateSprite(CreateCircleRingTexture(texSize, innerR, outerR));
		Plugin.log.LogInfo((object)$"[WheelTex] 圆环纹理已生成 {texSize}x{texSize}");
		return _circleSprite;
	}

	public static Sprite Divider(int texSize, float innerR, float outerR, float lineHalfWidth)
	{
		if ((Object)(object)_dividerSprite != (Object)null)
		{
			return _dividerSprite;
		}
		_dividerSprite = CreateSprite(CreateSingleDividerTexture(texSize, innerR, outerR, lineHalfWidth));
		Plugin.log.LogInfo((object)$"[WheelTex] 分隔线纹理已生成 {texSize}x{texSize}");
		return _dividerSprite;
	}

	public static Sprite Sector(int sectorCount, int texSize, float innerR, float outerR, float arcDeg)
	{
		if (_sectorSprites.TryGetValue(sectorCount, out var value))
		{
			return value;
		}
		Sprite val = CreateSprite(CreateSectorHighlightTexture(texSize, innerR, outerR, arcDeg));
		_sectorSprites[sectorCount] = val;
		Plugin.log.LogInfo((object)$"[WheelTex] 扇区高亮纹理已生成 sectors={sectorCount} arc={arcDeg:0.##}°");
		return val;
	}

	public static Sprite HighlightArc(int sectorCount, int texSize, float innerR, float outerR, float arcDeg)
	{
		if (_highlightArcSprites.TryGetValue(sectorCount, out var value))
		{
			return value;
		}
		Sprite val = CreateSprite(CreateHighlightArcTexture(texSize, innerR, outerR, arcDeg));
		_highlightArcSprites[sectorCount] = val;
		Plugin.log.LogInfo((object)$"[WheelTex] 高亮弧纹理已生成 sectors={sectorCount} arc={arcDeg:0.##}°");
		return val;
	}

	public static void Reset()
	{
		_circleSprite = null;
		_dividerSprite = null;
		_sectorSprites.Clear();
		_highlightArcSprites.Clear();
	}

	private static Sprite CreateSprite(Texture2D tex)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		return Sprite.Create(tex, new Rect(0f, 0f, (float)((Texture)tex).width, (float)((Texture)tex).height), new Vector2(0.5f, 0.5f));
	}

	private static Texture2D CreateCircleRingTexture(int texSize, float innerR, float outerR)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		Texture2D val = new Texture2D(texSize, texSize, (TextureFormat)4, false);
		Color32[] array = (Color32[])(object)new Color32[texSize * texSize];
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)texSize / 2f, (float)texSize / 2f);
		Color32 val3 = default(Color32);
		((Color32)(ref val3))._002Ector((byte)200, (byte)200, (byte)200, (byte)220);
		Color32 val4 = default(Color32);
		((Color32)(ref val4))._002Ector((byte)0, (byte)0, (byte)0, (byte)0);
		for (int i = 0; i < texSize; i++)
		{
			for (int j = 0; j < texSize; j++)
			{
				float num = Vector2.Distance(new Vector2((float)j, (float)i), val2);
				array[i * texSize + j] = ((num >= innerR && num <= outerR) ? val3 : val4);
			}
		}
		val.SetPixels32(array);
		val.Apply();
		return val;
	}

	private static Texture2D CreateSingleDividerTexture(int texSize, float innerR, float outerR, float lineHalfWidth)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Expected O, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		Texture2D val = new Texture2D(texSize, texSize, (TextureFormat)4, false);
		Color32[] array = (Color32[])(object)new Color32[texSize * texSize];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Color32((byte)0, (byte)0, (byte)0, (byte)0);
		}
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)texSize / 2f, (float)texSize / 2f);
		Color32 val3 = default(Color32);
		for (float num = innerR; num <= outerR; num += 0.5f)
		{
			float num2 = (num - innerR) / (outerR - innerR);
			byte b = (byte)Mathf.Lerp(200f, 0f, num2);
			((Color32)(ref val3))._002Ector(byte.MaxValue, byte.MaxValue, byte.MaxValue, b);
			for (float num3 = 0f - lineHalfWidth; num3 <= lineHalfWidth; num3 += 0.5f)
			{
				float num4 = val2.x + num3;
				float num5 = val2.y + num;
				int num6 = Mathf.RoundToInt(num4);
				int num7 = Mathf.RoundToInt(num5);
				if (num6 >= 0 && num6 < texSize && num7 >= 0 && num7 < texSize)
				{
					array[num7 * texSize + num6] = val3;
				}
			}
		}
		val.SetPixels32(array);
		val.Apply();
		return val;
	}

	private static Texture2D CreateHighlightArcTexture(int texSize, float innerR, float outerR, float arcDeg)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		Texture2D val = new Texture2D(texSize, texSize, (TextureFormat)4, false);
		Color32[] array = (Color32[])(object)new Color32[texSize * texSize];
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)texSize / 2f, (float)texSize / 2f);
		float num = arcDeg / 2f;
		Color32 val3 = default(Color32);
		((Color32)(ref val3))._002Ector(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		Color32 val4 = default(Color32);
		((Color32)(ref val4))._002Ector((byte)0, (byte)0, (byte)0, (byte)0);
		for (int i = 0; i < texSize; i++)
		{
			for (int j = 0; j < texSize; j++)
			{
				float num2 = (float)j - val2.x;
				float num3 = (float)i - val2.y;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				if (num4 >= innerR && num4 <= outerR)
				{
					float num5 = Mathf.Atan2(num3, num2) * 57.29578f;
					if (num5 < 0f)
					{
						num5 += 360f;
					}
					float num6 = num5 - 90f;
					if (num6 > 180f)
					{
						num6 -= 360f;
					}
					if (num6 < -180f)
					{
						num6 += 360f;
					}
					array[i * texSize + j] = ((Mathf.Abs(num6) <= num) ? val3 : val4);
				}
				else
				{
					array[i * texSize + j] = val4;
				}
			}
		}
		val.SetPixels32(array);
		val.Apply();
		((Texture)val).wrapMode = (TextureWrapMode)1;
		return val;
	}

	private static Texture2D CreateSectorHighlightTexture(int texSize, float innerR, float outerR, float arcDeg)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		Texture2D val = new Texture2D(texSize, texSize, (TextureFormat)4, false);
		Color32[] array = (Color32[])(object)new Color32[texSize * texSize];
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)texSize / 2f, (float)texSize / 2f);
		float num = arcDeg / 2f;
		Color32 val3 = default(Color32);
		((Color32)(ref val3))._002Ector((byte)0, (byte)0, (byte)0, (byte)0);
		for (int i = 0; i < texSize; i++)
		{
			for (int j = 0; j < texSize; j++)
			{
				float num2 = (float)j - val2.x;
				float num3 = (float)i - val2.y;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				if (num4 >= innerR && num4 <= outerR)
				{
					float num5 = Mathf.Atan2(num3, num2) * 57.29578f;
					if (num5 < 0f)
					{
						num5 += 360f;
					}
					float num6 = num5 - 90f;
					if (num6 > 180f)
					{
						num6 -= 360f;
					}
					if (num6 < -180f)
					{
						num6 += 360f;
					}
					if (Mathf.Abs(num6) <= num)
					{
						float num7 = (num4 - innerR) / (outerR - innerR);
						byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(0.35f, 0f, num7) * 255f);
						array[i * texSize + j] = new Color32((byte)160, (byte)160, (byte)160, b);
					}
					else
					{
						array[i * texSize + j] = val3;
					}
				}
				else
				{
					array[i * texSize + j] = val3;
				}
			}
		}
		val.SetPixels32(array);
		val.Apply();
		((Texture)val).wrapMode = (TextureWrapMode)1;
		return val;
	}
}
