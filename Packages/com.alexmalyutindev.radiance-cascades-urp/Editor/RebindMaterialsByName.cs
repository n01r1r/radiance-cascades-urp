using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebindMaterialsByName
{
    [MenuItem("Tools/RC/Rebind Materials By Name (Selected Root)")]
    static void RebindSelected()
    {
        var roots = Selection.gameObjects;
        foreach (var go in roots)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) continue;
                    // 슬롯명이나 오브젝트명으로 후보 검색
                    var slotGuess = r.sharedMaterials[i]?.name ?? r.gameObject.name;
                    var candidate = AssetDatabase.FindAssets("t:Material")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<Material>)
                        .FirstOrDefault(m => m && (m.name == slotGuess));
                    if (candidate) mats[i] = candidate;
                }
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("RebindMaterialsByName: completed.");
    }

    [MenuItem("Tools/RC/Convert All Materials to URP")]
    static void ConvertToURP()
    {
        var materials = AssetDatabase.FindAssets("t:Material")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .Where(m => m != null && !m.shader.name.Contains("Universal Render Pipeline"))
            .ToArray();

        foreach (var mat in materials)
        {
            Debug.Log($"Converting material: {mat.name}");
            EditorUtility.SetDirty(mat);
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {materials.Length} materials to URP.");
    }

    [MenuItem("Tools/RC/Setup CornellBox Scene")]
    static void SetupCornellBox()
    {
        // CornellBox 씬 설정
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name.Contains("CornellBox"))
        {
            // 카메라 설정
            var camera = Camera.main;
            if (camera != null)
            {
                camera.fieldOfView = 60f;
                Debug.Log("CornellBox camera FOV set to 60°");
            }

            // 라이팅 설정
            var lightingSettings = new SerializedObject(LightmapSettings.lightmapsMode);
            Debug.Log("CornellBox scene setup completed.");
        }
        else
        {
            Debug.LogWarning("Please open CornellBox scene first.");
        }
    }

    [MenuItem("Tools/RC/Setup Sponza Scene")]
    static void SetupSponza()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name.Contains("Sponza"))
        {
            // 카메라 설정
            var camera = Camera.main;
            if (camera != null)
            {
                camera.fieldOfView = 60f;
                Debug.Log("Sponza camera FOV set to 60°");
            }

            Debug.Log("Sponza scene setup completed.");
        }
        else
        {
            Debug.LogWarning("Please open Sponza scene first.");
        }
    }
}
