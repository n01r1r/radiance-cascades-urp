using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using AlexMalyutinDev.RadianceCascades;

namespace AlexMalyutinDev.RadianceCascades.Editor
{
    public class DebugRadianceCascades : EditorWindow
    {
        [MenuItem("Tools/Radiance Cascades/Debug Info")]
        public static void ShowWindow()
        {
            GetWindow<DebugRadianceCascades>("Debug RadianceCascades");
        }

        private void OnGUI()
        {
            GUILayout.Label("RadianceCascades Debug Information", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (GUILayout.Button("Check Volume Components"))
            {
                CheckVolumeComponents();
            }

            if (GUILayout.Button("Test Screen-Space Probes"))
            {
                TestScreenSpaceProbes();
            }

            GUILayout.Space(10);
            GUILayout.Label("Instructions:", EditorStyles.boldLabel);
            GUILayout.Label("1. Click 'Check Volume Components' to see current state");
            GUILayout.Label("2. Click 'Test Screen-Space Probes' to test the screen-space approach");
            GUILayout.Label("3. Check Console for debug logs");
            GUILayout.Label("4. Check Frame Debugger for RC passes");
        }

        private void CheckVolumeComponents()
        {
            Debug.Log("=== RadianceCascades Volume Components Check ===");
            
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
                    Debug.Log($"Found RadianceCascades component:");
                    Debug.Log($"  - Active: {volumeComponent.active}");
                    Debug.Log($"  - Rendering Type: {volumeComponent.RenderingType.value}");
                    Debug.Log($"  - Override State: {volumeComponent.RenderingType.overrideState}");
                    Debug.Log($"  - Ray Scale: {volumeComponent.RayScale.value}");
                    Debug.Log($"  - Enable Adaptive Ray Scale: {volumeComponent.EnableAdaptiveRayScale.value}");
                    Debug.Log($"  - Cascade Scale Factor: {volumeComponent.CascadeScaleFactor.value}");
                    Debug.Log($"  - Variance Influence: {volumeComponent.VarianceInfluence.value}");
                }
                else
                {
                    Debug.LogError("No RadianceCascades component found in profile!");
                }
            }
            else
            {
                Debug.LogError("SampleSceneProfile.asset not found!");
            }
        }

        private void TestScreenSpaceProbes()
        {
            Debug.Log("=== Testing Screen-Space Probes (Direction-First) ===");
            
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile == null)
            {
                Debug.LogError("SampleSceneProfile.asset not found!");
                return;
            }

            RadianceCascades volumeComponent = null;
            foreach (var component in profile.components)
            {
                if (component is RadianceCascades rc)
                {
                    volumeComponent = rc;
                    break;
                }
            }

            if (volumeComponent == null)
            {
                Debug.LogError("No RadianceCascades component found in profile!");
                return;
            }

            Debug.Log($"Testing {RenderingType.DirectionFirstProbes} (Value: {(int)RenderingType.DirectionFirstProbes})");
            volumeComponent.RenderingType.overrideState = true;
            volumeComponent.RenderingType.value = RenderingType.DirectionFirstProbes;
            volumeComponent.active = true;
            
            EditorUtility.SetDirty(profile);
            
            // Force a repaint to see the change
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
            
            Debug.Log("Screen-space probes enabled. Check Console and Frame Debugger for results.");
        }
    }
}
