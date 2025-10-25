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

            if (GUILayout.Button("Test All Rendering Types"))
            {
                TestAllRenderingTypes();
            }

            GUILayout.Space(10);
            GUILayout.Label("Instructions:", EditorStyles.boldLabel);
            GUILayout.Label("1. Click 'Check Volume Components' to see current state");
            GUILayout.Label("2. Click 'Test All Rendering Types' to cycle through all types");
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
                    Debug.Log($"  - Volume Resolution: {volumeComponent.VolumeResolution.value}");
                    Debug.Log($"  - Cascade Count: {volumeComponent.CascadeCount.value}");
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

        private void TestAllRenderingTypes()
        {
            Debug.Log("=== Testing All Rendering Types ===");
            
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

            var types = new[] 
            {
                RenderingType.Simple2dProbes,
                RenderingType.CubeMapProbes,
                RenderingType.DirectionFirstProbes,
                RenderingType.Probes3D
            };

            foreach (var type in types)
            {
                Debug.Log($"Testing {type} (Value: {(int)type})");
                volumeComponent.RenderingType.overrideState = true;
                volumeComponent.RenderingType.value = type;
                volumeComponent.active = true;
                
                EditorUtility.SetDirty(profile);
                
                // Force a repaint to see the change
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
                
                // Small delay to see the change
                System.Threading.Thread.Sleep(100);
            }
            
            Debug.Log("All rendering types tested. Check Console and Frame Debugger for results.");
        }
    }
}
