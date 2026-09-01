using UnityEngine;

/// <summary>
/// Sahanin sabit olculeri. Kamera bu olculere gore ayarlanir, boylece
/// oyun ekran oraninden bagimsiz olarak hep ayni sekilde oynanir.
/// </summary>
public static class PongField
{
    /// <summary>Oyun alaninin merkezden sag/sol duvara olan mesafesi.</summary>
    public const float HalfWidth = 8.4f;

    /// <summary>Ust/alt duvarin ic yuzeyinin merkeze olan mesafesi.</summary>
    public const float HalfHeight = 4.6f;

    /// <summary>Paletlerin merkeze olan yatay mesafesi.</summary>
    public const float PaddleX = 7.6f;

    /// <summary>Topun bu cizgiyi gecmesi sayi demektir.</summary>
    public const float GoalLine = HalfWidth + 0.8f;

    public const float WallThickness = 0.4f;
    public const float BallRadius = 0.18f;

    public static readonly Vector2 PaddleSize = new Vector2(0.32f, 1.9f);

    // Placeholder palet: koyu zemin, acik nesneler.
    public static readonly Color Background = new Color(0.043f, 0.058f, 0.098f, 1f);
    public static readonly Color Neutral = new Color(0.878f, 0.925f, 1f, 1f);
    public static readonly Color PlayerColor = new Color(0.42f, 0.89f, 1f, 1f);
    public static readonly Color AiColor = new Color(1f, 0.478f, 0.42f, 1f);
    public static readonly Color NetColor = new Color(0.878f, 0.925f, 1f, 0.18f);

    /// <summary>Top azami hiza yaklastikca bu renge doner.</summary>
    public static readonly Color BallHot = new Color(1f, 0.78f, 0.35f, 1f);

    /// <summary>Duvarlar sonuk: parlak birakilirsa bloom tum ekrani sisliyor.</summary>
    public static readonly Color WallColor = new Color(0.34f, 0.40f, 0.55f, 1f);
}
