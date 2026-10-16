using UnityEngine;

namespace StashTools.Fika;

public sealed class FikaAdapterBehaviour : MonoBehaviour
{
	public void Update()
	{
		FikaPluginAdapter.Tick();
	}

	public void OnDestroy()
	{
		FikaPluginAdapter.Dispose();
	}

	public void OnApplicationQuit()
	{
		FikaPluginAdapter.Dispose();
	}
}
