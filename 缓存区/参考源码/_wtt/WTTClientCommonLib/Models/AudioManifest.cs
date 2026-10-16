using System.Collections.Generic;

namespace WTTClientCommonLib.Models;

public class AudioManifest
{
	public List<string> AudioBundles { get; set; } = new List<string>();

	public Dictionary<string, FaceCardAudioEntry> FaceCardMappings { get; set; } = new Dictionary<string, FaceCardAudioEntry>();

	public List<string> RadioAudio { get; set; } = new List<string>();
}
