using System;
using System.Reflection;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace CookingGrenades;

public class TrajectoryRenderer : MonoBehaviour
{
	private LineRenderer _line;

	private LineRenderer _landingRing;

	private Transform _landingRingRoot;

	private GameObject _landingCenterObj;

	private Renderer _landingCenterRenderer;

	private Material _ringMaterial;

	private float _ringAnimT;

	private Vector3 _lastRingNormal;

	private GameWorld _cachedGameWorld;

	private Vector3[] _positions;

	private Vector3 _playerVelocity;

	private static readonly Vector3[] _ringPoints = GenerateRingPoints(48);

	private float _mass;

	private string _itemName;

	private float _gravity;

	private const float LinearDrag = 0.1f;

	private static FieldInfo _grenadePrefabField;

	private static bool _grenadePrefabFieldChecked;

	private static int _cachedHitMask = -1;

	private string _lastControllerTypeName;

	private int _recalcCounter;

	private int _lastPositionCount;

	private bool _lastCollided;

	private Vector3 _lastThrowPos;

	private Vector3 _lastThrowVelocity;

	private bool _hasComputed;

	private const float RecalcMoveSqr = 0.0036f;

	private const float RecalcVelSqr = 0.0064f;

	private Color _lastGradStart;

	private Color _lastGradEnd;

	private bool _gradientDirty;

