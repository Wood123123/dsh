using System.Collections.Generic;

namespace WTTClientCommonLib.Models;

public class SalvageConfig
{
	public string RequiredItemTpl { get; set; } = "";

	public float SalvageTime { get; set; } = 10f;

	public bool ConsumeRequiredItem { get; set; } = true;

	public List<SalvageRewardConfig> Rewards { get; set; } = new List<SalvageRewardConfig>();
}
