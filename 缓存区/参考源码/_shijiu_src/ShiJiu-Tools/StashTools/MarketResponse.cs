using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace StashTools;

internal static class MarketResponse
{
	internal static MarketQuote Parse(ReadOnlyMemory<byte> json)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		JObject val = JObject.Parse(Encoding.UTF8.GetString(json.ToArray()));
		JToken obj = val["err"];
		JObject data;
		if (obj != null && (int)obj.Type == 6 && Extensions.Value<long>((IEnumerable<JToken>)val["err"]) == 0L)
		{
			JToken val2 = val["data"];
			data = (JObject)(object)((val2 is JObject) ? val2 : null);
			if (data != null)
			{
				return new MarketQuote(Read("min"), Read("avg"), Read("max"));
			}
		}
		throw new InvalidDataException("市场报价响应无效");
		double Read(string name)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Invalid comparison between Unknown and I4
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Invalid comparison between Unknown and I4
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Invalid comparison between Unknown and I4
			JToken val3 = data[name];
			if (val3 == null || (int)val3.Type == 10)
			{
				return 0.0;
			}
			if ((int)val3.Type != 6 && (int)val3.Type != 7)
			{
				throw new InvalidDataException("市场报价数字无效：" + name);
			}
			double num = Extensions.Value<double>((IEnumerable<JToken>)val3);
			if (double.IsNaN(num) || double.IsInfinity(num) || num < 0.0)
			{
				throw new InvalidDataException("市场报价数字无效：" + name);
			}
			return num;
		}
	}
}
