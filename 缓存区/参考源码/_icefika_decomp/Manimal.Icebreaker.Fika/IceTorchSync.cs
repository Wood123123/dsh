using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Manimal.Icebreaker.Blowtorch;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

internal class IceTorchSync : MonoBehaviour
{
	private readonly Dictionary<int, bool> _hadTorch = new Dictionary<int, bool>();

	private float _next;

	internal static void Ensure()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		if (!((Object)(object)Object.FindObjectOfType<IceTorchSync>() != (Object)null))
		{
			GameObject val = new GameObject("Icebreaker_FikaTorchSync");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<IceTorchSync>();
		}
	}

	private void Update()
	{
		if (Time.time < _next)
		{
			return;
		}
		_next = Time.time + 0.5f;
		try
		{
			if (!IceGate.On)
			{
				return;
			}
			IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
			if (instance == null || instance.ObservedPlayers == null)
			{
				return;
			}
			foreach (ObservedPlayer observedPlayer in instance.ObservedPlayers)
			{
				if (!((Object)(object)observedPlayer == (Object)null))
				{
					bool flag = (Object)(object)((Player)observedPlayer).HandsController != (Object)null && BlowtorchIds.IsTorch(((Player)observedPlayer).HandsController.Item);
					_hadTorch.TryGetValue(((FikaPlayer)observedPlayer).NetId, out var value);
					if (flag != value)
					{
						_hadTorch[((FikaPlayer)observedPlayer).NetId] = flag;
						BlowtorchController.PokeBodyAction((Player)(object)observedPlayer, flag ? 1 : 2);
						ManualLogSource log = FikaAddonPlugin.Log;
						string[] obj = new string[5] { "[TorchSync] '", null, null, null, null };
						Profile profile = ((Player)observedPlayer).Profile;
						obj[1] = ((profile != null) ? profile.Nickname : null);
						obj[2] = "' ";
						obj[3] = (flag ? "drew" : "holstered");
						obj[4] = " the torch — body pose poked";
						log.LogInfo((object)string.Concat(obj));
					}
				}
			}
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogWarning((object)("[TorchSync] watcher failed: " + ex.Message));
		}
	}

	internal static void SetRemoteFlame(int netId, bool on)
	{
		try
		{
			IFikaNetworkManager instance = Singleton<IFikaNetworkManager>.Instance;
			if (instance == null || instance.ObservedPlayers == null)
			{
				return;
			}
			foreach (ObservedPlayer observedPlayer in instance.ObservedPlayers)
			{
				if ((Object)(object)observedPlayer == (Object)null || ((FikaPlayer)observedPlayer).NetId != netId)
				{
					continue;
				}
				if (!((Object)(object)((Player)observedPlayer).HandsController == (Object)null) && BlowtorchIds.IsTorch(((Player)observedPlayer).HandsController.Item))
				{
					Animator val = (((Object)(object)((Player)observedPlayer).HandsController.ControllerGameObject != (Object)null) ? ((Player)observedPlayer).HandsController.ControllerGameObject.GetComponentInChildren<Animator>(true) : null);
					if (!((Object)(object)val == (Object)null))
					{
						val.SetBool("Firing", on);
						int num = Mathf.Min(1, val.layerCount - 1);
						val.CrossFade(on ? "torch_start" : "torch_end", 0.05f, num);
					}
				}
				break;
			}
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogWarning((object)("[TorchSync] remote flame failed: " + ex.Message));
		}
	}
}
