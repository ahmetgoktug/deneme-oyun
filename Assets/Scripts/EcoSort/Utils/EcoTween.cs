using System;
using System.Collections;
using UnityEngine;

#if ECOSORT_DOTWEEN
using DG.Tweening;
#endif

namespace EcoSort.Utils
{
    public enum EcoEase
    {
        Linear,
        OutQuad,
        InOutQuad,
        OutCubic,
        OutBack,   // hafif tasma: kart yerine "otururken" tatmin edici his verir
        OutElastic
    }

    /// <summary>
    /// Calisan bir tween'e tutamak. Yeni tween baslatmadan once Kill() cagirarak
    /// ayni hedef uzerinde cakisan animasyonlari onleriz.
    /// </summary>
    public sealed class TweenHandle
    {
        public static readonly TweenHandle Done = new TweenHandle { Completed = true };

        Coroutine _routine;

        internal bool Completed;

#if ECOSORT_DOTWEEN
        Tween _tween;
        internal void Bind(Tween tween) { _tween = tween; }
#endif

        internal void Bind(Coroutine routine) { _routine = routine; }

        public bool IsActive => !Completed;

        public void Kill()
        {
            if (Completed) return;
            Completed = true;
#if ECOSORT_DOTWEEN
            if (_tween != null && _tween.IsActive()) _tween.Kill();
            _tween = null;
#endif
            if (_routine != null) EcoTween.StopRoutine(_routine);
            _routine = null;
        }
    }

    /// <summary>
    /// Ince bir animasyon katmani. DOTween projede varsa (ECOSORT_DOTWEEN define'i
    /// tanimliysa) DOTween kullanilir, yoksa ayni API coroutine tabanli calisir.
    /// Boylece oyun kodu tek bir cagri seklini bilir.
    ///
    /// DOTween'e gecmek icin:
    ///   Project Settings > Player > Other Settings > Scripting Define Symbols > ECOSORT_DOTWEEN
    /// </summary>
    public static class EcoTween
    {
        class Runner : MonoBehaviour { }

        static Runner _runner;

        static Runner Host
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("[EcoTween]") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
                return _runner;
            }
        }

        internal static void StopRoutine(Coroutine routine)
        {
            if (_runner != null && routine != null) _runner.StopCoroutine(routine);
        }

        // ---------------------------------------------------------------- hareket

        /// <summary>RectTransform'u hedef anchoredPosition degerine tasir.</summary>
        public static TweenHandle MoveAnchored(RectTransform target, Vector2 to, float duration,
            EcoEase ease = EcoEase.OutCubic, Action onComplete = null)
        {
            if (target == null) return Immediate(onComplete);
            if (duration <= 0f)
            {
                target.anchoredPosition = to;
                return Immediate(onComplete);
            }

#if ECOSORT_DOTWEEN
            var handle = new TweenHandle();
            var tween = target.DOAnchorPos(to, duration).SetEase(ToDoEase(ease)).SetUpdate(true);
            tween.OnComplete(() => { handle.Completed = true; onComplete?.Invoke(); });
            handle.Bind(tween);
            return handle;
#else
            Vector2 from = target.anchoredPosition;
            return Run(duration, ease,
                k => target.anchoredPosition = Vector2.LerpUnclamped(from, to, k),
                () => target != null,
                onComplete);
#endif
        }

        // ---------------------------------------------------------------- olcek

        public static TweenHandle Scale(Transform target, Vector3 to, float duration,
            EcoEase ease = EcoEase.OutBack, Action onComplete = null)
        {
            if (target == null) return Immediate(onComplete);
            if (duration <= 0f)
            {
                target.localScale = to;
                return Immediate(onComplete);
            }

#if ECOSORT_DOTWEEN
            var handle = new TweenHandle();
            var tween = target.DOScale(to, duration).SetEase(ToDoEase(ease)).SetUpdate(true);
            tween.OnComplete(() => { handle.Completed = true; onComplete?.Invoke(); });
            handle.Bind(tween);
            return handle;
#else
            Vector3 from = target.localScale;
            return Run(duration, ease,
                k => target.localScale = Vector3.LerpUnclamped(from, to, k),
                () => target != null,
                onComplete);
#endif
        }

        // ---------------------------------------------------------------- saydamlik

        public static TweenHandle Fade(CanvasGroup target, float to, float duration, Action onComplete = null)
        {
            if (target == null) return Immediate(onComplete);
            if (duration <= 0f)
            {
                target.alpha = to;
                return Immediate(onComplete);
            }

#if ECOSORT_DOTWEEN
            var handle = new TweenHandle();
            var tween = target.DOFade(to, duration).SetEase(Ease.Linear).SetUpdate(true);
            tween.OnComplete(() => { handle.Completed = true; onComplete?.Invoke(); });
            handle.Bind(tween);
            return handle;
#else
            float from = target.alpha;
            return Run(duration, EcoEase.Linear,
                k => target.alpha = Mathf.LerpUnclamped(from, to, k),
                () => target != null,
                onComplete);
#endif
        }

        /// <summary>Kisa "pop" vurusu: dogru eslesmede kartin nefes almasi icin.</summary>
        public static TweenHandle Punch(Transform target, float strength = 0.12f, float duration = 0.28f,
            Action onComplete = null)
        {
            if (target == null) return Immediate(onComplete);
            Vector3 baseScale = target.localScale;

#if ECOSORT_DOTWEEN
            var handle = new TweenHandle();
            var tween = target.DOPunchScale(baseScale * strength, duration, 8, 0.6f).SetUpdate(true);
            tween.OnComplete(() => { handle.Completed = true; onComplete?.Invoke(); });
            handle.Bind(tween);
            return handle;
#else
            return Run(duration, EcoEase.Linear,
                k =>
                {
                    // Sonumlenerek biten tek sinuzoid dalga.
                    float wave = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k) * strength;
                    target.localScale = baseScale * (1f + wave);
                },
                () => target != null,
                () =>
                {
                    if (target != null) target.localScale = baseScale;
                    onComplete?.Invoke();
                });
