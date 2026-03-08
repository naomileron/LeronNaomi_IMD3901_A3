using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ResultsUI : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI resultsText;
    [SerializeField] private TextMeshProUGUI teamResultsText;

    public override void OnNetworkSpawn()
    {
        var state = SaveResults.Instance;

        if (state != null)
        {
            // Update immediately
            UpdateText();

            // Update again if results arrive later
            state.HasResults.OnValueChanged += OnResultsChanged;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        var state = SaveResults.Instance;

        if (state != null)
        {
            state.HasResults.OnValueChanged -= OnResultsChanged;
        }
    }

    private void OnResultsChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            UpdateText();
        }
    }

    private void UpdateText()
    {
        var state = SaveResults.Instance;

        if (state == null || !state.HasResults.Value)
        {
            if (resultsText != null)
            {
                resultsText.text = "Results not available.";
                resultsText.gameObject.SetActive(true);
            }

            if (teamResultsText != null)
            {
                teamResultsText.text = "Results not available.";
                teamResultsText.gameObject.SetActive(false);
            }

            return;
        }

        int blue = state.BlueScore.Value;
        int green = state.GreenScore.Value;

        GameModeType gameMode = (GameModeType)state.GameMode.Value;
        ScoreMode scoreMode = (ScoreMode)state.ScoreDisplayMode.Value;

        if (scoreMode == ScoreMode.Team)
        {
            int teamScore = blue + green;

            if (resultsText != null)
                resultsText.gameObject.SetActive(false);

            if (teamResultsText != null)
            {
                teamResultsText.gameObject.SetActive(true);
                teamResultsText.text = $"Shift Complete!\nYou saved {teamScore} patients!";
            }

            return;
        }

        if (teamResultsText != null)
            teamResultsText.gameObject.SetActive(false);

        if (resultsText != null)
            resultsText.gameObject.SetActive(true);

        if (blue > green)
        {
            resultsText.text =
                $"Blue Hospital Wins!\nFinal Scores\nBlue: {blue}\nGreen: {green}";
        }
        else if (green > blue)
        {
            resultsText.text =
                $"Green Hospital Wins!\nFinal Scores\nGreen: {green}\nBlue: {blue}";
        }
        else
        {
            resultsText.text =
                $"It's a tie!\nFinal Scores\nBlue: {blue}\nGreen: {green}";
        }
    }
}