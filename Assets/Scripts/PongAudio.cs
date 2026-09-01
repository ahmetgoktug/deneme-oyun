using UnityEngine;

/// <summary>
/// Tum ses efektleri calisma aninda dalga formundan uretilir; projede
/// tek bir ses dosyasi yoktur. Klasik Pong bip'lerinin biraz sicak hali.
/// </summary>
public class PongAudio : MonoBehaviour
{
    enum Wave
    {
        Sine,
        Square,
        Triangle,
        Saw,
        Noise
    }

    const int SampleRate = 44100;
    const int VoiceCount = 8;

    AudioSource[] _voices;
    int _nextVoice;

    AudioClip _paddleHit;
    AudioClip _wallHit;
    AudioClip _playerScored;
    AudioClip _aiScored;
    AudioClip _countdownTick;
    AudioClip _launch;
    AudioClip _win;
    AudioClip _lose;
    AudioClip _uiHover;
    AudioClip _uiClick;

    public void Build()
    {
        _voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.bypassReverbZones = true;
            _voices[i] = source;
        }

        // Palet vurusu: kisa, govdeli kare dalga. Ralli uzadikca pitch yukselecek.
        _paddleHit = Tone("PaddleHit", 500f, 0.075f, Wave.Square, 0.004f, 0.9f, 0.42f);
        // Duvar: daha bogus ve alcak.
        _wallHit = Tone("WallHit", 260f, 0.055f, Wave.Triangle, 0.003f, 0.75f, 0.34f);

        // Sayi sesleri: oyuncu icin yukselen, bilgisayar icin alcalan ucluk.
        _playerScored = Sequence("PlayerScored", new[] { 523.25f, 659.25f, 783.99f }, 0.085f, Wave.Square, 0.34f);
        _aiScored = Sequence("AiScored", new[] { 392f, 311.13f, 233.08f }, 0.1f, Wave.Square, 0.32f);

        _countdownTick = Tone("Tick", 720f, 0.06f, Wave.Sine, 0.004f, 0.85f, 0.3f);
        _launch = Sweep("Launch", 300f, 900f, 0.16f, Wave.Triangle, 0.32f);

        _win = Sequence("Win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, Wave.Square, 0.36f);
        _lose = Sequence("Lose", new[] { 440f, 349.23f, 293.66f, 220f }, 0.14f, Wave.Saw, 0.3f);

        _uiHover = Tone("UiHover", 880f, 0.035f, Wave.Sine, 0.002f, 0.95f, 0.16f);
        _uiClick = Tone("UiClick", 640f, 0.05f, Wave.Square, 0.002f, 0.92f, 0.26f);
    }

    // ---------------------------------------------------------------- calma

    /// <summary><paramref name="intensity"/> 0-1 arasi; ralli hizlandikca sesi tizlestirir.</summary>
    public void PlayPaddleHit(float intensity)
    {
        Play(_paddleHit, Mathf.Lerp(0.85f, 1.65f, Mathf.Clamp01(intensity)), 1f);
    }

    public void PlayWallHit()
    {
        Play(_wallHit, Random.Range(0.94f, 1.08f), 0.8f);
    }

    public void PlayScore(bool playerScored)
    {
        Play(playerScored ? _playerScored : _aiScored, 1f, 1f);
    }

    public void PlayCountdownTick(int remaining)
    {
        // Son saniyeye dogru tizlesir.
        Play(_countdownTick, remaining <= 1 ? 1.35f : 1f, 0.8f);
    }

    public void PlayLaunch() { Play(_launch, 1f, 0.75f); }
    public void PlayWin() { Play(_win, 1f, 1f); }
    public void PlayLose() { Play(_lose, 1f, 1f); }
    public void PlayUiHover() { Play(_uiHover, Random.Range(0.97f, 1.05f), 1f); }
    public void PlayUiClick() { Play(_uiClick, 1f, 1f); }

    void Play(AudioClip clip, float pitch, float volume)
    {
        if (clip == null || _voices == null) return;

        var source = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;

        source.Stop();
        source.clip = clip;
        source.pitch = pitch;
        source.volume = volume;
        source.Play();
    }

    // ---------------------------------------------------------------- sentez

    static AudioClip Tone(string name, float frequency, float duration, Wave wave,
        float attack, float decay, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
        var data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / SampleRate;
            float envelope = Envelope(time, duration, attack, decay);
            data[i] = Sample(wave, time * frequency) * envelope * amplitude;
        }

        Soften(data, 0.35f);
        return ToClip(name, data);
    }

    /// <summary>Frekansi baslangictan bitise kaydiran ton (servis sesi).</summary>
    static AudioClip Sweep(string name, float fromHz, float toHz, float duration, Wave wave, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
        var data = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / SampleRate;
            float t = (float)i / sampleCount;
            float frequency = Mathf.Lerp(fromHz, toHz, t * t);

            // Frekans degistigi icin fazi adim adim biriktiriyoruz.
            phase += frequency / SampleRate;
            data[i] = Sample(wave, phase) * Envelope(time, duration, 0.01f, 0.6f) * amplitude;
        }

        Soften(data, 0.4f);
        return ToClip(name, data);
    }

    /// <summary>Art arda calan notalar (sayi ve oyun sonu jingle'lari).</summary>
    static AudioClip Sequence(string name, float[] frequencies, float noteDuration, Wave wave, float amplitude)
    {
        int perNote = Mathf.Max(1, Mathf.RoundToInt(SampleRate * noteDuration));
        var data = new float[perNote * frequencies.Length];

        for (int n = 0; n < frequencies.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float time = (float)i / SampleRate;
                float envelope = Envelope(time, noteDuration, 0.005f, 0.7f);
                data[n * perNote + i] = Sample(wave, time * frequencies[n]) * envelope * amplitude;
            }
        }

        Soften(data, 0.35f);
        return ToClip(name, data);
    }

    static float Sample(Wave wave, float phase)
    {
        switch (wave)
        {
            case Wave.Square:
                return Mathf.Sin(phase * Mathf.PI * 2f) >= 0f ? 1f : -1f;
            case Wave.Triangle:
                return Mathf.PingPong(phase * 2f, 1f) * 2f - 1f;
            case Wave.Saw:
                return (phase - Mathf.Floor(phase)) * 2f - 1f;
            case Wave.Noise:
                return Random.Range(-1f, 1f);
            default:
                return Mathf.Sin(phase * Mathf.PI * 2f);
        }
    }

    /// <summary>Kisa atak + ustel sonum. Tik sesi olmadan baslayip yumusak biter.</summary>
    static float Envelope(float time, float duration, float attack, float decay)
    {
        if (time < attack) return time / Mathf.Max(attack, 0.0001f);

        float remaining = 1f - (time - attack) / Mathf.Max(duration - attack, 0.0001f);
        return Mathf.Clamp01(Mathf.Pow(Mathf.Max(remaining, 0f), 1f + decay * 3f));
    }

    /// <summary>Tek kutuplu alcak geciren filtre: kare dalganin cizirtisini alir.</summary>
    static void Soften(float[] data, float amount)
    {
        float previous = 0f;
        float a = Mathf.Clamp01(1f - amount);
        for (int i = 0; i < data.Length; i++)
        {
            previous += a * (data[i] - previous);
            data[i] = previous;
        }
    }

    static AudioClip ToClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
