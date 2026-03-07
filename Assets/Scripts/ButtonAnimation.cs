using System.Collections;
using UnityEngine;

public class ButtonAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string pressTrigger = "Press";

    private void Awake()
    {
        //Force the animator to be on this object or its children
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
            
    }

    public void PlayPress()
    {
        //prevent errors from null references
        if (animator == null)
        {
            //Debug.LogError("[ButtonAnimation] Animator is NULL.", this);
            return;
        }

        //Debugging 
        //foreach (var p in animator.parameters)
            //Debug.Log($"    param: {p.name} ({p.type})", animator);
        
        //if the animator game object is inactive, return
        if (!animator.gameObject.activeInHierarchy)
        {
            //Debug.LogWarning("[ButtonAnimation] Animator GameObject is inactive, cannot play.", animator);
            return;
        }

        //play animation based on trigger. Reset to allow the anmation to play more than once
        animator.ResetTrigger(pressTrigger);
        animator.SetTrigger(pressTrigger);
    }

    private IEnumerator PlayWhenActive()
    {
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

        //Debug.LogWarning("[ButtonAnimation] Never became active in time, press animation skipped.", this);
    }
}