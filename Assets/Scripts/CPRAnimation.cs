using UnityEngine;

public class CPRAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string triggerName = "Compress";

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            //Debug.LogError("[CPRAnimation] No Animator found.", this);
        }
            
    }

    public void PlayCompress()
    {
        if (animator == null)
        {
            return;
        }
        if (!animator.gameObject.activeInHierarchy)
        { 
            return; 
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }
}