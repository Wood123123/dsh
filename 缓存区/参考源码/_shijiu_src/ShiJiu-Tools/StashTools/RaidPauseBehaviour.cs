using UnityEngine;

namespace StashTools;

public sealed class RaidPauseBehaviour : MonoBehaviour
{
	public void Update()
	{
		RaidPause.Tick();
	}

	public void OnDestroy()
	{
		RaidPause.Dispose();
	}

	public void OnApplicationQuit()
	{
		RaidPause.Dispose();
	}
}
