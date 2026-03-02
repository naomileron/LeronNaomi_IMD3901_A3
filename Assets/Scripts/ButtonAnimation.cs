using System.Collections;
using UnityEngine;

public class ButtonAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string pressTrigger = "Press";

    //public void PlayPress()
    //{
    //    if (animator == null)
    //    {
    //        Debug.LogError("[ButtonAnimation] Animator is NULL.", this);
    //        return;
    //    }

    //    // If object isn't active yet, wait and try again
    //    if (!animator.gameObject.activeInHierarchy)
    //    {
    //        StartCoroutine(PlayWhenActive());
    //        return;
    //    }

    //    animator.ResetTrigger(pressTrigger);
    //    animator.SetTrigger(pressTrigger);
    //}
    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    public void PlayPress()
    {
        Debug.Log("[ButtonAnimation] PlayPress called", this);

        if (animator == null)
        {
            Debug.LogError("[ButtonAnimation] Animator is NULL.", this);
            return;
        }

        Debug.Log($"[ButtonAnimation] AnimatorGO={animator.gameObject.name} activeInHierarchy={animator.gameObject.activeInHierarchy} enabled={animator.enabled}", animator);
        Debug.Log($"[ButtonAnimation] Controller={(animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : "NONE")}", animator);

        Debug.Log("[ButtonAnimation] Animator parameters:", animator);
        foreach (var p in animator.parameters)
            Debug.Log($"    param: {p.name} ({p.type})", animator);

        if (!animator.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[ButtonAnimation] Animator GameObject is inactive, cannot play.", animator);
            return;
        }

        animator.ResetTrigger(pressTrigger);
        animator.SetTrigger(pressTrigger);
    }

    private IEnumerator PlayWhenActive()
    {
        // wait up to ~1 second (60 frames) for safety
        for (int i = 0; i < 60; i++)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.ResetTrigger(pressTrigger);
                animator.SetTrigger(pressTrigger);
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning("[ButtonAnimation] Never became active in time, press animation skipped.", this);
    }
}