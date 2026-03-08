using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

//Automatically starts the client and host instead of having to do it in the inspector
//Used ChatGPT to figure this out
public class AutoHostClient : MonoBehaviour
{
    public string sceneToLoad;

    public void StartGame()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            //first player to join becomes the host
            bool isHost = NetworkManager.Singleton.StartHost();

            //second player to join becomes the client
            if (!isHost)
            {
                NetworkManager.Singleton.StartClient();
            }
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
