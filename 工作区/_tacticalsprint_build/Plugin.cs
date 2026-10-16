using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FromHell.TacticalSprint;

[BepInPlugin("com.fromhell.tacticalsprint", "FROM HELL - Tactical Sprint", "0.1.7")]
public sealed class Plugin : BaseUnityPlugin
{
	public const string Id = "com.fromhell.tacticalsprint";

	private static Plugin? Self;
	public static bool IsTacticalSprinting
	{
		get
		{
			Plugin self = Self;
			return (Object)(object)self != (Object)null && self.gate.Active;
		}
	}

	public static event Action<bool> OnTacticalSprintChanged;

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

	private void Awake()
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Expected O, but got Unknown
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Expected O, but got Unknown
		Self = this;
		Enabled = Config.Bind<bool>("General", "Enabled", true, "Enable tactical sprint. Sprint key and toggle/hold mode are controlled by this plugin while holding a firearm.");
		sprintKey = Config.Bind<KeyCode>("Input", "SprintKey", (KeyCode)304, "Physical sprint key used by this plugin.");
		holdMode = Config.Bind<bool>("Input", "HoldToSprint", false, "false: tap toggles sprint. true: hold second tap to keep tactical sprint; release stops.");
		diagnostics = Config.Bind<bool>("Debug", "InputDiagnostics", true, "Log sprint key edges and state changes, not each frame.");
		Window = Config.Bind<float>("Input", "DoubleTapSeconds", 0.4f, new ConfigDescription("Maximum interval between sprint presses.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.15f, 0.6f), Array.Empty<object>()));
		Speed = Config.Bind<float>("Movement", "SpeedMultiplier", 1.25f, new ConfigDescription("Tactical sprint target speed multiplier.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 10f), Array.Empty<object>()));
		Drain = Config.Bind<float>("Movement", "StaminaMultiplier", 2f, new ConfigDescription("Tactical sprint leg-stamina drain relative to vanilla sprint. Does not scale other drains.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 5f), Array.Empty<object>()));
		if (typeof(Player).Module.ModuleVersionId != new Guid("cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7"))
		{
			Logger.LogError((object)"Unsupported client build. No patches installed.");
			return;
		}
		if (AppDomain.CurrentDomain.GetAssemblies().Any((Assembly a) => a.GetName().Name == "西欧弟式持枪"))
		{
			Logger.LogError((object)"Remove 西欧弟式持枪.dll and restart: it globally forces the raised animation. Tactical Sprint disabled.");
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
			Logger.LogInfo((object)"Tactical Sprint 0.1.6 (SPT 4.1.5) loaded. Physical-key double tap; plugin HoldToSprint controls mode. InputDiagnostics enabled by default.");
			Logger.LogInfo((object)"制作由[来自地狱]死神少爷是只兔");
		}
		catch (Exception ex)
		{
			Harmony? obj = harmony;
			if (obj != null)
			{
				obj.UnpatchSelf();
			}
			Logger.LogError((object)ex);
		}
	}

	private bool Eligible(Player p)
	{
		if (ready && ((Behaviour)this).isActiveAndEnabled && Enabled.Value && p.IsYourPlayer && p.HandsController is Player.FirearmController && !p.IsInventoryOpened)
		{
			return p.HealthController.IsAlive;
		}
		return false;
	}

	private bool Tactical(Player p)
	{
		if (player == p && gate.Active && Eligible(p))
		{
			return p.Physical.Sprinting;
		}
		return false;
	}

	private void Bind(Player p)
	{
		if (player != p)
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
			Logger.LogInfo((object)("Input: " + message));
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
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
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
		if (ready)
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			Player val = (((Object)(object)instance != (Object)null) ? instance.MainPlayer : null);
			if (!((Object)(object)val == (Object)null) && Eligible(val))
			{
				Bind(val);
				ReadInput(val);
			}
		}
	}

	private static bool OnToggle(Player __instance)
	{
		Plugin self = Self;
		if ((Object)(object)self == (Object)null || self.issuing || !self.Eligible(__instance))
		{
			return true;
		}
		self.Bind(__instance);
		self.ReadInput(__instance);
		self.Trace("suppressed vanilla ToggleSprint");
		return false;
	}

	private unsafe static bool OnCommand(MoveInputTranslator __instance, ECommand command, ref InputNode.ETranslateResult __result)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		Plugin self = Self;
		Player controller = __instance._controller;
		if ((Object)(object)self == (Object)null || (Object)(object)controller == (Object)null || !self.Eligible(controller))
		{
			return true;
		}
		if ((int)command != 23 && (int)command != 24)
		{
			return true;
		}
		self.Bind(controller);
		self.ReadInput(controller);
		__result = (InputNode.ETranslateResult)0;
		self.Trace("suppressed command=" + ((object)(*(ECommand*)(&command))/*cast due to constrained. prefix*/).ToString());
		return false;
	}

	private void LateUpdate()
	{
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		if (!ready)
		{
			return;
		}
		GameWorld instance = Singleton<GameWorld>.Instance;
		Player val = (((Object)(object)instance != (Object)null) ? instance.MainPlayer : null);
		if ((Object)(object)val == (Object)null)
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
			nativeSize = ((Item)((Player.FirearmController)val.HandsController).Item).CalculateCellSize().X;
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
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		if ((Object)(object)player == (Object)null || !Tactical(player))
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
			Physical val = (Physical)(object)((physical is Physical) ? physical : null);
			if (val != null && val.Consumptions.TryGetValue((Physical.EConsumptionType)1, out var baseline))
			{
				Physical.Consumption val2 = new Physical.Consumption((Physical.EConsumptionType)1)
				{
					Delta = new Compute<float>((Func<float>)(() => (!Tactical(p)) ? 0f : (Math.Max(0f, baseline.Delta.Value) * (Safe(Drain.Value, 1f, 5f) - 1f)))),
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
		if (wrote && animator != null && (Object)(object)player != (Object)null && hands == player.HandsController)
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
		Harmony? obj = harmony;
		if (obj != null)
		{
			obj.UnpatchSelf();
		}
		if (Self == this)
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

	private static float nextSpeedLog;

	private static void LogSpeed(string msg)
	{
		try
		{
			if (Time.unscaledTime < nextSpeedLog)
			{
				return;
			}
			nextSpeedLog = Time.unscaledTime + 1f;
			Plugin self = Self;
			if ((Object)(object)self != (Object)null)
			{
				self.Logger.LogInfo((object)("[Speed] " + msg));
			}
		}
		catch
		{
		}
	}

	private static void ApplyMotionPrefix(MovementState __instance, ref Vector3 motion, float deltaTime)
	{
		try
		{
			GameWorld gameWorld = Singleton<GameWorld>.Instance;
			if ((Object)(object)gameWorld == (Object)null || (Object)(object)gameWorld.MainPlayer == (Object)null)
			{
				return;
			}
			Player player = (Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance.MovementContext);
			if ((Object)(object)player == (Object)null)
			{
				return;
			}
			bool isMain = (Object)(object)player == (Object)(object)gameWorld.MainPlayer;
			if (!isMain)
			{
				return;
			}
			bool sprint = __instance.MovementContext.IsSprintEnabled;
			bool active = (Object)(object)Self != (Object)null && Self.gate.Active;
			if (sprint && active)
			{
				float mult = Safe(Self.Speed.Value, 1f, 10f);
				motion *= mult;
				LogSpeed("加速 x" + mult);
			}
			else
			{
				// [日志噪音已禁用] 原本这里每帧打 "本地未加速"，已移除以免刷屏
			}
		}
		catch (Exception e)
		{
			LogSpeed("异常 " + e.GetType().Name + ":" + e.Message);
		}
	}

	private static void ApplyMotionPostfix(MovementState __instance, Vector3 motion, float deltaTime)
	{
		try
		{
			GameWorld gameWorld = Singleton<GameWorld>.Instance;
			if ((Object)(object)gameWorld == (Object)null || (Object)(object)gameWorld.MainPlayer == (Object)null)
			{
				return;
			}
			Player player = (Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance.MovementContext);
			if ((Object)(object)player != (Object)(object)gameWorld.MainPlayer)
			{
				return;
			}
			Plugin self = Self;
			if ((Object)(object)self == (Object)null || !self.gate.Active || !__instance.MovementContext.IsSprintEnabled)
			{
				return;
			}
			float mult = Safe(self.Speed.Value, 1f, 10f);
			Vector3 extra = new Vector3(motion.x, motion.y, motion.z) * (mult - 1f);
			self.player.Transform.position += extra * deltaTime;
		}
		catch
		{
		}
	}

	private static void ClampedSpeedPostfix(MovementContext __instance, ref float __result)
	{
		try
		{
			GameWorld gameWorld = Singleton<GameWorld>.Instance;
			if ((Object)(object)gameWorld == (Object)null || (Object)(object)gameWorld.MainPlayer == (Object)null)
			{
				return;
			}
			Player player = (Player)AccessTools.Field(typeof(MovementContext), "_player").GetValue(__instance);
			if ((Object)(object)player != (Object)(object)gameWorld.MainPlayer)
			{
				return;
			}
			Plugin self = Self;
			if ((Object)(object)self == (Object)null || !self.gate.Active || !__instance.IsSprintEnabled)
			{
				return;
			}
			__result *= Safe(self.Speed.Value, 1f, 10f);
		}
		catch
		{
		}
	}
}
