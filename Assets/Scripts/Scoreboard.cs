using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text greenScoreText;

    private ScoreSystem blueScore;
    private ScoreSystem greenScore;

    private bool bound;

    private void Start()
    {
        // Try a few times while Netcode finishes spawning
        InvokeRepeating(nameof(TryBindOnce), 0.1f, 0.25f);
    }

    private void OnDestroy()
    {
        Unbind();
        CancelInvoke(nameof(TryBindOnce));
    }

    private void TryBindOnce()
    {
        if (bound) { CancelInvoke(nameof(TryBindOnce)); return; }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        blueScore = null;
        greenScore = null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = client.PlayerObject;
            if (po == null) continue;

            var ph = po.GetComponent<PlayerHospital>();
            var ss = po.GetComponent<ScoreSystem>();
            if (ph == null || ss == null) continue;

            if (ph.Hospital.Value == HospitalType.Blue) blueScore = ss;
            else if (ph.Hospital.Value == HospitalType.Green) greenScore = ss;
        }

        // Show placeholders if not ready yet
        if (blueScoreText != null) blueScoreText.text = blueScore != null ? $"Blue: {blueScore.Score.Value}" : "Blue: --";
        if (greenScoreText != null) greenScoreText.text = greenScore != null ? $"Green: {greenScore.Score.Value}" : "Green: --";

        if (blueScore == null || greenScore == null) return;

        // Subscribe ONCE
        blueScore.Score.OnValueChanged += OnBlueChanged;
        greenScore.Score.OnValueChanged += OnGreenChanged;

        bound = true;
        CancelInvoke(nameof(TryBindOnce));
    }

    private void Unbind()
    {
        if (blueScore != null) blueScore.Score.OnValueChanged -= OnBlueChanged;
        if (greenScore != null) greenScore.Score.OnValueChanged -= OnGreenChanged;
        blueScore = null;
        greenScore = null;
        bound = false;
    }

    private void OnBlueChanged(int oldV, int newV)
    {
        if (blueScoreText != null) blueScoreText.text = $"Blue: {newV}";
    }

    private void OnGreenChanged(int oldV, int newV)
    {
        if (greenScoreText != null) greenScoreText.text = $"Green: {newV}";
    }
}