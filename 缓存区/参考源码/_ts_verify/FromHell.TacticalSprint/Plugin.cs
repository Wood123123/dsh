using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace FromHell.TacticalSprint;

[BepInPlugin("com.fromhell.tacticalsprint", "FROM HELL - Tactical Sprint", "0.1.7")]
public sealed class Plugin : BaseUnityPlugin
{
	public const string Id = "com.fromhell.tacticalsprint";

	private static Plugin? Self;

	private ConfigEntry<bool> Enabled;

	private ConfigEntry<float> Window;

	private ConfigEntry<float> Speed;

	private ConfigEntry<float> Drain;

	private Harmony? harmony;

	private Player? player;

	private object? hands;

	private object? animator;

	private static readonly MethodInfo GetAnimator = AccessTools.PropertyGetter(typeof(Player), "BodyAnimatorCommon");

	private MethodInfo? setFloat;

	private readonly object[] animArgs = new object[2];

	private float nativeSize;

	private bool wrote;

	private bool ready;

	private Action? removeDrain;

	private readonly SprintGate gate = new SprintGate();

	private static bool lastTactical;

	private ConfigEntry<KeyCode> sprintKey;

	private ConfigEntry<bool> holdMode;

	private ConfigEntry<bool> diagnostics;

	private int inputFrame = -1;

	private float pendingUntil;

	private bool issuing;

	private static float nextSpeedLog;

