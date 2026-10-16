using System;
using System.Collections.Generic;
using System.Linq;
using StashTools.History;

namespace StashTools.Fika;

internal sealed class FikaHistorySession
{
	private readonly Dictionary<string, TeamMemberIdentity> _members = new Dictionary<string, TeamMemberIdentity>(StringComparer.Ordinal);

	private readonly Dictionary<string, bool?> _identities = new Dictionary<string, bool?>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> _memberCharacters = new Dictionary<string, string>(StringComparer.Ordinal);

	private bool _awaitTransit;

	internal string ActionId { get; private set; } = "";

	internal bool SegmentOpen { get; private set; }

	internal bool ContinuingTransit { get; private set; }

	internal bool Begin(Guid sharedRoom, bool transit)
	{
		if (sharedRoom == Guid.Empty)
		{
			SegmentOpen = false;
			ContinuingTransit = false;
			if (!_awaitTransit)
			{
				ActionId = "";
				_members.Clear();
				_identities.Clear();
			}
			return false;
		}
		ContinuingTransit = (_awaitTransit & transit) && ActionId.Length > 0;
		if (!ContinuingTransit)
		{
			ActionId = "fika-" + sharedRoom.ToString("N");
			_members.Clear();
			_identities.Clear();
			_memberCharacters.Clear();
		}
		SegmentOpen = true;
		_awaitTransit = false;
		return true;
	}

	internal bool End(bool transit)
	{
		if (!SegmentOpen)
		{
			return false;
		}
		SegmentOpen = false;
		_awaitTransit = transit;
		return true;
	}

	internal void Observe(string character, string? account, string name, bool isAi, bool scav)
	{
		if (!SegmentOpen || string.IsNullOrWhiteSpace(character))
		{
			return;
		}
		if (_identities.TryGetValue(character, out var value) && value != isAi)
		{
			_identities[character] = null;
		}
		else
		{
			_identities[character] = isAi;
		}
		if (isAi)
		{
			return;
		}
		string text = AccountToken(account, character, scav);
		if (text == null)
		{
			return;
		}
		if (_memberCharacters.TryGetValue(character, out string oldToken) && oldToken != text && !_memberCharacters.Any<KeyValuePair<string, string>>((KeyValuePair<string, string> m) => m.Key != character && m.Value == oldToken))
		{
			_members.Remove(oldToken);
		}
		_memberCharacters[character] = text;
		if (_members.Count < 32 || _members.ContainsKey(text))
		{
			if (name == null)
			{
				name = "";
			}
			_members[text] = new TeamMemberIdentity
			{
				Profile = text,
				Name = ((name.Length > 128) ? name.Substring(0, 128) : name)
			};
		}
	}

	internal bool? Identity(string character)
	{
		return _identities.GetValueOrDefault(character);
	}

	internal bool ContainsMember(string character)
	{
		if (_memberCharacters.TryGetValue(character, out string value))
		{
			return _members.ContainsKey(value);
		}
		return false;
	}

	internal CoopRaidContext? Capture(string provider)
	{
		if (ActionId.Length == 0 || _members.Count == 0)
		{
			return null;
		}
		return new CoopRaidContext
		{
			Provider = provider,
			ActionId = ActionId,
			Members = (from m in _members.Values.OrderBy<TeamMemberIdentity, string>((TeamMemberIdentity m) => m.Profile, StringComparer.Ordinal)
				select new TeamMemberIdentity
				{
					Profile = m.Profile,
					Name = m.Name
				}).ToList()
		};
	}

	internal static string? AccountToken(string? account, string character, bool scav)
	{
		if (!string.IsNullOrWhiteSpace(account) && account.Length <= 48 && account.All((char c) => c >= '0' && c <= '9') && account.Any((char c) => c != '0'))
		{
			return "aid_" + account;
		}
		if (scav || string.IsNullOrWhiteSpace(character) || character.Length > 64 || character.Any(delegate(char c)
		{
			bool flag;
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'A':
			case 'B':
			case 'C':
			case 'D':
			case 'E':
			case 'F':
			case 'G':
			case 'H':
			case 'I':
			case 'J':
			case 'K':
			case 'L':
			case 'M':
			case 'N':
			case 'O':
			case 'P':
			case 'Q':
			case 'R':
			case 'S':
			case 'T':
			case 'U':
			case 'V':
			case 'W':
			case 'X':
			case 'Y':
			case 'Z':
			case 'a':
			case 'b':
			case 'c':
			case 'd':
			case 'e':
			case 'f':
			case 'g':
			case 'h':
			case 'i':
			case 'j':
			case 'k':
			case 'l':
			case 'm':
			case 'n':
			case 'o':
			case 'p':
			case 'q':
			case 'r':
			case 's':
			case 't':
			case 'u':
			case 'v':
			case 'w':
			case 'x':
			case 'y':
			case 'z':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			return !flag && c != '_' && c != '-';
		}))
		{
			return null;
		}
		return character;
	}
}
