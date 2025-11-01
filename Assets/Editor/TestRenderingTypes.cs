using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using AlexMalyutinDev.RadianceCascades;

namespace AlexMalyutinDev.RadianceCascades.Editor
{
    public class TestRenderingTypes : EditorWindow
    {
        [MenuItem("Tools/Radiance Cascades/Test Rendering Types")]
        public static void ShowWindow()
        {
            GetWindow<TestRenderingTypes>("Test Rendering Types");
        }

        private void OnGUI()
        {
            GUILayout.Label("Radiance Cascades Rendering Type Tester", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("Current Settings:", EditorStyles.boldLabel);
            
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
            {
                RadianceCascades volumeComponent = null;
                foreach (var component in profile.components)
                {
                    if (component is RadianceCascades rc)
                    {
                        volumeComponent = rc;
                        break;
                    }
                }
                
                if (volumeComponent != null)
                {
                    GUILayout.Label($"Active: {volumeComponent.active}");
                    GUILayout.Label($"Rendering Type: {volumeComponent.RenderingType.value}");
                    GUILayout.Label($"Override State: {volumeComponent.RenderingType.overrideState}");
                }
                else
                {
                    GUILayout.Label("No RadianceCascades component found in profile!");
                }
            }
            else
            {
                GUILayout.Label("SampleSceneProfile.asset not found!");
            }

            GUILayout.Space(10);
            GUILayout.Label("Quick Test Buttons:", EditorStyles.boldLabel);

            if (GUILayout.Button("Enable Screen-Space Probes (Direction-First)"))
            {
                SetRenderingType(RenderingType.DirectionFirstProbes);
            }

            if (GUILayout.Button("Disable"))
            {
                SetRenderingType(RenderingType.None);
            }

            GUILayout.Space(10);
            GUILayout.Label("Instructions:", EditorStyles.boldLabel);
            GUILayout.Label("1. Click a button above to set rendering type");
            GUILayout.Label("2. Check Console for debug logs");
            GUILayout.Label("3. Check Frame Debugger for RC passes");
            GUILayout.Label("4. Look for visual differences in scene");
        }

        private void SetRenderingType(RenderingType type)
        {
            // Find the Volume Profile asset
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile == null)
            {
                Debug.LogError("SampleSceneProfile.asset not found!");
                return;
            }

            // Find the RadianceCascades component
            RadianceCascades volumeComponent = null;
            foreach (var component in profile.components)
            {
                if (component is RadianceCascades rc)
                {
                    volumeComponent = rc;
                    break;
                }
            }

            if (volumeComponent != null)
            {
                volumeComponent.RenderingType.overrideState = true;
                volumeComponent.RenderingType.value = type;
                volumeComponent.active = (type != RenderingType.None);
                
                Debug.Log($"Set RadianceCascades to {type} (Value: {(int)type})");
                EditorUtility.SetDirty(profile);
            }
            else
            {
                Debug.LogError("No RadianceCascades component found in profile!");
            }
        }
    }
}
