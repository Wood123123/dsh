using System;
using System.Collections;
using UnityEngine;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Components;

public class CharacterAudioHandler : MonoBehaviour
{
	private AudioSource _audioSource;

	private bool _isInitialized;

	private bool _isFadingIn;

	private bool _isFadingOut;

	private Coroutine _fadeCoroutine;

	public void Initialize(string faceName)
	{
		LogHelper.LogDebug("CharacterAudioHandler.Initialize called with faceName=" + faceName);
		string audioForFace = ResourceLoader.GetAudioForFace(faceName);
		if (string.IsNullOrEmpty(audioForFace))
		{
			LogHelper.LogDebug("AudioKey is empty for face " + faceName);
			return;
		}
		if (!ResourceLoader.ClipCache.TryGetAudioClip(audioForFace, out AudioClip clip))
		{
			LogHelper.LogDebug("Audio clip not in cache: " + audioForFace);
			return;
		}
		LogHelper.LogDebug("Found audio clip in cache: " + audioForFace);
		LogHelper.LogDebug($"Clip details - duration: {clip.length}s, samples: {clip.samples}, channels: {clip.channels}");
		AssignAudioToSource(clip, faceName);
		RadioSettings.FaceCardVolume.SettingChanged += OnFaceCardVolumeChanged;
	}

	private void OnDestroy()
	{
		RadioSettings.FaceCardVolume.SettingChanged -= OnFaceCardVolumeChanged;
	}

	private void OnFaceCardVolumeChanged(object sender, EventArgs e)
	{
		if (_isInitialized && !((Object)(object)_audioSource == (Object)null) && _audioSource.isPlaying && !_isFadingOut)
		{
			_audioSource.volume = RadioSettings.FaceCardVolume.Value;
			LogHelper.LogDebug($"Updated audio volume: {_audioSource.volume}");
		}
	}

	public void AssignAudioToSource(AudioClip audioClip, string originalName)
	{
		LogHelper.LogDebug("AssignAudioToSource: clip=" + ((audioClip != null) ? ((Object)audioClip).name : null) + ", name=" + originalName);
		_audioSource = ((Component)this).gameObject.GetComponent<AudioSource>();
		if ((Object)(object)_audioSource == (Object)null)
		{
			_audioSource = ((Component)this).gameObject.AddComponent<AudioSource>();
			LogHelper.LogDebug("Created new AudioSource");
		}
		else
		{
			LogHelper.LogDebug("Reusing existing AudioSource");
		}
		_audioSource.clip = audioClip;
		_audioSource.loop = true;
		_audioSource.volume = 0f;
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;
		_isInitialized = true;
		LogHelper.LogDebug($"Audio source configured: spatialBlend={_audioSource.spatialBlend}, volume={_audioSource.volume}");
	}

	public void FadeIn()
	{
		if (!_isInitialized)
		{
			LogHelper.LogWarn("FadeIn called but handler not initialized");
			return;
		}
		AudioSource audioSource = _audioSource;
		LogHelper.LogDebug($"FadeIn: Starting fade-in, current volume: {((audioSource != null) ? new float?(audioSource.volume) : ((float?)null))}");
		if (_fadeCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(_fadeCoroutine);
			LogHelper.LogDebug("Stopped previous fade coroutine");
		}
		_fadeCoroutine = ((MonoBehaviour)this).StartCoroutine(FadeAudio(RadioSettings.FaceCardVolume.Value, 5f));
	}

	public void FadeOut()
	{
		if (!_isInitialized)
		{
			LogHelper.LogWarn("FadeOut called but handler not initialized");
			return;
		}
		AudioSource audioSource = _audioSource;
		LogHelper.LogDebug($"FadeOut: Starting fade-out, current volume: {((audioSource != null) ? new float?(audioSource.volume) : ((float?)null))}");
		if (_fadeCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(_fadeCoroutine);
			LogHelper.LogDebug("Stopped previous fade coroutine");
		}
		_fadeCoroutine = ((MonoBehaviour)this).StartCoroutine(FadeAudio(0f, 5f));
	}

	private IEnumerator FadeAudio(float targetVolume, float duration)
	{
		if ((Object)(object)_audioSource == (Object)null || (Object)(object)_audioSource.clip == (Object)null)
		{
			LogHelper.LogWarn("FadeAudio: AudioSource or clip is null");
			yield break;
		}
		_isFadingIn = targetVolume > 0f;
		_isFadingOut = targetVolume == 0f;
		LogHelper.LogDebug($"FadeAudio started: targetVolume={targetVolume}, duration={duration}, isFadingIn={_isFadingIn}");
		float startVolume = _audioSource.volume;
		float time = 0f;
		if (_isFadingIn && !_audioSource.isPlaying)
		{
			_audioSource.Play();
			LogHelper.LogDebug("Audio started playing");
		}
		while (time < duration && (Object)(object)_audioSource != (Object)null)
		{
			time += Time.deltaTime;
			float num = (_isFadingIn ? RadioSettings.FaceCardVolume.Value : targetVolume);
			_audioSource.volume = Mathf.Lerp(startVolume, num, time / duration);
			yield return null;
		}
		if ((Object)(object)_audioSource != (Object)null)
		{
			_audioSource.volume = (_isFadingIn ? RadioSettings.FaceCardVolume.Value : targetVolume);
			LogHelper.LogDebug($"FadeAudio completed: final volume={_audioSource.volume}, isFadingOut={_isFadingOut}");
			if (_isFadingOut)
			{
				_audioSource.Stop();
				LogHelper.LogDebug("Audio stopped");
			}
		}
		_isFadingIn = false;
		_isFadingOut = false;
	}
}
