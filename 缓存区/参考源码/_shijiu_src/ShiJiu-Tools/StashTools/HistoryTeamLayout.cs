namespace StashTools;

internal static class HistoryTeamLayout
{
	internal static bool A(float P_0)
	{
		return P_0 >= 940f;
	}

	internal static float a(float P_0)
	{
		return A(P_0) ? 64 : 88;
	}

	internal static HistoryBox[] B(float P_0)
	{
		float[] array = new float[7] { 0.2f, 0.16f, 0.14f, 0.16f, 0.07f, 0.09f, 0.18f };
		float num = 0f;
		HistoryBox[] array2 = new HistoryBox[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = P_0 * array[i];
			array2[i] = new HistoryBox(num, 0f, num2, a(P_0));
			num += num2;
		}
		return array2;
	}

	internal static string A(bool P_0, bool P_1, bool P_2, bool P_3, string P_4)
	{
		if (P_0)
		{
			if (P_2)
			{
				if (P_3)
				{
					return "";
				}
				return "已结束，待核对";
			}
			return "转图中，尚未结束";
		}
		if (!P_1)
		{
			return "等待队友上传";
		}
		return "等待结算上传";
	}
}
