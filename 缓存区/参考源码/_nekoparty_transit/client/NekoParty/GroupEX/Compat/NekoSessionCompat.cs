using EFT;
using HarmonyLib;
using JsonType;

namespace NekoParty.GroupEX.Compat;

internal static class NekoSessionCompat
{
	internal static Profile GetProfileBySide(object session, ESideType side)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		IProfileSession val = (IProfileSession)((session is IProfileSession) ? session : null);
		if (val != null)
		{
			if ((int)side != 1)
			{
				return val.Profile;
			}
			return val.ProfileOfPet;
		}
		return null;
	}

	internal static IEftSession GetMenuSession(object menuOperation)
	{
		if (menuOperation == null)
		{
			return null;
		}
		return Traverse.Create(menuOperation).Field("iEftSession").GetValue<IEftSession>();
	}

	internal static LocationSettings GetLocationSettings()
	{
		TarkovApplication val = null;
		if (TarkovApplication.Exist(ref val))
		{
			IEftSession session = ((ClientApplication<IEftSession>)(object)val).Session;
			if (session == null)
			{
				return null;
			}
			return session.LocationSettings;
		}
		return null;
	}
}
