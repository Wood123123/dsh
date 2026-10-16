using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;

namespace NekoParty.Server.GroupEX.Patches;

public class SaveServerScavProfileFallbackPatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(SaveServer), "GetProfile", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static void Prefix(SaveServer __instance, ref MongoId sessionId)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (((MongoId)(ref sessionId)).IsEmpty || __instance.ProfileExists(sessionId))
			{
				return;
			}
			string b = ((object)Unsafe.As<MongoId, MongoId>(ref sessionId)).ToString();
			foreach (KeyValuePair<MongoId, SptProfile> profile in __instance.GetProfiles())
			{
				SptProfile value = profile.Value;
				object obj;
				if (value == (SptProfile)null)
				{
					obj = null;
				}
				else
				{
					Characters characterData = value.CharacterData;
					obj = ((characterData != (Characters)null) ? characterData.ScavData : null);
				}
				PmcData val = (PmcData)obj;
				if (!(val == (PmcData)null) && string.Equals(((BotBase)val).Id.ToString(), b, StringComparison.Ordinal))
				{
					sessionId = profile.Key;
					break;
				}
			}
		}
		catch
		{
		}
	}

	public SaveServerScavProfileFallbackPatch()
		: base((string)null)
	{
	}
}
