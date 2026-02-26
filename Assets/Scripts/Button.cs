using UnityEngine;

public class Button : MonoBehaviour
{

    public float pressDistance = 0.1f;
    public float pressSpeed = 3f;

    public GameObject targetObject;

    Vector3 startPos;
    bool isPressed = false;
    bool isAnimating = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        startPos = transform.localPosition;

    }

    // Update is called once per frame
    void Update()
    {
        
        Vector3 target = isPressed ? startPos - Vector3.up * pressDistance : startPos;

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, pressSpeed * Time.deltaTime);

        if(Vector3.Distance(transform.localPosition, target) < 0.001f)
        {
            if(isPressed)
            {
                isPressed = false;
            }
            else
            {
                isAnimating = false;
            }
        }

    }

    public void Press()
    {

        if(targetObject.activeSelf)
        {
            targetObject.SetActive(false);
        }
        else
        {
            targetObject.SetActive(true);
        }

        isPressed = true;
        isAnimating = true;

    }

}
