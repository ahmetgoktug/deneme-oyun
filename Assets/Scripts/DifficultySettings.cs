using UnityEngine;

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// Zorluk moduna gore top hizi ve bilgisayar rakibin yetenegini belirleyen ayarlar.
/// </summary>
public class DifficultySettings
{
    public Difficulty Level;
    public string DisplayName;

    /// <summary>Servis anindaki top hizi.</summary>
    public float BallStartSpeed;

    /// <summary>Her palet vurusunda hizin carpildigi katsayi.</summary>
    public float BallSpeedUpPerHit;

    public float BallMaxSpeed;

    /// <summary>Rakip paletin saniyedeki maksimum hareketi.</summary>
    public float AiMaxSpeed;

    /// <summary>Top kendisine dondukten sonra rakibin tepki vermesi icin gecen sure.</summary>
    public float AiReactionTime;

    /// <summary>Rakibin her raundda hedefine ekledigi rastgele sapma.</summary>
    public float AiAimError;

    /// <summary>
    /// 0 = rakip sadece topun anlik y degerini takip eder,
    /// 1 = duvar sekmelerini hesaplayip topun varacagi noktayi bilir.
    /// </summary>
    public float AiPrediction;

    public int ScoreToWin;

    public static DifficultySettings For(Difficulty level)
    {
        switch (level)
        {
            case Difficulty.Easy:
                return new DifficultySettings
                {
                    Level = Difficulty.Easy,
                    DisplayName = "EASY",
                    BallStartSpeed = 6.0f,
                    BallSpeedUpPerHit = 1.02f,
                    BallMaxSpeed = 10f,
                    AiMaxSpeed = 5.0f,
                    AiReactionTime = 0.30f,
                    AiAimError = 1.15f,
                    AiPrediction = 0f,
                    ScoreToWin = 5
                };

            case Difficulty.Hard:
                return new DifficultySettings
                {
                    Level = Difficulty.Hard,
                    DisplayName = "HARD",
                    BallStartSpeed = 9.0f,
                    BallSpeedUpPerHit = 1.06f,
                    BallMaxSpeed = 18f,
                    AiMaxSpeed = 11.0f,
                    AiReactionTime = 0.04f,
                    AiAimError = 0.18f,
                    AiPrediction = 1f,
                    ScoreToWin = 11
                };

            default:
                return new DifficultySettings
                {
                    Level = Difficulty.Medium,
                    DisplayName = "MEDIUM",
                    BallStartSpeed = 7.5f,
                    BallSpeedUpPerHit = 1.04f,
                    BallMaxSpeed = 14f,
                    AiMaxSpeed = 7.5f,
                    AiReactionTime = 0.14f,
                    AiAimError = 0.60f,
                    AiPrediction = 0.6f,
                    ScoreToWin = 7
                };
        }
    }

    /// <summary>Oyuncu paletinin hizi zorluktan bagimsizdir.</summary>
    public const float PlayerSpeed = 12f;
}
