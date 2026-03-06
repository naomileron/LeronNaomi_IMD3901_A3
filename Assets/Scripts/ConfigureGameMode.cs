using UnityEngine;

public enum GameModeType
{ 
    Competitive,
    Coop
}

public enum ScoreMode
{
    Individual,
    Team
}

public class ConfigureGameMode : MonoBehaviour
{
    public GameModeType gameMode = GameModeType.Competitive;

    public HospitalType CoopHospital = HospitalType.Blue;

    public ScoreMode scoreMode = ScoreMode.Individual;
}
