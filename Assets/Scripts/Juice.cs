using System.Collections;
using UnityEngine;

/// <summary>
/// Vurus hissi veren efektler: kamera sarsintisi, kisa donma (hit stop)
/// ve carpma noktasindan firlayan parcaciklar.
/// </summary>
public class Juice : MonoBehaviour
{
    Camera _camera;
    Vector3 _basePosition;

    ParticleSystem _particles;

    float _shakeTimer;
    float _shakeDuration;
    float _shakeStrength;

    Coroutine _hitStop;

    public void Build(Camera targetCamera)
    {
        _camera = targetCamera;
        _basePosition = _camera.transform.position;
        BuildParticles();
    }

    // ---------------------------------------------------------------- sarsinti

    public void Shake(float strength, float duration)
    {
        // Zaten daha guclu bir sarsinti varsa onu bozma.
        if (_shakeTimer > 0f && strength < _shakeStrength * (_shakeTimer / _shakeDuration)) return;

        _shakeStrength = strength;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    void LateUpdate()
    {
        if (_camera == null) return;

        if (_shakeTimer > 0f)
        {
            // Hit stop sirasinda da devam etmesi icin olceksiz zaman.
            _shakeTimer -= Time.unscaledDeltaTime;
            float falloff = Mathf.Clamp01(_shakeTimer / _shakeDuration);
            var offset = (Vector3)(Random.insideUnitCircle * (_shakeStrength * falloff * falloff));
            _camera.transform.position = _basePosition + offset;
        }
        else if (_camera.transform.position != _basePosition)
        {
            _camera.transform.position = _basePosition;
        }
    }

    // ---------------------------------------------------------------- hit stop

    /// <summary>Vurus aninda zamani cok kisa sureligine neredeyse durdurur.</summary>
    public void HitStop(float duration)
    {
        if (!isActiveAndEnabled) return;
        if (_hitStop != null) StopCoroutine(_hitStop);
        _hitStop = StartCoroutine(HitStopRoutine(duration));
    }

    /// <summary>
    /// Duraklatma menusu acilmadan once cagrilir; aksi halde hit stop
    /// bitiminde timeScale'i 1'e cekip oyunu kendiliginden devam ettirir.
    /// </summary>
    public void CancelHitStop()
    {
        if (_hitStop == null) return;
        StopCoroutine(_hitStop);
        _hitStop = null;
        Time.timeScale = 1f;
    }

    IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        _hitStop = null;
    }

    // ---------------------------------------------------------------- parcaciklar

    void BuildParticles()
    {
        var go = new GameObject("HitParticles");
        go.transform.SetParent(transform, false);
        _particles = go.AddComponent<ParticleSystem>();

        var main = _particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.gravityModifier = 0f;

        // Yayilimi kapatiyoruz; parcaciklari yalnizca Emit ile elle firlatiyoruz.
        var emission = _particles.emission;
        emission.enabled = false;

        var shape = _particles.shape;
        shape.enabled = false;

        var colorOverLifetime = _particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = _particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = PlaceholderArt.ParticleMaterial;
        renderer.sortingOrder = 6;

        _particles.Play();
    }

    /// <summary>Verilen noktadan, verilen yone dogru koni seklinde parcacik saçar.</summary>
    public void Burst(Vector2 position, Vector2 direction, Color color, int count, float speed, float spreadDegrees)
    {
        if (_particles == null) return;

        var normalized = direction.sqrMagnitude < 0.0001f ? Vector2.up : direction.normalized;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-spreadDegrees, spreadDegrees) * Mathf.Deg2Rad;
            var scattered = Rotate(normalized, angle);

            var emit = new ParticleSystem.EmitParams();
            emit.position = position;
            emit.velocity = scattered * Random.Range(speed * 0.35f, speed);
            emit.startColor = color;
            emit.startSize = Random.Range(0.05f, 0.15f);
            emit.startLifetime = Random.Range(0.18f, 0.45f);
            _particles.Emit(emit, 1);
        }
    }

    /// <summary>Sayi anindaki halka seklinde patlama.</summary>
    public void BurstRing(Vector2 position, Color color, int count, float speed)
    {
        if (_particles == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var emit = new ParticleSystem.EmitParams();
            emit.position = position;
            emit.velocity = direction * Random.Range(speed * 0.5f, speed);
            emit.startColor = color;
            emit.startSize = Random.Range(0.06f, 0.18f);
            emit.startLifetime = Random.Range(0.3f, 0.7f);
            _particles.Emit(emit, 1);
        }
    }

    static Vector2 Rotate(Vector2 value, float radians)
    {
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }
}
