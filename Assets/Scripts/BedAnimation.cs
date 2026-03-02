using System.Collections;
using UnityEngine;

public class BedAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string loweredBool = "Lowered";

    private void Awake()
    {
        // FORCE the animator to be the one on THIS object (or its children)
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        Debug.Log($"[BedAnimation] Awake on {name} -> animatorGO={(animator ? animator.gameObject.name : "NULL")} controller={(animator && animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : "NONE")}", this);
    }

    public void SetLowered(bool lowered)
    {
        if (animator == null)
        {
            Debug.LogError("[BedAnimation] Animator is NULL.", this);
            return;
        }

        if (!animator.gameObject.activeInHierarchy)
        {
            StartCoroutine(SetWhenActive(lowered));
            return;
        }

        Debug.Log($"[BedAnimation] SetLowered({lowered})", this);
        animator.SetBool(loweredBool, lowered);

    }

    private IEnumerator SetWhenActive(bool lowered)
    {
        for (int i = 0; i < 60; i++)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetBool(loweredBool, lowered);
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning("[BedAnimation] Never became active in time, lowered state skipped.", this);
    }
}