	public static bool IsTacticalSprinting
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Expected O, but got Unknown
			Plugin self = Self;
			if ((Object)self != (Object)null)
			{
				return self.gate.Active;
			}
			return false;
		}
	}

	public static event Action<bool> OnTacticalSprintChanged;

	private void Awake()
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Expected O, but got Unknown
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Expected O, but got Unknown
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Expected O, but got Unknown
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Expected O, but got Unknown
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Expected O, but got Unknown
		//IL_02de: Expected O, but got Unknown
		Self = this;
		Enabled = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Enabled", true, "Enable tactical sprint. Sprint key and toggle/hold mode are controlled by this plugin while holding a firearm.");
		sprintKey = ((BaseUnityPlugin)this).Config.Bind<KeyCode>("Input", "SprintKey", (KeyCode)304, "Physical sprint key used by this plugin.");
		holdMode = ((BaseUnityPlugin)this).Config.Bind<bool>("Input", "HoldToSprint", false, "false: tap toggles sprint. true: hold second tap to keep tactical sprint; release stops.");
		diagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "InputDiagnostics", true, "Log sprint key edges and state changes, not each frame.");
		Window = ((BaseUnityPlugin)this).Config.Bind<float>("Input", "DoubleTapSeconds", 0.4f, new ConfigDescription("Maximum interval between sprint presses.", (AcceptableValueBase)new AcceptableValueRange<float>(0.15f, 0.6f), Array.Empty<object>()));
		Speed = ((BaseUnityPlugin)this).Config.Bind<float>("Movement", "SpeedMultiplier", 1.25f, new ConfigDescription("Tactical sprint target speed multiplier.", (AcceptableValueBase)new AcceptableValueRange<float>(1f, 10f), Array.Empty<object>()));
		Drain = ((BaseUnityPlugin)this).Config.Bind<float>("Movement", "StaminaMultiplier", 2f, new ConfigDescription("Tactical sprint leg-stamina drain relative to vanilla sprint. Does not scale other drains.", (AcceptableValueBase)new AcceptableValueRange<float>(1f, 5f), Array.Empty<object>()));
		if (typeof(Player).Module.ModuleVersionId != new Guid("cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7"))
		{
			((BaseUnityPlugin)this).Logger.LogError((object)"Unsupported client build. No patches installed.");
			return;
		}
		if (AppDomain.CurrentDomain.GetAssemblies().Any((Assembly a) => a.GetName().Name == "西欧弟式持枪"))
		{
			((BaseUnityPlugin)this).Logger.LogError((object)"Remove 西欧弟式持枪.dll and restart: it globally forces the raised animation. Tactical Sprint disabled.");
			return;
		}
		try
		{
			harmony = new Harmony("com.fromhell.tacticalsprint");
			harmony.Patch((MethodBase)AccessTools.Method(typeof(Player), "ToggleSprint", (Type[])null, (Type[])null), new HarmonyMethod(typeof(Plugin), "OnToggle", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)AccessTools.Method(typeof(MoveInputTranslator), "TranslateCommand", (Type[])null, (Type[])null), new HarmonyMethod(typeof(Plugin), "OnCommand", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)AccessTools.Method(typeof(MovementContext), "get_ClampedSpeed", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(Plugin), "ClampedSpeedPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)AccessTools.Method(typeof(MovementState), "ApplyMotion", (Type[])null, (Type[])null), new HarmonyMethod(typeof(Plugin), "ApplyMotionPrefix", (Type[])null), new HarmonyMethod(typeof(Plugin), "ApplyMotionPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			ready = true;
			((BaseUnityPlugin)this).Logger.LogInfo((object)"Tactical Sprint 0.1.6 (SPT 4.1.5) loaded. Physical-key double tap; plugin HoldToSprint controls mode. InputDiagnostics enabled by default.");
			((BaseUnityPlugin)this).Logger.LogInfo((object)"制作由[来自地狱]死神少爷是只兔");
		}
		catch (Exception ex)
		{
			Harmony val = harmony;
			if (val != null)
			{
				val.UnpatchSelf();
			}
			((BaseUnityPlugin)this).Logger.LogError((object)ex);
		}
	}

	private bool Eligible(Player p)
	{
		if (ready && ((Behaviour)this).isActiveAndEnabled && Enabled.Value && p.IsYourPlayer && p.HandsController is FirearmController && !p.IsInventoryOpened)
		{
			return p.HealthController.IsAlive;
		}
		return false;
	}

	private bool Tactical(Player p)
	{
		if ((Object)(object)player == (Object)(object)p && gate.Active && Eligible(p))
		{
			return p.Physical.Sprinting;
		}
		return false;
	}

	private void Bind(Player p)
	{
		if ((Object)(object)player != (Object)(object)p)
		{
			Clear();
			player = p;
			hands = p.HandsController;
		}
	}

	private void Trace(string message)
	{
		if (diagnostics.Value)
		{
			((BaseUnityPlugin)this).Logger.LogInfo((object)("Input: " + message));
		}
	}

	private void Issue(Player p, bool value)
	{
		issuing = true;
		try
		{
			p.EnableSprint(value);
		}
		finally
		{
			issuing = false;
		}
	}

	private void ReadInput(Player p)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		if (inputFrame == Time.frameCount)
		{
			return;
		}
		inputFrame = Time.frameCount;
		if (Input.GetKeyDown(sprintKey.Value))
		{
			bool num = gate.Press(Time.unscaledTime, Window.Value, p.Physical.Sprinting);
			bool value = num || holdMode.Value || !p.Physical.Sprinting;
			if (num)
			{
				pendingUntil = Time.unscaledTime + 0.45f;
			}
			else
			{
				pendingUntil = 0f;
			}
			Issue(p, value);
			Trace("down; tactical=" + gate.Active + " requestSprint=" + value + " sprinting=" + p.Physical.Sprinting + " hold=" + holdMode.Value + " playerType=" + ((object)p).GetType().FullName);
		}
		if (holdMode.Value && Input.GetKeyUp(sprintKey.Value))
		{
			gate.Stop();
			pendingUntil = 0f;
			Issue(p, value: false);
			Trace("release -> stop (hold mode)");
		}
	}

	private void Update()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		if (ready)
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			Player val = (((Object)instance != (Object)null) ? instance.MainPlayer : null);
			if (!((Object)val == (Object)null) && Eligible(val))
			{
				Bind(val);
				ReadInput(val);
			}
		}
	}

	private static bool OnToggle(Player __instance)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		Plugin self = Self;
		if ((Object)self == (Object)null || self.issuing || !self.Eligible(__instance))
		{
			return true;
		}
		self.Bind(__instance);
		self.ReadInput(__instance);
		self.Trace("suppressed vanilla ToggleSprint");
		return false;
	}

	private static bool OnCommand(MoveInputTranslator __instance, ECommand command, ref ETranslateResult __result)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Invalid comparison between Unknown and I4
		Plugin self = Self;
		Player controller = __instance._controller;
		if ((Object)self == (Object)null || (Object)controller == (Object)null || !self.Eligible(controller))
		{
			return true;
		}
		if ((int)command != 23 && (int)command != 24)
		{
			return true;
		}
		self.Bind(controller);
		self.ReadInput(controller);
		__result = (ETranslateResult)0;
		self.Trace("suppressed command=" + ((object)command).ToString());
		return false;
	}

	private void LateUpdate()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		if (!ready)
		{
			return;
		}
		GameWorld instance = Singleton<GameWorld>.Instance;
		Player val = (((Object)instance != (Object)null) ? instance.MainPlayer : null);
		if ((Object)val == (Object)null)
		{
			Clear();
			return;
		}
		Bind(val);
		if (!Eligible(val))
		{
			gate.Reset();
			SyncDrain();
			Restore();
			return;
		}
		if (hands != val.HandsController)
		{
			gate.Reset();
			Restore();
			hands = val.HandsController;
		}
		if (!val.Physical.Sprinting && Time.unscaledTime >= pendingUntil)
		{
			if (gate.Active)
			{
				Trace("tactical ended: not sprinting");
			}
			gate.Stop();
		}
		SyncDrain();
		if (!val.Physical.Sprinting)
		{
			Restore();
			return;
		}
		object obj = GetAnimator.Invoke(val, null);
		if (obj != null)
		{
			if (animator != obj)
			{
				Restore();
				animator = obj;
				setFloat = AccessTools.Method(obj.GetType(), "SetFloat", new Type[2]
				{
					typeof(int),
					typeof(float)
				}, (Type[])null);
			}
			nativeSize = ((Item)((FirearmController)val.HandsController).Item).CalculateCellSize().X;
			WriteSize(gate.Active ? 2f : Math.Max(3f, nativeSize));
			wrote = true;
		}
		if (gate.Active != lastTactical)
		{
			lastTactical = gate.Active;
			OnTacticalSprintChanged?.Invoke(gate.Active);
		}
	}

	private void SyncDrain()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		if ((Object)player == (Object)null || !Tactical(player))
		{
			removeDrain?.Invoke();
			removeDrain = null;
		}
		else
		{
			if (removeDrain != null)
			{
				return;
			}
			Player p = player;
			PhysicalBase physical = p.Physical;
			Physical val = (Physical)((physical is Physical) ? physical : null);
			if (val != null && val.Consumptions.TryGetValue((EConsumptionType)1, out var baseline))
			{
				Consumption val2 = new Consumption((EConsumptionType)1)
				{
					Delta = new Compute<float>((Func<float>)(() => Tactical(p) ? (Math.Max(0f, baseline.Delta.Value) * (Safe(Drain.Value, 1f, 5f) - 1f)) : 0f)),
					AllowsRestoration = false,
					Downtime = baseline.Downtime
				};
				removeDrain = ((PhysicalBase)val).Stamina.AddConsumption(val2);
			}
		}
	}

	private void WriteSize(float value)
	{
		animArgs[0] = PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH;
		animArgs[1] = value;
		setFloat?.Invoke(animator, animArgs);
	}

	private void Restore()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		if (wrote && animator != null && (Object)player != (Object)null && hands == player.HandsController)
		{
			WriteSize(nativeSize);
		}
		wrote = false;
		animator = null;
	}

	private void Clear()
	{
		pendingUntil = 0f;
		inputFrame = -1;
		gate.Reset();
		removeDrain?.Invoke();
		removeDrain = null;
		Restore();
		hands = null;
		player = null;
	}

	private void OnDisable()
	{
		Clear();
	}

	private void OnDestroy()
	{
		ready = false;
		Clear();
		Harmony val = harmony;
		if (val != null)
		{
			val.UnpatchSelf();
		}
		if ((Object)(object)Self == (Object)(object)this)
		{
			Self = null;
		}
	}

	private static float Safe(float f, float min, float max)
	{
		if (!float.IsNaN(f) && !float.IsInfinity(f))
		{
			return Math.Max(min, Math.Min(max, f));
		}
		return min;
	}

	private static void LogSpeed(string msg)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		try
		{
			if (!(Time.unscaledTime < nextSpeedLog))
			{
				nextSpeedLog = Time.unscaledTime + 1f;
				Plugin self = Self;
				if ((Object)self != (Object)null)
				{
					((BaseUnityPlugin)self).Logger.LogInfo((object)("[Speed] " + msg));
				}
			}
		}
		catch
		{
		}
	}

	private static void ApplyMotionPrefix(MovementState __instance, ref Vector3 motion, float deltaTime)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_007a: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			if ((Object)instance == (Object)null || (Object)instance.MainPlayer == (Object)null)
			{
				return;
			}
			Player val = (Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance.MovementContext);
			if (!((Object)val == (Object)null) && (Object)val == (Object)instance.MainPlayer)
			{
				bool isSprintEnabled = __instance.MovementContext.IsSprintEnabled;
				bool flag = (Object)Self != (Object)null && Self.gate.Active;
				if (isSprintEnabled & flag)
				{
					float num = Safe(Self.Speed.Value, 1f, 10f);
					motion *= num;
					LogSpeed("加速 x" + num);
				}
			}
		}
		catch (Exception ex)
		{
			LogSpeed("异常 " + ex.GetType().Name + ":" + ex.Message);
		}
	}

	private static void ApplyMotionPostfix(MovementState __instance, Vector3 motion, float deltaTime)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0065: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			if (!((Object)instance == (Object)null) && !((Object)instance.MainPlayer == (Object)null) && !((Object)(Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance.MovementContext) != (Object)instance.MainPlayer))
			{
				Plugin self = Self;
				if (!((Object)self == (Object)null) && self.gate.Active && __instance.MovementContext.IsSprintEnabled)
				{
					float num = Safe(self.Speed.Value, 1f, 10f);
					Vector3 val = new Vector3(motion.x, motion.y, motion.z) * (num - 1f);
					BifacialTransform transform = self.player.Transform;
					transform.position += val * deltaTime;
				}
			}
		}
		catch
		{
		}
	}

	private static void ClampedSpeedPostfix(MovementContext __instance, ref float __result)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_0060: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		try
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			if (!((Object)instance == (Object)null) && !((Object)instance.MainPlayer == (Object)null) && !((Object)(Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance) != (Object)instance.MainPlayer))
			{
				Plugin self = Self;
				if (!((Object)self == (Object)null) && self.gate.Active && __instance.IsSprintEnabled)
				{
					__result *= Safe(self.Speed.Value, 1f, 10f);
				}
			}
		}
		catch
		{
		}
	}
}