#endif
        }

        // ---------------------------------------------------------------- ic isleyis

        static TweenHandle Immediate(Action onComplete)
        {
            onComplete?.Invoke();
            return TweenHandle.Done;
        }

#if !ECOSORT_DOTWEEN
        static TweenHandle Run(float duration, EcoEase ease, Action<float> apply, Func<bool> alive, Action onComplete)
        {
            var handle = new TweenHandle();
            handle.Bind(Host.StartCoroutine(Lerp(handle, duration, ease, apply, alive, onComplete)));
            return handle;
        }

        static IEnumerator Lerp(TweenHandle handle, float duration, EcoEase ease, Action<float> apply,
            Func<bool> alive, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // unscaledDeltaTime: oyun duraklatilsa bile UI animasyonlari akici kalir.
                elapsed += Time.unscaledDeltaTime;
                if (!alive()) yield break;
                apply(Evaluate(ease, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            if (!alive()) yield break;
            apply(1f);
            handle.Completed = true;
            onComplete?.Invoke();
        }
#endif

        /// <summary>Normalize edilmis zamani (0..1) easing egrisinden gecirir.</summary>
        public static float Evaluate(EcoEase ease, float k)
        {
            switch (ease)
            {
                case EcoEase.OutQuad:
                    return 1f - (1f - k) * (1f - k);

                case EcoEase.InOutQuad:
                    return k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) * 0.5f;

                case EcoEase.OutCubic:
                    return 1f - Mathf.Pow(1f - k, 3f);

                case EcoEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
                }

                case EcoEase.OutElastic:
                {
                    if (k <= 0f) return 0f;
                    if (k >= 1f) return 1f;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * k) * Mathf.Sin((k * 10f - 0.75f) * c4) + 1f;
                }

                default:
                    return k;
            }
        }

#if ECOSORT_DOTWEEN
        static Ease ToDoEase(EcoEase ease)
        {
            switch (ease)
            {
                case EcoEase.OutQuad: return Ease.OutQuad;
                case EcoEase.InOutQuad: return Ease.InOutQuad;
                case EcoEase.OutCubic: return Ease.OutCubic;
                case EcoEase.OutBack: return Ease.OutBack;
                case EcoEase.OutElastic: return Ease.OutElastic;
                default: return Ease.Linear;
            }
        }
#endif
    }
}
