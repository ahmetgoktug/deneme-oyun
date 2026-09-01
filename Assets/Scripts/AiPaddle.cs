using UnityEngine;

/// <summary>
/// Bilgisayar rakip. Zorluk uc parametreyle ayarlanir:
/// tepki suresi, nisan hatasi ve topun varacagi noktayi tahmin etme becerisi.
/// </summary>
public class AiPaddle : Paddle
{
    Ball _ball;
    DifficultySettings _settings;

    float _errorOffset;
    float _reactionTimer;
    bool _wasIncoming;

    public void Bind(Ball ball, DifficultySettings settings)
    {
        _ball = ball;
        _settings = settings;
        Speed = settings.AiMaxSpeed;
        _errorOffset = 0f;
        _reactionTimer = 0f;
        _wasIncoming = false;
    }

    protected override float ComputeTargetY(float deltaTime)
    {
        if (_ball == null || _settings == null || !_ball.IsActive) return 0f;

        var velocity = _ball.Velocity;
        // Top bize doguru geliyor mu? Sag palet icin FacingSign = -1, yani vx > 0 olmali.
        bool incoming = velocity.x * -FacingSign > 0f;

        if (!incoming)
        {
            // Top rakipteyken merkeze donerek bekle.
            _wasIncoming = false;
            _reactionTimer = 0f;
            return 0f;
        }

        if (!_wasIncoming)
        {
            // Yeni bir raunt basladi: tepki sayacini ve sapmayi sifirla.
            _wasIncoming = true;
            _reactionTimer = 0f;
            _errorOffset = Random.Range(-_settings.AiAimError, _settings.AiAimError);
        }

        _reactionTimer += deltaTime;
        if (_reactionTimer < _settings.AiReactionTime) return Body.position.y;

        float followY = _ball.Position.y;
        float predictedY = PredictInterceptY();
        return Mathf.Lerp(followY, predictedY, _settings.AiPrediction) + _errorOffset;
    }

    /// <summary>
    /// Topun paletin bulundugu x'e ulastiginda hangi y'de olacagini,
    /// ust/alt duvar sekmelerini de hesaba katarak bulur.
    /// </summary>
    float PredictInterceptY()
    {
        var position = _ball.Position;
        var velocity = _ball.Velocity;
        if (Mathf.Abs(velocity.x) < 0.01f) return position.y;

        float time = (transform.position.x - position.x) / velocity.x;
        if (time <= 0f) return position.y;

        float rawY = position.y + velocity.y * time;

        // Duvarlar arasindaki sekmeleri ucgen dalga ile katla.
        float half = PongField.HalfHeight - PongField.BallRadius;
        float span = 2f * half;
        float folded = Mathf.Repeat(rawY + half, 2f * span);
        if (folded > span) folded = 2f * span - folded;
        return folded - half;
    }

    public override void ResetToCenter()
    {
        base.ResetToCenter();
        _reactionTimer = 0f;
        _wasIncoming = false;
        _errorOffset = 0f;
    }
}
