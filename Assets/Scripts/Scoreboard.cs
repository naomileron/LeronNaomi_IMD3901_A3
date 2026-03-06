using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
    //Competitive UI
    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text greenScoreText;

    //Collaborative UI
    [SerializeField] private TMP_Text teamScoreText;

    private ScoreSystem blueScore;
    private ScoreSystem greenScore;

    private bool bound;

    private ConfigureGameMode gameModeConfig;

    private void Start()
    {
        gameModeConfig = FindFirstObjectByType<ConfigureGameMode>();

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
        if (blueScoreText != null) blueScoreText.text = blueScore != null ? $"BLUE HOSPITAL: {blueScore.Score.Value}" : "BLUE HOSPITAL: --";
        if (greenScoreText != null) greenScoreText.text = greenScore != null ? $"GREEN HOSPITAL: {greenScore.Score.Value}" : "GREEN HOSPITAL: --";

        if (blueScore == null || greenScore == null) return;

        // Subscribe once
        blueScore.Score.OnValueChanged += OnBlueChanged;
        greenScore.Score.OnValueChanged += OnGreenChanged;

        bound = true;
        CancelInvoke(nameof(TryBindOnce));

        UpdateTeamScore();
        UpdateModeUI();
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
        if (blueScoreText != null)
        {
            blueScoreText.text = $"BLUE HOSPITAL: {newV}";
        }

        UpdateTeamScore();
    }

    private void OnGreenChanged(int oldV, int newV)
    {
        if (greenScoreText != null)
        {
            greenScoreText.text = $"GREEN HOSPITAL: {newV}";
        }

        UpdateTeamScore();
    }

    private void UpdateTeamScore()
    {
        if (teamScoreText == null)
        {
            return;
        }
        if (blueScore == null || greenScore == null)
        {
            return;
        }

        int team = blueScore.Score.Value + greenScore.Score.Value;
        teamScoreText.text = $"PATIENTS SAVED: {team}";
    }

    private void UpdateModeUI()
    {
        bool coop = gameModeConfig != null && gameModeConfig.gameMode == GameModeType.Coop;

        if (coop)
        {
            if (blueScoreText != null)
            {
                blueScoreText.gameObject.SetActive(false);
            }
            if (greenScoreText != null)
            {
                greenScoreText.gameObject.SetActive(false);
            }
            if (teamScoreText != null)
            {
                teamScoreText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (blueScoreText != null)
            {
                blueScoreText.gameObject.SetActive(true);
            }
            if (greenScoreText != null)
            {
                greenScoreText.gameObject.SetActive(true);

            }
            if (teamScoreText != null)
            {
                teamScoreText.gameObject.SetActive(false);
            }
        }
    }
}