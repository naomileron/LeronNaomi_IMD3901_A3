using UnityEngine;

public class KeepNetworkManager : MonoBehaviour
{
    private static KeepNetworkManager instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}