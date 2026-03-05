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
        //if (timer == null || timerText == null)
        //{
        //    timerText.text = "--:--.--";
        //    return;
        //}

        float remaining = Mathf.Max(0f, timer.GetRemainingSecs());
        TimeSpan t = TimeSpan.FromSeconds(remaining);
        timerText.text = t.ToString(@"m\:ss");
    }

    //[SerializeField] private TextMeshProUGUI timerText;

    //private Timer timer;
    //private bool hooked;

    //void Awake()
    //{
    //    if (timerText == null)
    //    {
    //        timerText = GetComponentInChildren<TextMeshProUGUI>();
    //    }
    //}

    //public override void OnNetworkSpawn()
    //{
    //    if (!IsOwner)
    //    {
    //        enabled = false;
    //        return;
    //    }

    //    timer = FindFirstObjectByType<Timer>();

    //    FindTimer();
    //}

    //// Update is called once per frame
    //void Update()
    //{
    //    if (timer == null || timerText == null)
    //    {
    //        return;
    //    }

    //    float remaining = Mathf.Max(0f, timer.GetRemainingSecs());
    //    TimeSpan t = TimeSpan.FromSeconds(remaining);

    //    timerText.text = t.ToString(@"m\:ss");
    //}

    //private void FindTimer()
    //{
    //    timer = FindFirstObjectByType<Timer>();
    //}
}
