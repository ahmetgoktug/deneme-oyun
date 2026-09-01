using UnityEngine;

/// <summary>
/// Oyuncu ve bilgisayar paletlerinin ortak davranisi: hedef y degerine
/// hiz sinirina uyarak ilerlemek ve saha disina tasmamak.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Paddle : MonoBehaviour
{
    public float HalfHeight { get; private set; }
    public float HalfWidth { get; private set; }

    /// <summary>Bu paletin topu gonderdigi yon: sol palet icin +1, sag palet icin -1.</summary>
    public int FacingSign { get; private set; }

    /// <summary>Menu ve sayi aralarinda paletleri dondurmak icin.</summary>
    public bool Active { get; set; }

    public float Speed { get; set; }

    protected Rigidbody2D Body;
    float _limit;

    SpriteRenderer _renderer;

    /// <summary>Gorsel alt nesne; ezilme animasyonu collider'a dokunmasin diye ayri.</summary>
    Transform _visual;

    Transform _glow;
    Color _baseColor;
    Vector3 _baseScale = Vector3.one;
    Vector3 _glowBaseScale;
    float _hitTimer;

    /// <summary>Vurus efektinin toplam suresi.</summary>
    const float HitFeedbackDuration = 0.18f;

    public void Setup(Vector2 size, int facingSign, Transform visual, SpriteRenderer renderer)
    {
        Body = GetComponent<Rigidbody2D>();
        HalfWidth = size.x * 0.5f;
        HalfHeight = size.y * 0.5f;
        FacingSign = facingSign;
        _limit = PongField.HalfHeight - HalfHeight;

        _visual = visual;
        _renderer = renderer;
        if (_renderer != null) _baseColor = _renderer.color;
        if (_visual != null) _baseScale = _visual.localScale;
    }

    /// <summary>Paletin arkasindaki isik lekesini bagla; vuruslarda birlikte parlar.</summary>
    public void AttachGlow(Transform glow)
    {
        _glow = glow;
        if (glow != null) _glowBaseScale = glow.localScale;
    }

    /// <summary>Top bu palete carptiginda cagrilir: kisa bir parlama ve ezilme.</summary>
    public void PlayHitFeedback()
    {
        _hitTimer = HitFeedbackDuration;
    }

    void Update()
    {
        if (_hitTimer <= 0f) return;

        // Hit stop sirasinda da akmasi icin olceksiz zaman kullaniyoruz.
        _hitTimer = Mathf.Max(0f, _hitTimer - Time.unscaledDeltaTime);
        float t = _hitTimer / HitFeedbackDuration;

        if (_renderer != null) _renderer.color = Color.Lerp(_baseColor, Color.white, t);

        // Carpma anininda enine yayilip boyuna kisalir, sonra eski haline doner.
        float squash = 1f + 0.45f * t;
        if (_visual != null)
            _visual.localScale = new Vector3(_baseScale.x * squash, _baseScale.y * (1f - 0.12f * t), _baseScale.z);

        if (_glow != null) _glow.localScale = _glowBaseScale * (1f + 0.6f * t);
    }

    void FixedUpdate()
    {
        if (!Active || Body == null) return;

        float dt = Time.fixedDeltaTime;
        float target = Mathf.Clamp(ComputeTargetY(dt), -_limit, _limit);
        float next = Mathf.MoveTowards(Body.position.y, target, Speed * dt);
        Body.MovePosition(new Vector2(Body.position.x, next));
    }

    /// <summary>Paletin ulasmak istedigi y degeri. Hiz siniri cagiran tarafta uygulanir.</summary>
    protected abstract float ComputeTargetY(float deltaTime);

    public virtual void ResetToCenter()
    {
        var centered = new Vector2(transform.position.x, 0f);
        transform.position = centered;
        if (Body != null) Body.position = centered;
    }
}
