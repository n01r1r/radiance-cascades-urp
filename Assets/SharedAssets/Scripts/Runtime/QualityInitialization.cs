using System;
using UnityEngine;
using UnityEngine.Rendering;

public class QualityInitialization : MonoBehaviour
{
    private const string k_QualityPCLow = "PC Low";

    void Start()
    {
        SetQualityLevel();
    }

    void SetQualityLevel()
    {
#if UNITY_STANDALONE
        // OpenGL doesn't support the Decal DBuffer technique, so we need to switch the quality level to low that uses Screen Space instead
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
        {
            QualitySettings.SetQualityLevel(GetQualityLevelFromName(k_QualityPCLow));
        }
#endif
    }

    private int GetQualityLevelFromName(string qualityName)
    {
        string[] qualityNames = QualitySettings.names;
        return Array.IndexOf(qualityNames, qualityName);
    }
}