using System.Collections;
using UnityEngine;

public class BedAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string loweredBool = "Lowered";

    public void SetLowered(bool lowered)
    {
        if (animator == null)
        {
            Debug.LogError("[BedVisual] Animator is NULL.", this);
            return;
        }

        if (!animator.gameObject.activeInHierarchy)
        {
            StartCoroutine(SetWhenActive(lowered));
            return;
        }

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

        Debug.LogWarning("[BedVisual] Never became active in time, lowered state skipped.", this);
    }
}