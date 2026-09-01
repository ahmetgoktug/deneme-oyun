using UnityEngine;

namespace EcoSort.Utils
{
    /// <summary>
    /// UI sesleri icin tek noktali, kurulum gerektirmeyen calar.
    /// ASMR hissi icin ust uste binen "pop" seslerinde hafif pitch varyasyonu uygular.
    /// </summary>
    public static class EcoAudio
    {
        static AudioSource _source;
        static float _lastPlayTime = -1f;

        static AudioSource Source
        {
            get
            {
                if (_source != null) return _source;
                var go = new GameObject("[EcoAudio]") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;   // 2D
                return _source;
            }
        }

        /// <summary>Tek atimlik ses calar. pitchJitter, arka arkaya seslerin robotik durmasini onler.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;

            // Ayni karede tetiklenen kopya sesler kulagi tirmalar; cok kisa bir kapi koyuyoruz.
            if (Mathf.Approximately(_lastPlayTime, Time.unscaledTime)) volume *= 0.6f;
            _lastPlayTime = Time.unscaledTime;

            Source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            Source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Kombo/zincir seslerinde her adimda tizlesen tatmin edici merdiven.</summary>
        public static void PlayStep(AudioClip clip, int step, float volume = 1f)
        {
            if (clip == null) return;
            _lastPlayTime = Time.unscaledTime;
            Source.pitch = Mathf.Clamp(1f + step * 0.07f, 0.5f, 2f);
            Source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
