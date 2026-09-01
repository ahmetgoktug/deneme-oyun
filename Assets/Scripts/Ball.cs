using UnityEngine;

/// <summary>
/// Top. Duvar sekmelerini fizik motoru halleder; palet vuruslarinda ise
/// klasik Pong davranisi icin cikis acisi vurus noktasina gore elle hesaplanir.
/// Hizlandikca rengi isinir ve izi uzar.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    /// <summary>Topun cok dikey gitmesini engelleyen siniri (yon vektorunun y bileseni).</summary>
    const float MaxVerticalRatio = 0.88f;

    /// <summary>Palet kenarindan vurulunca elde edilebilecek en genis acisi.</summary>
    const float MaxDeflectAngle = 55f;

    const float SquashDuration = 0.14f;

    /// <summary>Palet, carpma noktasi, hiz orani (0-1).</summary>
    public event System.Action<Paddle, Vector2, float> PaddleHit;

    /// <summary>Carpma noktasi, hiz orani (0-1).</summary>
    public event System.Action<Vector2, float> WallHit;

    Rigidbody2D _body;
    SpriteRenderer _renderer;
    TrailRenderer _trail;

    /// <summary>Gorsel alt nesne. Ezilme animasyonu yalnizca bunu olcekler,
    /// boylece collider'in yaricapi hic degismez.</summary>
    Transform _visual;

    Transform _glow;
    Vector3 _glowBaseScale;
    Vector3 _baseScale = Vector3.one;

    float _speed;
    float _startSpeed;
    float _speedUpPerHit;
    float _maxSpeed;
    float _squashTimer;
    Vector2 _squashAxis = Vector2.right;

    public bool IsActive { get; private set; }

    /// <summary>Bu ralli boyunca yapilan palet vurusu sayisi.</summary>
    public int RallyCount { get; private set; }

    public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;
    public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;

    /// <summary>0 = servis hizi, 1 = azami hiz. Renk, ses ve efekt siddeti buna bagli.</summary>
    public float SpeedRatio
    {
        get
        {
            float range = _maxSpeed - _startSpeed;
            return range <= 0.001f ? 0f : Mathf.Clamp01((_speed - _startSpeed) / range);
        }
    }

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
    }

    public void AttachVisuals(Transform visual, SpriteRenderer renderer, TrailRenderer trail, Transform glow)
    {
        _visual = visual;
        _renderer = renderer;
        _trail = trail;
        _glow = glow;

        if (visual != null) _baseScale = visual.localScale;
        if (glow != null) _glowBaseScale = glow.localScale;
    }

    public void Configure(DifficultySettings settings)
    {
        _speed = settings.BallStartSpeed;
        _startSpeed = settings.BallStartSpeed;
        _speedUpPerHit = settings.BallSpeedUpPerHit;
        _maxSpeed = settings.BallMaxSpeed;
    }

    /// <summary>Topu merkeze alip durdurur (menu, sayi arasi, oyun sonu).</summary>
    public void Park()
    {
        IsActive = false;
        RallyCount = 0;
        _speed = _startSpeed;
        _squashTimer = 0f;

        _body.linearVelocity = Vector2.zero;
        _body.position = Vector2.zero;
        transform.position = Vector3.zero;
        if (_visual != null) _visual.localScale = _baseScale;

        // Iz temizlenmezse top merkeze isinlanirken ekrani boydan boya cizer.
        if (_trail != null) _trail.Clear();
        ApplyHeatVisuals();
    }

    /// <summary>
    /// Merkezden servis atar. <paramref name="towardSign"/> -1 ise oyuncuya,
    /// +1 ise bilgisayara dogru gider.
    /// </summary>
    public void Launch(DifficultySettings settings, int towardSign)
    {
        Configure(settings);
        Park();

        float angle = Random.Range(-22f, 22f) * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(angle) * towardSign, Mathf.Sin(angle));
        _body.linearVelocity = direction.normalized * _speed;
        IsActive = true;
    }

    void FixedUpdate()
    {
        if (!IsActive) return;

        var velocity = _body.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        var direction = velocity.normalized;

        // Sekmelerde hiz kaybini telafi et ve topun dikeye saplanmasini onle.
        if (Mathf.Abs(direction.y) > MaxVerticalRatio)
        {
            float signY = direction.y >= 0f ? 1f : -1f;
            float signX = direction.x >= 0f ? 1f : -1f;
            float x = Mathf.Sqrt(1f - MaxVerticalRatio * MaxVerticalRatio);
            direction = new Vector2(signX * x, signY * MaxVerticalRatio);
        }

        _body.linearVelocity = direction * _speed;
    }

    void Update()
    {
        UpdateSquash();
    }

    // ---------------------------------------------------------------- carpismalar

    void OnCollisionEnter2D(Collision2D collision)
    {
        var paddle = collision.collider.GetComponent<Paddle>();
        var contact = collision.contactCount > 0 ? collision.GetContact(0).point : Position;

        if (paddle != null)
        {
            DeflectFrom(paddle, true);
            Squash(new Vector2(paddle.FacingSign, 0f));
            PaddleHit?.Invoke(paddle, contact, SpeedRatio);
        }
        else
        {
            var normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
            Squash(normal);
            WallHit?.Invoke(contact, SpeedRatio);
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Top palete gomulu kalirsa cikis yonunu tazele. Bu bir "yeni vurus"
        // degil, kurtarma islemi: ralli sayaci ve hiz artisi uygulanmaz.
        var paddle = collision.collider.GetComponent<Paddle>();
        if (paddle != null && Mathf.Sign(_body.linearVelocity.x) != paddle.FacingSign)
            DeflectFrom(paddle, false);
    }

    void DeflectFrom(Paddle paddle, bool countAsHit)
    {
        // Paletin neresine carptigina gore aci: ortada duz, uclarda genis.
        float offset = (Position.y - paddle.transform.position.y) / paddle.HalfHeight;
        offset = Mathf.Clamp(offset, -1f, 1f);

        float angle = offset * MaxDeflectAngle * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(angle) * paddle.FacingSign, Mathf.Sin(angle)).normalized;

        if (countAsHit)
        {
            _speed = Mathf.Min(_speed * _speedUpPerHit, _maxSpeed);
            RallyCount++;
        }

        _body.linearVelocity = direction * _speed;
        ApplyHeatVisuals();

        // Topu paletin on yuzune tasi; aksi halde ayni karede tekrar carpip
        // paletin icinde sikisabiliyor.
        float faceX = paddle.transform.position.x
                      + paddle.FacingSign * (paddle.HalfWidth + PongField.BallRadius + 0.02f);
        bool behindFace = paddle.FacingSign > 0 ? Position.x < faceX : Position.x > faceX;
        if (behindFace)
        {
            var corrected = new Vector2(faceX, Position.y);
            _body.position = corrected;
            transform.position = corrected;
        }
    }

    // ---------------------------------------------------------------- gorsel

    /// <summary>Hiz arttikca top beyazdan sicak turuncuya doner, izi ve halesi buyur.</summary>
    void ApplyHeatVisuals()
    {
        float heat = SpeedRatio;

        if (_renderer != null) _renderer.color = Color.Lerp(PongField.Neutral, PongField.BallHot, heat);

        if (_trail != null)
        {
            _trail.time = Mathf.Lerp(0.09f, 0.22f, heat);
            _trail.startColor = Color.Lerp(
                new Color(PongField.Neutral.r, PongField.Neutral.g, PongField.Neutral.b, 0.55f),
                new Color(PongField.BallHot.r, PongField.BallHot.g, PongField.BallHot.b, 0.8f), heat);
            _trail.endColor = new Color(PongField.BallHot.r, PongField.BallHot.g, PongField.BallHot.b, 0f);
        }

        if (_glow != null) _glow.localScale = _glowBaseScale * Mathf.Lerp(1f, 1.7f, heat);
    }

    /// <summary>Carpma yonunde eziklik: darbe hissini cok ucuza veriyor.</summary>
    void Squash(Vector2 normal)
    {
        _squashAxis = normal.sqrMagnitude < 0.0001f ? Vector2.right : normal.normalized;
        _squashTimer = SquashDuration;
    }

    void UpdateSquash()
    {
        if (_squashTimer <= 0f || _visual == null) return;

        _squashTimer = Mathf.Max(0f, _squashTimer - Time.unscaledDeltaTime);
        float t = _squashTimer / SquashDuration;

        // Carpma eksenine dogru bastir, dik eksende genislet.
        float along = 1f - 0.4f * t;
        float across = 1f + 0.3f * t;

        // Ekseni tam yakalamak yerine yatay/dikey agirligina gore harmanliyoruz.
        float horizontal = Mathf.Abs(_squashAxis.x);
        float scaleX = Mathf.Lerp(across, along, horizontal);
        float scaleY = Mathf.Lerp(along, across, horizontal);

        _visual.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);
    }
}
