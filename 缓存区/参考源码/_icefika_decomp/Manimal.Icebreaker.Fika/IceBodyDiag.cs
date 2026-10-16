using System;
using System.Text;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.Communications;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

internal class IceBodyDiag : MonoBehaviour
{
	private float _next;

	private float _nextState;

	internal static void Ensure()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		if (!((Object)(object)Object.FindObjectOfType<IceBodyDiag>() != (Object)null))
		{
			GameObject val = new GameObject("Icebreaker_FikaBodyDiag");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<IceBodyDiag>();
			FikaAddonPlugin.Log.LogDebug((object)"[BodyDiag] probe component created (raid-time)");
		}
	}

	private void Start()
	{
		FikaAddonPlugin.Log.LogDebug((object)"[BodyDiag] probe alive — Update ticking");
	}

	private void LogState()
	{
		if (Time.time < _nextState)
		{
			return;
		}
		_nextState = Time.time + 60f;
		try
		{
			IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
			FikaAddonPlugin.Log.LogDebug((object)($"[BodyDiag] state: iceGate={IceGate.On} manager={instance != null}" + " observed=" + ((instance != null && instance.ObservedPlayers != null) ? instance.ObservedPlayers.Count.ToString() : "n/a") + $" cam={(Object)(object)GameCam() != (Object)null}"));
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogDebug((object)("[BodyDiag] state failed: " + ex.Message));
		}
	}

	private void Update()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			LogState();
			if (!IceGate.On)
			{
				return;
			}
			if (FikaAddonPlugin.ProbeKey != null)
			{
				KeyboardShortcut value = FikaAddonPlugin.ProbeKey.Value;
				if (((KeyboardShortcut)(ref value)).IsDown())
				{
					Player val = PickObserved(byAim: true);
					if ((Object)(object)val != (Object)null)
					{
						Dump(val, "AIMED", 999);
						Profile profile = val.Profile;
						Toast("BodyDiag captured '" + ((profile != null) ? profile.Nickname : null) + "' — in the log");
					}
					else
					{
						Toast("BodyDiag: no observed player found");
					}
					return;
				}
			}
			if (!(Time.time < _next))
			{
				_next = Time.time + 20f;
				Player val2 = PickObserved(byAim: false);
				if ((Object)(object)val2 != (Object)null)
				{
					Dump(val2, "nearest", 6);
				}
			}
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogDebug((object)("[BodyDiag] failed: " + ex.Message));
		}
	}

	private static Camera GameCam()
	{
		try
		{
			CameraManager instance = CameraManager.Instance;
			if (instance != null && (Object)(object)instance.Camera != (Object)null)
			{
				return instance.Camera;
			}
		}
		catch
		{
		}
		return Camera.main;
	}

	private static Player PickObserved(bool byAim)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
		if (instance == null || instance.ObservedPlayers == null || instance.ObservedPlayers.Count == 0)
		{
			return null;
		}
		Camera val = GameCam();
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		Player result = null;
		float num = float.MaxValue;
		foreach (ObservedPlayer observedPlayer in instance.ObservedPlayers)
		{
			if (!((Object)(object)observedPlayer == (Object)null) && ((Player)observedPlayer).HealthController != null && ((Player)observedPlayer).HealthController.IsAlive)
			{
				Vector3 val2 = ((Player)observedPlayer).Position + Vector3.up - ((Component)val).transform.position;
				float num2 = (byAim ? (Vector3.Angle(((Component)val).transform.forward, val2) + ((Vector3)(ref val2)).magnitude * 0.05f) : ((Vector3)(ref val2)).sqrMagnitude);
				if (num2 < num)
				{
					num = num2;
					result = (Player)(object)observedPlayer;
				}
			}
		}
		return result;
	}

	private static void Dump(Player p, string how, int cap)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		Camera val = GameCam();
		float num = (((Object)(object)val != (Object)null) ? Vector3.Distance(((Component)val).transform.position, p.Position) : (-1f));
		StringBuilder stringBuilder = new StringBuilder();
		object[] obj = new object[6] { how, null, null, null, null, null };
		Profile profile = p.Profile;
		obj[1] = ((profile != null) ? profile.Nickname : null);
		obj[2] = num;
		obj[3] = ((Component)p).gameObject.activeInHierarchy;
		obj[4] = ((Component)p).gameObject.layer;
		obj[5] = p.PointOfView;
		stringBuilder.AppendLine(string.Format("[BodyDiag] ({0}) '{1}' dist={2:0}m goActive={3} layer={4} pov={5}", obj));
		int num2 = 0;
		SkinnedMeshRenderer[] componentsInChildren = ((Component)p).GetComponentsInChildren<SkinnedMeshRenderer>(true);
		foreach (SkinnedMeshRenderer val2 in componentsInChildren)
		{
			if (num2++ >= cap)
			{
				stringBuilder.AppendLine("  ...skinned capped");
				break;
			}
			stringBuilder.AppendLine(string.Format("  skin '{0}': en={1} act={2} off={3} layer={4} shadow={5} shader={6}", ((Object)val2).name, ((Renderer)val2).enabled, ((Component)val2).gameObject.activeInHierarchy, ((Renderer)val2).forceRenderingOff, ((Component)val2).gameObject.layer, ((Renderer)val2).shadowCastingMode, ((Object)(object)((Renderer)val2).sharedMaterial != (Object)null && (Object)(object)((Renderer)val2).sharedMaterial.shader != (Object)null) ? ((Object)((Renderer)val2).sharedMaterial.shader).name : "NULL"));
		}
		int num3 = 0;
		MeshRenderer[] componentsInChildren2 = ((Component)p).GetComponentsInChildren<MeshRenderer>(true);
		foreach (MeshRenderer val3 in componentsInChildren2)
		{
			if (num3++ >= 8)
			{
				stringBuilder.AppendLine("  ...mesh capped");
				break;
			}
			stringBuilder.AppendLine($"  mesh '{((Object)val3).name}': en={((Renderer)val3).enabled} act={((Component)val3).gameObject.activeInHierarchy} off={((Renderer)val3).forceRenderingOff} layer={((Component)val3).gameObject.layer}");
		}
		FikaAddonPlugin.Log.LogDebug((object)stringBuilder.ToString());
	}

	private static void Toast(string text)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			NotificationManager.DisplayMessageNotification(text, (ENotificationDurationType)0, (ENotificationIconType)0, (Color?)Color.cyan);
		}
		catch
		{
		}
	}
}
