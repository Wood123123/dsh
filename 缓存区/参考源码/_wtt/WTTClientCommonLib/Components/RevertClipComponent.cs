using System;
using UnityEngine;

namespace WTTClientCommonLib.Components;

public class RevertClipComponent : MonoBehaviour
{
	private AudioSource _audioSource;

	private AudioClip _originalClip;

	private Action _onRevert;

	public void Initialize(AudioSource audioSource, AudioClip originalClip, Action onRevert)
	{
		_audioSource = audioSource;
		_originalClip = originalClip;
		_onRevert = onRevert;
	}

	private void Update()
	{
		if (!Object.op_Implicit((Object)(object)_audioSource) || !Object.op_Implicit((Object)(object)_originalClip))
		{
			Cleanup();
		}
		else if (!_audioSource.isPlaying && (Object)(object)_audioSource.clip != (Object)(object)_originalClip)
		{
			_audioSource.clip = _originalClip;
			_onRevert?.Invoke();
			Cleanup();
		}
	}

	private void Cleanup()
	{
		_onRevert = null;
		Object.Destroy((Object)(object)this);
	}
}
