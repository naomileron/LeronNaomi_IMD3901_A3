using UnityEngine;
using System;
using TMPro;
using Unity.Netcode;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private PlayerHospital ownerCheck;

    void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);

        ownerCheck = GetComponentInParent<PlayerHospital>();
    }

    void Start()
    {
        // If this UI is not on a player or player isn't owner, disable.
        if (ownerCheck == null || !ownerCheck.IsOwner)
        {
            enabled = false;
            return;
        }
    }

    void Update()
    {
        var timer = Timer.Instance;
        float remaining = Mathf.Max(0f, timer.GetRemainingSecs());
        TimeSpan t = TimeSpan.FromSeconds(remaining);
        timerText.text = t.ToString(@"m\:ss");
    }
}
