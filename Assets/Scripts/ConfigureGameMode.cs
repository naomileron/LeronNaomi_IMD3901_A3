using UnityEngine;

//Tells other script what logic to use based on the game mode
public enum GameModeType
{ 
    Competitive,
    Coop
}

//Scoring logic also changes based on the game mode
public enum ScoreMode
{
    Individual,
    Team
}

public class ConfigureGameMode : MonoBehaviour
{
    public GameModeType gameMode = GameModeType.Competitive;

    //if in co-op mode, just assign everything to the blue hospital
    public HospitalType CoopHospital = HospitalType.Blue;

    public ScoreMode scoreMode = ScoreMode.Individual;
}
