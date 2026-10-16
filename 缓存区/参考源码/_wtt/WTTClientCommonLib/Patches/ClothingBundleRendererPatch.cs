using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Diz.Jobs;
using EFT;
using EFT.Customization;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class ClothingBundleRendererPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ClothingIconCreator), "RenderModel", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	private static bool PatchPrefix(ClothingIconCreator __instance, CustomizationClothing clothing, SpriteFactory<CustomizationClothing, ClothingIcon> spriteFactory, ref Task<RenderModelResult<CustomizationClothing, ClothingIcon>> __result)
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			GameObject val = null;
			try
			{
				val = EasyAssetsExtensions.GetAsset<GameObject>(__instance._easyAssets, clothing.Prefab.path, clothing.Prefab.rcid);
			}
			catch (Exception)
			{
				LogHelper.LogDebug("Bundle not loaded for " + clothing.Prefab.path + ", using original method");
				return true;
			}
			SkinnedMeshRenderer[] array = ((val != null) ? val.GetComponentsInChildren<SkinnedMeshRenderer>(true) : null);
			if (array == null || array.Length <= 1)
			{
				return true;
			}
			HashSet<Material> hashSet = new HashSet<Material>();
			SkinnedMeshRenderer[] array2 = array;
			foreach (SkinnedMeshRenderer val2 in array2)
			{
				if (((Renderer)val2).sharedMaterials == null)
				{
					continue;
				}
				Material[] sharedMaterials = ((Renderer)val2).sharedMaterials;
				foreach (Material val3 in sharedMaterials)
				{
					if ((Object)(object)val3 != (Object)null)
					{
						hashSet.Add(val3);
					}
				}
			}
			if (hashSet.Count <= 1)
			{
				ModulePatch.Logger.LogDebug((object)$"Skipping patch: Only {hashSet.Count} unique materials found");
				return true;
			}
			GameObject val4 = new GameObject("ClothingBundleParent");
			val4.layer = LayersMaskController.WeaponPreview;
			val4.transform.SetPositionAndRotation(__instance._clothingMeshContainer.transform.position, __instance._clothingMeshContainer.transform.rotation);
			array2 = array;
			foreach (SkinnedMeshRenderer val5 in array2)
			{
				if ((Object)(object)val5.sharedMesh != (Object)null && ((Renderer)val5).enabled)
				{
					GameObject val6 = new GameObject(((Object)val5).name + "_Preview")
					{
						layer = val4.layer
					};
					val6.transform.SetParent(val4.transform, false);
					MeshFilter val7 = val6.AddComponent<MeshFilter>();
					MeshRenderer obj = val6.AddComponent<MeshRenderer>();
					val7.sharedMesh = val5.sharedMesh;
					((Renderer)obj).sharedMaterials = ((Renderer)val5).sharedMaterials;
					val6.transform.localPosition = ((Component)val5).transform.localPosition;
					val6.transform.localRotation = ((Component)val5).transform.localRotation;
					val6.transform.localScale = ((Component)val5).transform.localScale;
				}
			}
			__result = CompleteRendering(__instance, spriteFactory, val4, val);
			return false;
		}
		catch (Exception arg)
		{
			ModulePatch.Logger.LogError((object)$"Error in ClothingBundleRendererPatch: {arg}");
			return true;
		}
	}

	private static async Task<RenderModelResult<CustomizationClothing, ClothingIcon>> CompleteRendering(ClothingIconCreator instance, SpriteFactory<CustomizationClothing, ClothingIcon> spriteFactory, GameObject parentObject, GameObject asset)
	{
		GameObject originalGameObject = instance._clothingMeshContainer;
		MeshFilter originalMeshFilter = instance._filter;
		MeshRenderer originalMeshRenderer = instance._renderer;
		try
		{
			object obj = default(object);
			while (instance._containerIsBusy)
			{
				IJobAwaiter val = JobScheduler.Yield((EJobPriority)1, (JobYield)null).GetAwaiter();
				if (!val.IsCompleted)
				{
					await val;
					val = (IJobAwaiter)obj;
					obj = null;
				}
				val.GetResult();
			}
			instance._containerIsBusy = true;
			if (!parentObject.activeSelf)
			{
				parentObject.SetActive(true);
			}
			foreach (Transform item in parentObject.transform)
			{
				item.Rotate(90f, 0f, 0f, (Space)1);
			}
			Bounds bounds = default(Bounds);
			((Bounds)(ref bounds))._002Ector(Vector3.zero, Vector3.zero);
			bool flag = false;
			int num = 0;
			Bounds val3 = default(Bounds);
			foreach (Transform item2 in parentObject.transform)
			{
				Transform val2 = item2;
				MeshFilter component = ((Component)val2).GetComponent<MeshFilter>();
				if (!((Object)(object)component != (Object)null) || !((Object)(object)component.sharedMesh != (Object)null))
				{
					continue;
				}
				Bounds bounds2 = component.sharedMesh.bounds;
				Vector3 size = ((Bounds)(ref bounds2)).size;
				if (((Vector3)(ref size)).magnitude < 0.001f)
				{
					ModulePatch.Logger.LogWarning((object)("Skipping mesh with invalid bounds: " + ((Object)val2).name));
					continue;
				}
				Vector3[] array = (Vector3[])(object)new Vector3[8];
				Vector3 center = ((Bounds)(ref bounds2)).center;
				Vector3 extents = ((Bounds)(ref bounds2)).extents;
				array[0] = center + new Vector3(0f - extents.x, 0f - extents.y, 0f - extents.z);
				array[1] = center + new Vector3(extents.x, 0f - extents.y, 0f - extents.z);
				array[2] = center + new Vector3(0f - extents.x, extents.y, 0f - extents.z);
				array[3] = center + new Vector3(extents.x, extents.y, 0f - extents.z);
				array[4] = center + new Vector3(0f - extents.x, 0f - extents.y, extents.z);
				array[5] = center + new Vector3(extents.x, 0f - extents.y, extents.z);
				array[6] = center + new Vector3(0f - extents.x, extents.y, extents.z);
				array[7] = center + new Vector3(extents.x, extents.y, extents.z);
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = val2.TransformPoint(array[i]);
					array[i] = parentObject.transform.InverseTransformPoint(array[i]);
				}
				((Bounds)(ref val3))._002Ector(array[0], Vector3.zero);
				for (int j = 1; j < array.Length; j++)
				{
					((Bounds)(ref val3)).Encapsulate(array[j]);
				}
				if (!flag)
				{
					bounds = val3;
					flag = true;
				}
				else
				{
					((Bounds)(ref bounds)).Encapsulate(val3);
				}
				num++;
			}
			LogHelper.LogDebug($"Rendering with {num} valid meshes. Bounds size: {((Bounds)(ref bounds)).size}");
			if (num == 0)
			{
				ModulePatch.Logger.LogWarning((object)"No valid meshes found for rendering");
				instance._containerIsBusy = false;
				return default(RenderModelResult<CustomizationClothing, ClothingIcon>);
			}
			instance._clothingMeshContainer = parentObject;
			Mesh boundsMesh = new Mesh();
			boundsMesh.bounds = bounds;
			MeshFilter val4 = parentObject.AddComponent<MeshFilter>();
			val4.sharedMesh = boundsMesh;
			instance._filter = val4;
			MeshRenderer renderer = parentObject.AddComponent<MeshRenderer>();
			instance._renderer = renderer;
			PreviewPivot val5 = ((asset != null) ? asset.GetComponent<PreviewPivot>() : null);
			Sprite sprite = await spriteFactory.Invoke(parentObject, val5);
			instance._containerIsBusy = false;
			instance._clothingMeshContainer = originalGameObject;
			instance._filter = originalMeshFilter;
			instance._renderer = originalMeshRenderer;
			if ((Object)(object)parentObject != (Object)null)
			{
				Object.Destroy((Object)(object)parentObject);
			}
			if ((Object)(object)boundsMesh != (Object)null)
			{
				Object.Destroy((Object)(object)boundsMesh);
			}
			return new RenderModelResult<CustomizationClothing, ClothingIcon>
			{
				sprite = sprite,
				zeroMipWasLoaded = true
			};
		}
		catch (Exception arg)
		{
			instance._containerIsBusy = false;
			instance._clothingMeshContainer = originalGameObject;
			instance._filter = originalMeshFilter;
			instance._renderer = originalMeshRenderer;
			ModulePatch.Logger.LogError((object)$"Error in CompleteRendering: {arg}");
			if ((Object)(object)parentObject != (Object)null)
			{
				Object.Destroy((Object)(object)parentObject);
			}
			return default(RenderModelResult<CustomizationClothing, ClothingIcon>);
		}
	}
}
