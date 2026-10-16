using System.Collections.Generic;

namespace WTTClientCommonLib.Models;

public class FaceCardAudioEntry
{
	public List<string> Audio { get; set; } = new List<string>();

	public bool PlayOnRadio { get; set; }
}
