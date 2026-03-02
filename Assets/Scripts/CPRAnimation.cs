using UnityEngine;

public class CPRAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string compressTrigger = "Compress";

    private void Reset()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    public void PlayCompression()
    {
        if (animator == null)
        {
            Debug.LogError("[HandsAnimation] Animator is NULL.", this);
            return;
        }

        // If you ever run into the "goActive=False" issue again, this prevents it:
        if (!animator.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[HandsAnimation] Hands not active yet, skipping compression.", this);
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[HandsAnimation] Animator has no controller assigned.", animator);
            return;
        }

        animator.ResetTrigger(compressTrigger);
        animator.SetTrigger(compressTrigger);
    }
}