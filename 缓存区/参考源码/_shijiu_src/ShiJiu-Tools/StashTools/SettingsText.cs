using System;
using System.Text.RegularExpressions;

namespace StashTools;

internal static class SettingsText
{
	internal static string MigrateConfigText(string text)
	{
		return Regex.Replace(text, "(?m)^([ \\t]*中键出售模式[ \\t]*=[ \\t]*)(Best|Flea|Trader)([ \\t]*\\r?)$", (Match match) => match.Groups[1].Value + ParseMode(match.Groups[2].Value).ToString() + match.Groups[3].Value);
	}

	internal static SellMode ParseMode(string value)
	{
		switch (value.Trim())
		{
		case "Best":
		case "收益优先":
			return SellMode.收益优先;
		case "Flea":
		case "仅跳蚤":
			return SellMode.仅跳蚤;
		case "Trader":
		case "仅商人":
			return SellMode.仅商人;
		default:
			throw new FormatException("未知的出售模式：" + value);
		}
	}

	internal static string KeyName(string name)
	{
		if (name.Length != 1)
		{
			if (name.StartsWith("F"))
			{
				string text = name;
				if (int.TryParse(text.Substring(1, text.Length - 1), out var _))
				{
					goto IL_0030;
				}
			}
			if (name.StartsWith("Alpha"))
			{
				string text = name;
				return text.Substring(5, text.Length - 5);
			}
			if (name.StartsWith("Keypad"))
			{
				string text = name;
				return "小键盘 " + KeyName(text.Substring(6, text.Length - 6));
			}
			if (name.StartsWith("Joystick"))
			{
				string text = name;
				return "手柄 " + text.Substring(8, text.Length - 8).Replace("Button", " 按钮 ");
			}
			switch (name)
			{
			case "None":
				return "未设置";
			case "Backspace":
				return "退格键";
			case "Tab":
				return "制表键";
			case "Clear":
				return "清除键";
			case "Enter":
			case "Return":
				return "回车键";
			case "Pause":
				return "暂停键";
			case "Escape":
				return "退出键";
			case "Space":
				return "空格键";
			case "Delete":
				return "删除键";
			case "Insert":
				return "插入键";
			case "Home":
				return "起始键";
			case "End":
				return "末尾键";
			case "PageUp":
				return "向上翻页";
			case "PageDown":
				return "向下翻页";
			case "UpArrow":
				return "方向键 ↑";
			case "DownArrow":
				return "方向键 ↓";
			case "LeftArrow":
				return "方向键 ←";
			case "RightArrow":
				return "方向键 →";
			case "Numlock":
				return "数字锁定";
			case "CapsLock":
				return "大写锁定";
			case "ScrollLock":
				return "滚动锁定";
			case "LeftShift":
				return "左上档键";
			case "RightShift":
				return "右上档键";
			case "LeftControl":
				return "左控制键";
			case "RightControl":
				return "右控制键";
			case "LeftAlt":
				return "左换档键";
			case "AltGr":
			case "RightAlt":
				return "右换档键";
			case "LeftApple":
			case "LeftMeta":
			case "LeftCommand":
			case "LeftWindows":
				return "左系统键";
			case "RightMeta":
			case "RightApple":
			case "RightCommand":
			case "RightWindows":
				return "右系统键";
			case "Mouse0":
				return "鼠标左键";
			case "Mouse1":
				return "鼠标右键";
			case "Mouse2":
				return "鼠标中键";
			case "Mouse3":
				return "鼠标侧键一";
			case "Mouse4":
				return "鼠标侧键二";
			case "Mouse5":
				return "鼠标按键六";
			case "Mouse6":
				return "鼠标按键七";
			case "Exclaim":
				return "!";
			case "DoubleQuote":
				return "\"";
			case "Hash":
				return "#";
			case "Dollar":
				return "$";
			case "Percent":
				return "%";
			case "Ampersand":
				return "&";
			case "Quote":
				return "'";
			case "LeftParen":
				return "(";
			case "RightParen":
				return ")";
			case "Asterisk":
			case "Multiply":
				return "*";
			case "Plus":
				return "+";
			case "Comma":
				return ",";
			case "Minus":
				return "-";
			case "Period":
				return ".";
			case "Slash":
			case "Divide":
				return "/";
			case "Colon":
				return ":";
			case "Semicolon":
				return ";";
			case "Less":
				return "<";
			case "Equals":
				return "=";
			case "Greater":
				return ">";
			case "Question":
				return "?";
			case "At":
				return "@";
			case "LeftBracket":
				return "[";
			case "RightBracket":
				return "]";
			case "Backslash":
				return "\\";
			case "Caret":
				return "^";
			case "Underscore":
				return "_";
			case "BackQuote":
				return "`";
			case "LeftCurlyBracket":
				return "{";
			case "RightCurlyBracket":
				return "}";
			case "Pipe":
				return "|";
			case "Tilde":
				return "~";
			case "Help":
				return "帮助键";
			case "Print":
				return "截屏键";
			case "SysReq":
				return "系统请求键";
			case "Break":
				return "中断键";
			case "Menu":
				return "菜单键";
			default:
				return "按键（" + name + "）";
			}
		}
		goto IL_0030;
		IL_0030:
		return name;
	}
}
