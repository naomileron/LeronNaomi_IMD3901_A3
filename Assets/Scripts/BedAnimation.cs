using System.Collections;
using UnityEngine;

public class BedAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string loweredBool = "Lowered";

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

    public void SetLowered(bool lowered)
    {
        //prevent errors from null references
        if (animator == null)
        {
            //Debug.LogError("[BedAnimation] Animator is NULL.", this);
            return;
        }
        //otherwise continue    
        if (!animator.gameObject.activeInHierarchy)
        {
            StartCoroutine(SetWhenActive(lowered));
            return;
        }

        //Debug.Log($"[BedAnimation] SetLowered({lowered})", this);
        animator.SetBool(loweredBool, lowered);

    }

    private IEnumerator SetWhenActive(bool lowered)
    {
        //play animation when triggered to do so by the bool
        for (int i = 0; i < 60; i++)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetBool(loweredBool, lowered);
                yield break;
            }
            yield return null;
        }
    }
}