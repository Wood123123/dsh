using System;
using System.Collections.Generic;
using EFT.Hideout;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Converters;

public class RequirementArrayConverter : JsonConverter
{
	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected I4, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Expected O, but got Unknown
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		JArray obj = JArray.Load(reader);
		List<Requirement> list = new List<Requirement>();
		foreach (JToken item in obj)
		{
			if ((int)item.Type != 1)
			{
				continue;
			}
			JObject val = (JObject)item;
			string text = ((JToken)val).Value<string>((object)"type");
			if (text != null)
			{
				if (!Enum.TryParse<ERequirementType>(text, out ERequirementType result))
				{
					LogHelper.LogWarn("Could not convert requirement type " + text + " to ERequirementType");
				}
				Requirement val2 = (Requirement)((int)result switch
				{
					0 => (object)new AreaRequirement(), 
					9 => (object)new BodyPartBuffRequirement(), 
					10 => (object)new GameVersionRequirement(), 
					8 => (object)new HealthRequirement(), 
					1 => (object)new ItemRequirement(), 
					7 => (object)new QuestRequirement(), 
					5 => (object)new ResourceRequirement(), 
					4 => (object)new SkillRequirement(), 
					6 => (object)new ToolRequirement(), 
					3 => (object)new TraderLoyaltyRequirement(), 
					2 => (object)new TraderUnlockRequirement(), 
					_ => null, 
				});
				if (val2 == null)
				{
					return null;
				}
				serializer.Populate(((JToken)val).CreateReader(), (object)val2);
				list.Add(val2);
			}
		}
		return list.ToArray();
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		serializer.Serialize(writer, value);
	}

	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(Requirement[]);
	}
}
