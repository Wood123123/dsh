using System;
using System.Collections.Generic;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Services;

public class ExtendedRecipeLoader
{
	public static ExtendedRecipeLoader Instance;

	private Dictionary<string, ExtendedProductionScheme> _extendedSchemes = new Dictionary<string, ExtendedProductionScheme>();

	public ExtendedRecipeLoader()
	{
		if (Instance == null)
		{
			Instance = this;
		}
	}

	public ExtendedProductionScheme GetExtendedScheme(string schemeId)
	{
		_extendedSchemes.TryGetValue(schemeId, out ExtendedProductionScheme value);
		return value;
	}

	public void FetchExtendedRecipesFromServer()
	{
		try
		{
			Dictionary<string, ExtendedProductionScheme> dictionary = Utils.Get<Dictionary<string, ExtendedProductionScheme>>("/wttcommonlib/recipes/extended/get");
			if (dictionary != null)
			{
				_extendedSchemes = dictionary;
			}
			else
			{
				LogHelper.LogError("Failed to fetch extended recipe data from server");
			}
		}
		catch (Exception ex)
		{
			LogHelper.LogError(ex.ToString());
			throw;
		}
	}

	public void LoadExtendedRecipeResults()
	{
		foreach (KeyValuePair<string, ExtendedProductionScheme> extendedScheme in _extendedSchemes)
		{
			extendedScheme.Deconstruct(out var _, out var value);
			value.LoadResultItems();
		}
	}
}
