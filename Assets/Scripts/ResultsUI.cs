using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ResultsUI : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI resultsText;

    public override void OnNetworkSpawn()
    {
        UpdateText();
    }

    private void UpdateText()
    {
        if (resultsText == null) return;

        var state = SaveResults.Instance;
        if (state == null || !state.HasResults.Value)
        {
            resultsText.text = "Results not available.";
            return;
        }

        int blue = state.BlueScore.Value;
        int green = state.GreenScore.Value;

        GameModeType gameMode = (GameModeType)state.GameMode.Value;
        ScoreMode scoreMode = (ScoreMode)state.ScoreDisplayMode.Value;

        if (scoreMode == ScoreMode.Team)
        {
            int teamScore = blue + green;
            resultsText.text = $"Shift Over! You saved {teamScore} patients\n";
        }

        if (blue > green)
        {
            resultsText.text = $"Blue Hospital Wins!\n\nFinal Scores\nBlue: {blue}\nGreen: {green}";
        }  
        else if (green > blue)
        {
            resultsText.text = $"Green Hospital Wins!\n\nFinal Scores\nGreen: {green}\nBlue: {blue}";
        }
        else
        {
            resultsText.text = $"It's a tie!\n\nFinal Scores\nBlue: {blue}\nGreen: {green}";
        }
            
    }
}