using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AlexMalyutinDev.RadianceCascades;

namespace AlexMalyutinDev.RadianceCascades.Editor
{
    public static class SetupRadianceCascades
    {
        private const string MenuPath = "Tools/Radiance Cascades/";
        
        [MenuItem(MenuPath + "Setup in Current Scene")]
        public static void SetupInCurrentScene()
        {
            Debug.Log("=== Radiance Cascades Auto Setup ===");
            
            // Step 1: Find or configure URP Renderer
            var rendererData = FindOrConfigureURPRenderer();
            if (rendererData == null)
            {
                Debug.LogError("Failed to find or configure URP Renderer. Please ensure URP is set up in your project.");
                return;
            }
            
            // Step 2: Add RadianceCascadesFeature
            if (!AddRadianceCascadesFeature(rendererData))
            {
                Debug.LogWarning("RadianceCascadesFeature may already exist or failed to add.");
            }
            
            // Step 3: Find RadianceCascadeResources
            var resources = FindRadianceCascadeResources();
            if (resources == null)
            {
                Debug.LogError("Failed to find RadianceCascadeResources asset. Please ensure the package is properly installed.");
                return;
            }
            
            // Step 4: Create or update Volume Profile
            var volumeProfile = FindOrCreateVolumeProfile();
            if (volumeProfile == null)
            {
                Debug.LogError("Failed to create or find Volume Profile.");
                return;
            }
            
            // Step 5: Configure RadianceCascades component
            ConfigureRadianceCascadesComponent(volumeProfile);
            
            // Step 6: Setup Global Volume in scene
            SetupGlobalVolume(volumeProfile);
            
            // Step 7: Validate setup
            ValidateSetup(rendererData, resources, volumeProfile);
            
            Debug.Log("=== Radiance Cascades Setup Complete ===");
            Debug.Log("Please verify the setup in the Inspector and test in Play Mode.");
        }
        
        [MenuItem(MenuPath + "Validate Setup")]
        public static void ValidateSetup()
        {
            Debug.Log("=== Validating Radiance Cascades Setup ===");
            
            var rendererData = FindActiveURPRenderer();
            var resources = FindRadianceCascadeResources();
            var volumeProfile = FindVolumeProfileInScene();
            
            ValidateSetup(rendererData, resources, volumeProfile);
        }
        
        [MenuItem(MenuPath + "Reset to Defaults")]
        public static void ResetToDefaults()
        {
            var volumeProfile = FindVolumeProfileInScene();
            if (volumeProfile == null)
            {
                Debug.LogWarning("No Volume Profile found in current scene.");
                return;
            }
            
            var rcComponent = volumeProfile.components.FirstOrDefault(c => c is RadianceCascades) as RadianceCascades;
            if (rcComponent == null)
            {
                Debug.LogWarning("RadianceCascades component not found in Volume Profile.");
                return;
            }
            
            // Reset to recommended defaults
            rcComponent.RenderingType.value = RenderingType.DirectionFirstProbes;
            rcComponent.RenderingType.overrideState = true;
            rcComponent.RayScale.value = 0.1f;
            rcComponent.RayScale.overrideState = true;
            rcComponent.EnableAdaptiveRayScale.value = true;
            rcComponent.EnableAdaptiveRayScale.overrideState = true;
            rcComponent.CascadeScaleFactor.value = 1.5f;
            rcComponent.CascadeScaleFactor.overrideState = true;
            rcComponent.VarianceInfluence.value = 0.3f;
            rcComponent.VarianceInfluence.overrideState = true;
            
            EditorUtility.SetDirty(volumeProfile);
            Debug.Log("Radiance Cascades settings reset to defaults.");
        }
        
        // Helper Methods
        
        private static UniversalRendererData FindOrConfigureURPRenderer()
        {
            // First, try to find the active renderer from Graphics Settings
            var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                // Use reflection to access m_RendererDataList (internal field)
                var rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField(
                    "m_RendererDataList",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                );
                
                if (rendererDataListField != null)
                {
                    var rendererDataList = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];
                    if (rendererDataList != null && rendererDataList.Length > 0)
                    {
                        var rendererData = rendererDataList[0] as UniversalRendererData;
                        if (rendererData != null)
                        {
                            Debug.Log($"Found URP Renderer: {rendererData.name}");
                            return rendererData;
                        }
                    }
                }
            }
            
