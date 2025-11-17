using UnityEngine;

public class DisableIfTransitionManager : MonoBehaviour
{
    void Start()
    {
        if (SceneTransitionManager.IsAvailable())
        {
            Destroy(gameObject);
        }
    }
}