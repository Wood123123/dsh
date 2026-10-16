using System;
using EFT;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(LocalGame), "Stop")]
internal static class RaidFinishPatch
{
	private static void Prefix(string profileId, ExitStatus exitStatus)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RaidEarnings.Finish(profileId, exitStatus);
		}
		catch (Exception ex)
		{
			RaidEarnings.Fail(ex);
		}
	}
}
