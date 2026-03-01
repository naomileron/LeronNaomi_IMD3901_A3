using UnityEngine;

public class CodeBlueSpawner : MonoBehaviour
{
    public GameObject previewPrefab;

    [ContextMenu("Spawn Preview")]
    void Spawn()
    {
        Instantiate(previewPrefab, transform.position, transform.rotation);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
