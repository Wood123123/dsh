using System.Collections.Generic;
using UnityEngine;
using WTTClientCommonLib.Helpers;

public class AudioClipCache : MonoBehaviour
{
	private Dictionary<string, AudioClip> _clipMap = new Dictionary<string, AudioClip>();

	private Dictionary<string, float[]> _audioDataMap = new Dictionary<string, float[]>();

	public void CacheAudioClip(string key, AudioClip clip)
	{
		if (!Object.op_Implicit((Object)(object)clip))
		{
			LogHelper.LogWarn("Attempted to cache null clip for key: " + key);
			return;
		}
		((Object)clip).hideFlags = (HideFlags)32;
		_clipMap[key] = clip;
		float[] array = new float[clip.samples * clip.channels];
		clip.GetData(array, 0);
		_audioDataMap[key] = array;
		LogHelper.LogDebug($"Cached audio clip: {key}, duration: {clip.length}s, data samples: {array.Length}");
	}

	public bool TryGetAudioClip(string key, out AudioClip clip)
	{
		if (_clipMap.TryGetValue(key, out clip) && (Object)(object)clip != (Object)null)
		{
			LogHelper.LogDebug($"Retrieved clip: {key}, duration: {clip.length}s");
			return true;
		}
		return false;
	}

	public void Clear()
	{
		_clipMap.Clear();
		_audioDataMap.Clear();
	}
}
