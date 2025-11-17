#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

// Trace and warm up pipeline state objects (PSOs) in a GraphicsStateCollection object.
public class GraphicsStateCollectionManager : MonoBehaviour
{
    public enum Mode
    {
        Tracing,
        WarmUp
    };
    public Mode mode;

    // Create a singleton so Unity uses the script only once across all scenes.
    public static GraphicsStateCollectionManager Instance;

    // Set up the collection of PSOs, and set where to store the files in the project folder.
    public GraphicsStateCollection[] collections;
    private const string k_CollectionFolderPath = "SharedAssets/GraphicsStateCollections/";

    // Create internal variables for the traced PSOs, and the file to output.
    private string m_OutputCollectionName;
    private GraphicsStateCollection m_GraphicsStateCollection;


    #if UNITY_EDITOR

    // Right click on the component to update the collection files list.
    [ContextMenu("Update collection list")]
    public void UpdateCollectionList()
    {
        string[] collectionGUIDs = AssetDatabase.FindAssets("t:GraphicsStateCollection", new[] {"Assets/" + k_CollectionFolderPath});
        collections = new GraphicsStateCollection[collectionGUIDs.Length];
        for (int i = 0; i < collections.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(collectionGUIDs[i]);
            collections[i] = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(path);
        }
        EditorUtility.SetDirty(this);
    }

    #endif

    // Find the available collection file that matches the current platform and quality level.
    private GraphicsStateCollection FindExistingCollection()
    {
        for (int i = 0; i < collections.Length; i++)
        {
            if (collections[i] != null)
            {
                if (collections[i].runtimePlatform == Application.platform &&
                    collections[i].graphicsDeviceType == SystemInfo.graphicsDeviceType &&
                    collections[i].qualityLevelName == QualitySettings.names[QualitySettings.GetQualityLevel()])
                {
                    return collections[i];
                }
            }
        }

        return null;
    }

    void Awake()
    {
        // Ensure there's only one instance of GraphicsStateCollectionManager.
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one instance of GraphicsStateCollectionManager is allowed!");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (mode == Mode.Tracing)
        {
            // Find the existing collection file based on current settings.
            m_GraphicsStateCollection = FindExistingCollection();

            if (m_GraphicsStateCollection != null)
            {
                // Use the existing file path if found.
                m_OutputCollectionName = k_CollectionFolderPath + m_GraphicsStateCollection.name;
            }
            else
            {
                // Create a new file if the file isn't found.

                // Get the name of the current quality level.
                int qualityLevelIndex = QualitySettings.GetQualityLevel();
                string qualityLevelName = QualitySettings.names[qualityLevelIndex];
                qualityLevelName = qualityLevelName.Replace(" ", "");

                // Set up the file path to use for the output collection.
                m_OutputCollectionName = string.Concat(k_CollectionFolderPath, "GfxState_", Application.platform, "_", SystemInfo.graphicsDeviceType.ToString(), "_", qualityLevelName);

                // Create a new GraphicsStateCollection.
                m_GraphicsStateCollection = new GraphicsStateCollection();
            }

            // Start tracing PSOs.
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log("Tracing started for GraphicsStateCollection by Scene '" + scene.name + "'.");
            m_GraphicsStateCollection.BeginTrace();
        }
        else
        {
            // Find the existing collection file based on current settings.
            GraphicsStateCollection collection = FindExistingCollection();

            // Warm up the PSOs.
            if (collection != null)
            {
                Scene scene = SceneManager.GetActiveScene();
                Debug.Log("Scene '" + scene.name + "' started warming up " + collection.totalGraphicsStateCount + " GraphicsState entries.");
                collection.WarmUp();
            }
        }
    }

    // For mobile platforms, data is additionally saved when focus is lost as OnDestroy() is not guaranteed to be called.
    void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            if (mode == Mode.Tracing && m_GraphicsStateCollection != null)
            {
                Debug.Log("Focus changed. Sending collection to Editor with " + m_GraphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
                m_GraphicsStateCollection.SendToEditor(m_OutputCollectionName);
            }
        }
    }

    void OnDestroy()
    {
        if (mode == Mode.Tracing && m_GraphicsStateCollection != null)
        {
            m_GraphicsStateCollection.EndTrace();
            Debug.Log("Sending collection to Editor with " + m_GraphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
            m_GraphicsStateCollection.SendToEditor(m_OutputCollectionName);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GraphicsStateCollectionManager))]
class GraphicsStateCollectionManagerEditor : Editor
{
    private const string k_Message =
        "Right click on this component to fill the collection list automatically with the files from the GraphicsStateCollections folder. \n" +
        "Collection files with irrelevant platforms will be excluded from build automatically according to current build target platform by GraphicsStateCollectionStripper.";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox(k_Message, MessageType.Info);
    }
}
#endif