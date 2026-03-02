using System.Collections;
using UnityEngine;

public class ButtonAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string pressTrigger = "Press";

    public void PlayPress()
    {
        if (animator == null)
        {
            Debug.LogError("[ButtonAnimation] Animator is NULL.", this);
            return;
        }

        // If object isn't active yet, wait and try again
        if (!animator.gameObject.activeInHierarchy)
        {
            StartCoroutine(PlayWhenActive());
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