            // Fallback: Search for UniversalRendererData assets in project
            var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData != null)
                {
                    Debug.Log($"Found URP Renderer asset: {path}");
                    return rendererData;
                }
            }
            
            Debug.LogWarning("No URP Renderer found. Please ensure URP is configured in your project.");
            return null;
        }
        
        private static UniversalRendererData FindActiveURPRenderer()
        {
            return FindOrConfigureURPRenderer();
        }
        
        private static bool AddRadianceCascadesFeature(UniversalRendererData rendererData)
        {
            if (rendererData == null) return false;
            
            // Check if feature already exists
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is RadianceCascadesFeature)
                {
                    Debug.Log("RadianceCascadesFeature already exists in renderer.");
                    
                    // Ensure Resources are assigned
                    var rcFeature = feature as RadianceCascadesFeature;
                    if (rcFeature.Resources == null)
                    {
                        var resources = FindRadianceCascadeResources();
                        if (resources != null)
                        {
                            rcFeature.Resources = resources;
                            EditorUtility.SetDirty(rendererData);
                            Debug.Log("Assigned RadianceCascadeResources to existing feature.");
                        }
                    }
                    return false; // Already exists
                }
            }
            
            // Create new feature
            var newFeature = ScriptableObject.CreateInstance<RadianceCascadesFeature>();
            newFeature.name = "Radiance Cascades Feature";
            newFeature.Resources = FindRadianceCascadeResources();
            
            // Add to renderer using SerializedObject (rendererFeatures is read-only)
            var serializedObject = new SerializedObject(rendererData);
            var rendererFeaturesProperty = serializedObject.FindProperty("m_RendererFeatures");
            
            if (rendererFeaturesProperty != null)
            {
                rendererFeaturesProperty.arraySize++;
                var newElement = rendererFeaturesProperty.GetArrayElementAtIndex(rendererFeaturesProperty.arraySize - 1);
                newElement.objectReferenceValue = newFeature;
                serializedObject.ApplyModifiedProperties();
                
                // Save the feature as a sub-asset
                var rendererDataPath = AssetDatabase.GetAssetPath(rendererData);
                AssetDatabase.AddObjectToAsset(newFeature, rendererDataPath);
                AssetDatabase.SaveAssets();
                
                EditorUtility.SetDirty(rendererData);
                Debug.Log("Added RadianceCascadesFeature to renderer.");
                return true;
            }
            else
            {
                Debug.LogError("Failed to find m_RendererFeatures property in UniversalRendererData.");
                Object.DestroyImmediate(newFeature);
                return false;
            }
        }
        
        private static RadianceCascadeResources FindRadianceCascadeResources()
        {
            // First, try package Resources folder
            var packageGuids = AssetDatabase.FindAssets("RadianceCascadeResources t:RadianceCascadeResources");
            foreach (var guid in packageGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("com.alexmalyutindev.radiance-cascades-urp"))
                {
                    var resources = AssetDatabase.LoadAssetAtPath<RadianceCascadeResources>(path);
                    if (resources != null)
                    {
                        Debug.Log($"Found RadianceCascadeResources in package: {path}");
                        return resources;
                    }
                }
            }
            
            // Fallback: Search in Assets folder
            var assetGuids = AssetDatabase.FindAssets("RadianceCascadeResources t:RadianceCascadeResources");
            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("PackageCache"))
                {
                    var resources = AssetDatabase.LoadAssetAtPath<RadianceCascadeResources>(path);
                    if (resources != null)
                    {
                        Debug.Log($"Found RadianceCascadeResources in Assets: {path}");
                        return resources;
                    }
                }
            }
            
            Debug.LogError("RadianceCascadeResources asset not found. Please ensure the package is properly installed.");
            return null;
        }
        
        private static VolumeProfile FindOrCreateVolumeProfile()
        {
            // First, try to find existing Volume Profile in scene
            var volumeProfile = FindVolumeProfileInScene();
            if (volumeProfile != null)
            {
                Debug.Log($"Using existing Volume Profile in scene: {volumeProfile.name}");
                return volumeProfile;
            }
            
            // Create new Volume Profile
            var newProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var profilePath = "Assets/RadianceCascadesProfile.asset";
            
            // Ensure unique name
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) != null)
            {
                profilePath = AssetDatabase.GenerateUniqueAssetPath(profilePath);
            }
            
            AssetDatabase.CreateAsset(newProfile, profilePath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created new Volume Profile: {profilePath}");
            return newProfile;
        }
        
        private static VolumeProfile FindVolumeProfileInScene()
        {
            // Find Global Volume in scene
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var volume in volumes)
            {
                if (volume.isGlobal && volume.profile != null)
                {
                    return volume.profile;
                }
            }
            
            // Search for Volume Profiles in project that might be used
            var guids = AssetDatabase.FindAssets("t:VolumeProfile");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile != null && profile.components.Any(c => c is RadianceCascades))
                {
                    return profile;
                }
            }
            
            return null;
        }
        
        private static void ConfigureRadianceCascadesComponent(VolumeProfile volumeProfile)
        {
            if (volumeProfile == null) return;
            
            // Check if component already exists
            RadianceCascades rcComponent = null;
            foreach (var component in volumeProfile.components)
            {
                if (component is RadianceCascades)
                {
                    rcComponent = component as RadianceCascades;
                    break;
                }
            }
            
            // Create if doesn't exist
            if (rcComponent == null)
            {
                rcComponent = volumeProfile.Add<RadianceCascades>();
                Debug.Log("Added RadianceCascades component to Volume Profile.");
            }
            else
            {
                Debug.Log("RadianceCascades component already exists in Volume Profile.");
            }
            
            // Configure settings
            rcComponent.RenderingType.overrideState = true;
            rcComponent.RenderingType.value = RenderingType.DirectionFirstProbes;
            
            rcComponent.RayScale.overrideState = true;
            rcComponent.RayScale.value = GetRecommendedRayScale();
            
            rcComponent.EnableAdaptiveRayScale.overrideState = true;
            rcComponent.EnableAdaptiveRayScale.value = true;
            
            rcComponent.CascadeScaleFactor.overrideState = true;
            rcComponent.CascadeScaleFactor.value = 1.5f;
            
            rcComponent.VarianceInfluence.overrideState = true;
            rcComponent.VarianceInfluence.value = 0.3f;
            
            EditorUtility.SetDirty(volumeProfile);
            Debug.Log("Configured RadianceCascades component with recommended settings.");
        }
        
        private static void SetupGlobalVolume(VolumeProfile volumeProfile)
        {
            if (volumeProfile == null) return;
            
            // Find existing Global Volume
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Volume globalVolume = null;
            
            foreach (var volume in volumes)
            {
                if (volume.isGlobal)
                {
                    globalVolume = volume;
                    break;
                }
            }
            
            // Create if doesn't exist
            if (globalVolume == null)
            {
                var go = new GameObject("Global Volume");
                globalVolume = go.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                Debug.Log("Created Global Volume GameObject in scene.");
            }
            
            // Assign profile
            globalVolume.profile = volumeProfile;
            globalVolume.weight = 1.0f;
            
            EditorUtility.SetDirty(globalVolume);
            Debug.Log("Configured Global Volume with Volume Profile.");
        }
        
        private static float GetRecommendedRayScale()
        {
            // Calculate scene bounds to recommend Ray Scale
            var bounds = new Bounds();
            var hasBounds = false;
            
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }
            
            if (hasBounds)
            {
                var size = bounds.size.magnitude;
                // Recommend Ray Scale based on scene size
                if (size < 10f) return 0.05f;      // Small scene
                if (size < 50f) return 0.1f;       // Medium scene
                if (size < 200f) return 0.2f;      // Large scene
                return 0.5f;                        // Very large scene
            }
            
            return 0.1f; // Default
        }
        
        private static void ValidateSetup(UniversalRendererData rendererData, RadianceCascadeResources resources, VolumeProfile volumeProfile)
        {
            var issues = new List<string>();
            var warnings = new List<string>();
            
            // Validate Renderer
            if (rendererData == null)
            {
                issues.Add("URP Renderer not found.");
            }
            else
            {
                var hasFeature = false;
                RadianceCascadesFeature rcFeature = null;
                
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature is RadianceCascadesFeature)
                    {
                        hasFeature = true;
                        rcFeature = feature as RadianceCascadesFeature;
                        break;
                    }
                }
                
                if (!hasFeature)
                {
                    issues.Add("RadianceCascadesFeature not found in renderer.");
                }
                else if (rcFeature.Resources == null)
                {
                    issues.Add("RadianceCascadesFeature.Resources is not assigned.");
                }
            }
            
            // Validate Resources
            if (resources == null)
            {
                issues.Add("RadianceCascadeResources asset not found.");
            }
            
            // Validate Volume Profile
            if (volumeProfile == null)
            {
                issues.Add("Volume Profile not found.");
            }
            else
            {
                var hasComponent = volumeProfile.components.Any(c => c is RadianceCascades);
                if (!hasComponent)
                {
                    issues.Add("RadianceCascades component not found in Volume Profile.");
                }
            }
            
            // Validate Global Volume in scene
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var hasGlobalVolume = volumes.Any(v => v.isGlobal && v.profile != null);
            if (!hasGlobalVolume)
            {
                issues.Add("Global Volume not found in scene.");
            }
            
            // Validate URP Asset
            var urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                warnings.Add("URP Asset not assigned in Graphics Settings.");
            }
            else
            {
                // Check if Deferred Rendering is enabled (requires reflection)
                var renderingPathField = typeof(UniversalRenderPipelineAsset).GetField(
                    "m_RenderingPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                );
                
                if (renderingPathField != null)
                {
                    var renderingPathValue = renderingPathField.GetValue(urpAsset);
                    // URP uses enum values: 0 = Forward, 1 = Deferred
                    if (renderingPathValue != null && !renderingPathValue.ToString().Contains("Deferred"))
                    {
                        warnings.Add("Deferred Rendering may not be enabled. Radiance Cascades requires Deferred Rendering.");
                    }
                }
            }
            
            // Report results
            if (issues.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("✓ Setup validation passed! All components are properly configured.");
            }
            else
            {
                if (issues.Count > 0)
                {
                    Debug.LogError("Setup validation found issues:");
                    foreach (var issue in issues)
                    {
                        Debug.LogError($"  - {issue}");
                    }
                }
                
                if (warnings.Count > 0)
                {
                    Debug.LogWarning("Setup validation found warnings:");
                    foreach (var warning in warnings)
                    {
                        Debug.LogWarning($"  - {warning}");
                    }
                }
            }
        }
    }
}