	private static Vector3[] GenerateRingPoints(int segs)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[segs];
		for (int i = 0; i < segs; i++)
		{
			float num = (float)i / (float)segs * (float)Math.PI * 2f;
			array[i] = new Vector3(Mathf.Sin(num), 0f, Mathf.Cos(num));
		}
		return array;
	}

	private void Start()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		_gravity = 0f - Physics.gravity.y;
		_positions = (Vector3[])(object)new Vector3[ConfigManager.TrajectoryPoints.Value];
		_playerVelocity = Vector3.zero;
		Plugin.log.LogInfo((object)"[Trajectory] TrajectoryRenderer 已启动");
	}

	private void EnsureLinesCreated()
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Expected O, but got Unknown
		if (!((Object)(object)_line != (Object)null))
		{
			_line = ((Component)this).gameObject.AddComponent<LineRenderer>();
			_line.useWorldSpace = true;
			Shader val = Shader.Find("Sprites/Default");
			if ((Object)(object)val == (Object)null)
			{
				val = Shader.Find("Unlit/Color");
			}
			((Renderer)_line).material = new Material(val);
			_line.numCapVertices = 1;
			_line.numCornerVertices = 2;
			_line.startWidth = ConfigManager.TrajectoryLineWidth.Value * 3f;
			_line.endWidth = ConfigManager.TrajectoryLineWidth.Value * 1.5f;
			ApplyLineGradient();
			((Renderer)_line).enabled = false;
			_landingCenterObj = GameObject.CreatePrimitive((PrimitiveType)0);
			_landingCenterObj.GetComponent<Collider>().enabled = false;
			_landingCenterRenderer = _landingCenterObj.GetComponent<Renderer>();
			_landingCenterRenderer.material = new Material(val);
			_landingCenterRenderer.enabled = false;
			_landingRingRoot = new GameObject("LandingRingRoot").transform;
			_landingRing = ((Component)_landingRingRoot).gameObject.AddComponent<LineRenderer>();
			_landingRing.useWorldSpace = true;
			_ringMaterial = new Material(val)
			{
				color = ConfigManager.LandingPointColor.Value
			};
			((Renderer)_landingRing).material = _ringMaterial;
			_landingRing.positionCount = _ringPoints.Length;
			_landingRing.SetPositions(_ringPoints);
			_landingRing.loop = true;
			_landingRing.numCapVertices = 0;
			_landingRing.numCornerVertices = 2;
			_landingRing.startWidth = 0.035f;
			_landingRing.endWidth = 0.035f;
			((Renderer)_landingRing).enabled = false;
			Plugin.log.LogInfo((object)"[Trajectory] 抛物线主线 + 落点圆环/中心点 已创建");
		}
	}

	private void ApplyLineGradient()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		Color value = ConfigManager.TrajectoryColor.Value;
		Color value2 = ConfigManager.LandingPointColor.Value;
		if (_gradientDirty || !(value == _lastGradStart) || !(value2 == _lastGradEnd))
		{
			_gradientDirty = false;
			_lastGradStart = value;
			_lastGradEnd = value2;
			float num = default(float);
			float num2 = default(float);
			float num3 = default(float);
			Color.RGBToHSV(value, ref num, ref num2, ref num3);
			float num4 = default(float);
			float num5 = default(float);
			float num6 = default(float);
			Color.RGBToHSV(value2, ref num4, ref num5, ref num6);
			float num7 = ((num4 - num) % 1f + 1f) % 1f;
			GradientColorKey[] array = (GradientColorKey[])(object)new GradientColorKey[7]
			{
				new GradientColorKey(value, 0f),
				default(GradientColorKey),
				default(GradientColorKey),
				default(GradientColorKey),
				default(GradientColorKey),
				default(GradientColorKey),
				new GradientColorKey(value2, 1f)
			};
			for (int i = 1; i < 6; i++)
			{
				float num8 = (float)i / 6f;
				float num9 = num + num7 * num8;
				float num10 = Mathf.Lerp(num2, num5, num8);
				float num11 = Mathf.Lerp(num3, num6, num8);
				array[i] = new GradientColorKey(Color.HSVToRGB(num9, num10, num11), num8);
			}
			LineRenderer line = _line;
			Gradient val = new Gradient();
			val.colorKeys = array;
			val.alphaKeys = (GradientAlphaKey[])(object)new GradientAlphaKey[3]
			{
				new GradientAlphaKey(0f, 0f),
				new GradientAlphaKey(0.6f, 0.5f),
				new GradientAlphaKey(1f, 1f)
			};
			line.colorGradient = val;
		}
	}

	private void HideLanding()
	{
		if ((Object)(object)_landingRing != (Object)null)
		{
			((Renderer)_landingRing).enabled = false;
		}
		if ((Object)(object)_landingCenterRenderer != (Object)null)
		{
			_landingCenterRenderer.enabled = false;
		}
		_ringAnimT = -1f;
	}

	private static FieldInfo FindGrenadePrefabField(Type controllerType)
	{
		if (_grenadePrefabFieldChecked)
		{
			return _grenadePrefabField;
		}
		_grenadePrefabFieldChecked = true;
		Type type = controllerType;
		while (type != null && type != typeof(object))
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.FieldType == typeof(GrenadePrefab))
				{
					_grenadePrefabField = fieldInfo;
					Plugin.log.LogInfo((object)("[Trajectory] 找到 GrenadePrefab 字段: " + fieldInfo.DeclaringType?.Name + "." + fieldInfo.Name));
					return _grenadePrefabField;
				}
			}
			type = type.BaseType;
		}
		Plugin.log.LogWarning((object)"[Trajectory] 未找到 GrenadePrefab 字段，将使用默认质量 0.55kg");
		return null;
	}

	private GrenadeThrow CalculateGrenadeThrow(Player player, bool lowThrow)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		float num = (lowThrow ? 0.66f : (1f + FloatBuff.op_Implicit(player.Skills.StrengthBuffThrowDistanceInc)));
		float grenadeForce = EFTHardSettings.Instance.GrenadeForce;
		Transform original = player.PlayerBones.WeaponRoot.Original;
		if (!BooleanBuff.op_Implicit(player.Skills.ThrowingEliteBuff))
		{
			float normalValue = player.Physical.HandsStamina.NormalValue;
			num *= Mathf.Lerp(0.4f, 1f, normalValue + 0.5f);
		}
		Vector3 val = -original.up;
		Vector3 throwForce = val * (grenadeForce * num) + _playerVelocity;
		Vector3 throwPosition = original.position + 0.5f * val;
		return new GrenadeThrow
		{
			ThrowPosition = throwPosition,
			ThrowForce = throwForce
		};
	}

	private static bool GetBallisticArcWithLinearDrag(Vector3[] positions, Vector3 startPosition, Vector3 initialVelocity, float intervalDistance, float maxDistance, float gravity, float linearDragCoefficient, out int positionCount)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		positions[num] = startPosition;
		num++;
		float num2 = linearDragCoefficient;
		if (num2 < 0.0001f)
		{
			num2 = 0.0001f;
		}
		float x = initialVelocity.x;
		float y = initialVelocity.y;
		float z = initialVelocity.z;
		float num3 = Mathf.Sqrt(x * x + z * z);
		if (num3 < 0.001f)
		{
			positionCount = num;
			return false;
		}
		float num4 = num3 / num2;
		float num5 = intervalDistance;
		Vector3 val = default(Vector3);
		RaycastHit val4 = default(RaycastHit);
		while (num5 <= maxDistance && num5 < num4 * 0.999f && num < positions.Length)
		{
			float num6 = num5 * num2 / num3;
			float num7 = (0f - Mathf.Log(1f - num6)) / num2;
			float num8 = Mathf.Exp((0f - num2) * num7);
			float num9 = startPosition.x + x / num2 * (1f - num8);
			float num10 = startPosition.y + 1f / num2 * ((y + gravity / num2) * (1f - num8) - gravity * num7);
			float num11 = startPosition.z + z / num2 * (1f - num8);
			((Vector3)(ref val))._002Ector(num9, num10, num11);
			Vector3 val2 = positions[num - 1];
			Vector3 val3 = val - val2;
			if (Physics.SphereCast(val2, 0.05f, ((Vector3)(ref val3)).normalized, ref val4, ((Vector3)(ref val3)).magnitude, GetHitMask()))
			{
				positions[num] = ((RaycastHit)(ref val4)).point;
				positionCount = num + 1;
				return true;
			}
			positions[num] = val;
			num5 += intervalDistance;
			num++;
		}
		positionCount = num;
		return false;
	}

	private void LateUpdate()
	{
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0391: Unknown result type (might be due to invalid IL or missing references)
		//IL_0396: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0432: Unknown result type (might be due to invalid IL or missing references)
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_050f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Unknown result type (might be due to invalid IL or missing references)
		//IL_0514: Unknown result type (might be due to invalid IL or missing references)
		//IL_0516: Unknown result type (might be due to invalid IL or missing references)
		//IL_0518: Unknown result type (might be due to invalid IL or missing references)
		//IL_051a: Unknown result type (might be due to invalid IL or missing references)
		//IL_051f: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0540: Unknown result type (might be due to invalid IL or missing references)
		//IL_055c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0573: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_052f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0534: Unknown result type (might be due to invalid IL or missing references)
		//IL_0638: Unknown result type (might be due to invalid IL or missing references)
		//IL_063f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0646: Unknown result type (might be due to invalid IL or missing references)
		//IL_0655: Unknown result type (might be due to invalid IL or missing references)
		//IL_065a: Unknown result type (might be due to invalid IL or missing references)
		//IL_067e: Unknown result type (might be due to invalid IL or missing references)
		if (!ConfigManager.EnableTrajectory.Value)
		{
			if ((Object)(object)_line != (Object)null)
			{
				((Renderer)_line).enabled = false;
			}
			HideLanding();
			return;
		}
		int num = Mathf.Max(2, ConfigManager.TrajectoryPoints.Value);
		if (_positions == null || _positions.Length != num)
		{
			_positions = (Vector3[])(object)new Vector3[num];
			_hasComputed = false;
		}
		if ((Object)(object)_cachedGameWorld == (Object)null)
		{
			_cachedGameWorld = Singleton<GameWorld>.Instance;
		}
		GameWorld cachedGameWorld = _cachedGameWorld;
		if ((Object)(object)cachedGameWorld == (Object)null)
		{
			if ((Object)(object)_line != (Object)null)
			{
				((Renderer)_line).enabled = false;
			}
			HideLanding();
			return;
		}
		Player mainPlayer = cachedGameWorld.MainPlayer;
		if ((Object)(object)mainPlayer == (Object)null || mainPlayer.HealthController == null || !mainPlayer.HealthController.IsAlive)
		{
			if ((Object)(object)_line != (Object)null)
			{
				((Renderer)_line).enabled = false;
			}
			HideLanding();
			return;
		}
		_playerVelocity = Vector3.Lerp(mainPlayer.Velocity, _playerVelocity, 0.9f);
		AbstractHandsController handsController = mainPlayer.HandsController;
		GrenadeHandsController val = (GrenadeHandsController)(object)((handsController is GrenadeHandsController) ? handsController : null);
		if ((Object)(object)val == (Object)null)
		{
			string text = ((object)mainPlayer.HandsController)?.GetType().Name ?? "null";
			if (text != _lastControllerTypeName)
			{
				_lastControllerTypeName = text;
				Plugin.log.LogInfo((object)("[Trajectory] 未手持手雷 (当前控制器: " + text + ")"));
			}
			if ((Object)(object)_line != (Object)null)
			{
				((Renderer)_line).enabled = false;
			}
			HideLanding();
			return;
		}
		string name = ((object)val).GetType().Name;
		if (name != _lastControllerTypeName)
		{
			_lastControllerTypeName = name;
			Plugin.log.LogInfo((object)("[Trajectory] 检测到手雷控制器: " + name));
		}
		bool waitingForLowThrow = val.WaitingForLowThrow;
		bool flag = val.WaitingForHighThrow | waitingForLowThrow;
		if (!((ConfigManager.TrajectoryDisplayMode.Value == 0) | flag))
		{
			if ((Object)(object)_line != (Object)null)
			{
				((Renderer)_line).enabled = false;
			}
			HideLanding();
			return;
		}
		EnsureLinesCreated();
		if (val.Item != null && _itemName != ((Item)val.Item).Name)
		{
			GrenadePrefab val2 = null;
			FieldInfo fieldInfo = FindGrenadePrefabField(((object)val).GetType());
			if (fieldInfo != null)
			{
				try
				{
					object value = fieldInfo.GetValue(val);
					val2 = (GrenadePrefab)((value is GrenadePrefab) ? value : null);
				}
				catch (Exception ex)
				{
					Plugin.log.LogWarning((object)("[Trajectory] 读取 GrenadePrefab 字段失败: " + ex.Message));
				}
			}
			if ((Object)(object)val2 != (Object)null && (Object)(object)val2.GrenadeItself != (Object)null && (Object)(object)((Component)val2.GrenadeItself).gameObject != (Object)null)
			{
				Rigidbody component = ((Component)val2.GrenadeItself).gameObject.GetComponent<Rigidbody>();
				if ((Object)(object)component != (Object)null)
				{
					_mass = component.mass;
				}
			}
			_itemName = ((Item)val.Item).Name;
			Plugin.log.LogInfo((object)$"[Trajectory] 手雷: {_itemName}, 质量: {_mass}kg");
		}
		GrenadeThrow grenadeThrow = CalculateGrenadeThrow(mainPlayer, waitingForLowThrow);
		ref Vector3 throwForce = ref grenadeThrow.ThrowForce;
		throwForce *= ConfigManager.ThrowForceMultiplier.Value;
		Vector3 val3 = grenadeThrow.ThrowForce / _mass;
		float value2 = ConfigManager.TrajectoryStepTime.Value;
		float maxDistance = (float)ConfigManager.TrajectoryPoints.Value * ConfigManager.TrajectoryStepTime.Value;
		int num2;
		if (_hasComputed)
		{
			Vector3 val4 = grenadeThrow.ThrowPosition - _lastThrowPos;
			if (!(((Vector3)(ref val4)).sqrMagnitude > 0.0036f))
			{
				val4 = val3 - _lastThrowVelocity;
				num2 = ((((Vector3)(ref val4)).sqrMagnitude > 0.0064f) ? 1 : 0);
				goto IL_03cb;
			}
		}
		num2 = 1;
		goto IL_03cb;
		IL_03cb:
		bool flag2 = ((Vector3)(ref _playerVelocity)).sqrMagnitude > 0.0025000002f;
		bool flag3;
		int positionCount;
		if (((uint)num2 | (flag2 ? 1u : 0u)) != 0 || _recalcCounter <= 0)
		{
			flag3 = (_lastCollided = GetBallisticArcWithLinearDrag(_positions, grenadeThrow.ThrowPosition, val3, value2, maxDistance, _gravity, 0.1f, out positionCount));
			_lastPositionCount = positionCount;
			_lastThrowPos = grenadeThrow.ThrowPosition;
			_lastThrowVelocity = val3;
			_hasComputed = true;
			_recalcCounter = Math.Max(1, ConfigManager.TrajectoryRecalcFrames.Value);
		}
		else
		{
			_recalcCounter--;
			flag3 = _lastCollided;
			positionCount = _lastPositionCount;
		}
		ApplyLineGradient();
		_line.startWidth = ConfigManager.TrajectoryLineWidth.Value * 3f;
		_line.endWidth = ConfigManager.TrajectoryLineWidth.Value * 1.5f;
		_line.positionCount = positionCount;
		_line.SetPositions(_positions);
		((Renderer)_line).enabled = true;
		if (flag3 && positionCount > 0)
		{
			Vector3 val5 = _positions[positionCount - 1];
			Vector3 val6 = ((positionCount >= 2) ? _positions[positionCount - 2] : val5);
			Vector3 val7 = val5 - val6;
			if (((Vector3)(ref val7)).sqrMagnitude < 0.0001f)
			{
				val7 = Vector3.up;
			}
			((Vector3)(ref val7)).Normalize();
			_lastRingNormal = val7;
			_landingCenterRenderer.enabled = true;
			_landingCenterObj.transform.position = val5;
			_landingCenterRenderer.material.color = ConfigManager.LandingPointColor.Value;
			_landingCenterObj.transform.localScale = Vector3.one * Mathf.Max(0.06f, ConfigManager.LandingPointRadius.Value * 0.28f);
			float value3 = ConfigManager.LandingPointRadius.Value;
			_landingRingRoot.position = val5;
			_landingRingRoot.rotation = Quaternion.FromToRotation(Vector3.up, val7);
			if (_ringAnimT < 0f)
			{
				_ringAnimT = 0f;
			}
			_ringAnimT = Mathf.Min(1f, _ringAnimT + Time.deltaTime * 5f);
			float num3 = Mathf.Lerp(0.55f, 1f, _ringAnimT);
			_landingRingRoot.localScale = Vector3.one * value3 * num3;
			Color value4 = ConfigManager.LandingPointColor.Value;
			value4.a = Mathf.Lerp(0f, 0.65f, _ringAnimT);
			_ringMaterial.color = value4;
			((Renderer)_landingRing).enabled = true;
		}
		else
		{
			HideLanding();
		}
	}

	private static int GetHitMask()
	{
		if (_cachedHitMask >= 0)
		{
			return _cachedHitMask;
		}
		try
		{
			Assembly assembly = null;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly2 in assemblies)
			{
				if (assembly2.GetName().Name == "Assembly-CSharp")
				{
					assembly = assembly2;
					break;
				}
			}
			if (assembly != null)
			{
				Type[] types = assembly.GetTypes();
				foreach (Type type in types)
				{
					if (!type.IsClass || (!type.Name.Contains("LayerMask") && !type.Name.Contains("HitMask") && !type.Name.Contains("Layer")))
					{
						continue;
					}
					FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (FieldInfo fieldInfo in fields)
					{
						if (!(fieldInfo.Name == "HitMask") && !fieldInfo.FieldType.Name.Contains("Mask"))
						{
							continue;
						}
						object value = fieldInfo.GetValue(null);
						if (value != null)
						{
							PropertyInfo property = value.GetType().GetProperty("value");
							if (property != null && property.PropertyType == typeof(int))
							{
								_cachedHitMask = (int)property.GetValue(value);
								Plugin.log.LogInfo((object)$"[Trajectory] HitMask 获取成功: {type.Name}.{fieldInfo.Name} = {_cachedHitMask}");
								return _cachedHitMask;
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.log.LogWarning((object)("[Trajectory] 获取 HitMask 异常: " + ex.Message));
		}
		_cachedHitMask = -37;
		Plugin.log.LogWarning((object)$"[Trajectory] 使用兜底 HitMask: ~(IgnoreRaycast|UI) = {_cachedHitMask}");
		return _cachedHitMask;
	}

	private void OnDestroy()
	{
		if ((Object)(object)_line != (Object)null)
		{
			Object.Destroy((Object)(object)_line);
		}
		if ((Object)(object)_landingCenterObj != (Object)null)
		{
			Object.Destroy((Object)(object)_landingCenterObj);
		}
		if ((Object)(object)_landingRingRoot != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_landingRingRoot).gameObject);
		}
	}

	public TrajectoryRenderer()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		_ringAnimT = -1f;
		_lastRingNormal = Vector3.up;
		_mass = 0.55f;
		_gradientDirty = true;
		((MonoBehaviour)this)._002Ector();
	}
}
