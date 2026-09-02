using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.Utils
{
    /// <summary>
    /// Canvas uzerinde calisan, prefab gerektirmeyen minik konfeti patlamasi.
    ///
    /// ParticleSystem yerine UI Image kullanir: kartlarla ayni Canvas'ta ciziLdigi
    /// icin siralama (sorting) sorunu cikmaz ve mobilde ek bir render gecisi acmaz.
    /// Parcaciklar havuzlanir; patlama bitince nesneler yok edilmeyip geri verilir.
    /// </summary>
    public static class EcoConfetti
    {
        const int MaxPooled = 64;

        static readonly Stack<Image> Pool = new Stack<Image>();
        static Transform s_poolRoot;

        class Runner : MonoBehaviour { }

        static Runner _runner;

        static Runner Host
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("[EcoConfetti]") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
                return _runner;
            }
        }

        /// <summary>
        /// Verilen RectTransform'un merkezinden yukari dogru bir konfeti patlamasi acar.
        /// </summary>
        /// <param name="origin">Patlamanin merkezi.</param>
        /// <param name="layer">Parcaciklarin ekleneceği ust katman (genelde DragLayer).</param>
        /// <param name="tint">Ana renk; parcaciklar bunun etrafinda tonlanir.</param>
        /// <param name="count">Parcacik sayisi.</param>
        /// <param name="spread">Yatay dagilma yaricapi (piksel).</param>
        public static void Burst(RectTransform origin, RectTransform layer, Color tint,
            int count = 16, float spread = 140f)
        {
            if (origin == null || layer == null || count <= 0) return;

            // Patlama merkezini hedef katmanin yerel koordinatina cevir.
            Vector2 center = layer.InverseTransformPoint(origin.position);

            for (int i = 0; i < count; i++)
                Host.StartCoroutine(FlyRoutine(layer, center, tint, spread, i));
        }

        static IEnumerator FlyRoutine(RectTransform layer, Vector2 center, Color tint, float spread, int index)
        {
            var image = Rent(layer);
            if (image == null) yield break;

            var rect = (RectTransform)image.transform;

            // Her parcacik biraz farkli: boyut, renk tonu, yon ve omur.
            float size = Random.Range(10f, 20f);
            rect.sizeDelta = new Vector2(size, size * Random.Range(0.55f, 1f));
            rect.anchoredPosition = center;
            rect.localScale = Vector3.one;

            image.color = Color.Lerp(tint, Random.value > 0.5f ? Color.white : Color.black,
                Random.Range(0f, 0.35f));
            image.sprite = Random.value > 0.4f ? UiSpriteFactory.Rounded(4) : UiSpriteFactory.Circle(32);
            image.type = Image.Type.Sliced;

            // Yukari dogru koni: yatayda sacilir, dikeyde firlatilir.
            var velocity = new Vector2(
                Random.Range(-spread, spread),
                Random.Range(spread * 1.4f, spread * 2.6f));

            float gravity = -spread * 5.5f;
            float spin = Random.Range(-420f, 420f);
            float life = Random.Range(0.75f, 1.15f);
            float elapsed = 0f;
            float angle = Random.Range(0f, 360f);

            // Ayni anda patlayan parcaciklar tek blok gibi durmasin diye kucuk gecikme.
            float delay = index * 0.012f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            while (elapsed < life)
            {
                // Sahne degisiminde katman yok edilmis olabilir.
                if (image == null || rect == null) yield break;

                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                velocity.y += gravity * dt;
                rect.anchoredPosition += velocity * dt;

                angle += spin * dt;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);

                // Son ucte solarak kaybol.
                float k = elapsed / life;
                var color = image.color;
                color.a = k < 0.65f ? 1f : Mathf.InverseLerp(1f, 0.65f, k);
                image.color = color;

                yield return null;
            }

            Release(image);
        }

        static Image Rent(RectTransform layer)
        {
            Image image = null;

            // Havuzdaki nesneler sahne degisiminde yok edilmis olabilir; temizleyerek al.
            while (Pool.Count > 0 && image == null) image = Pool.Pop();

            if (image == null)
            {
                var go = new GameObject("Confetti", typeof(RectTransform));
                image = go.AddComponent<Image>();
                image.raycastTarget = false;
            }

            var rect = (RectTransform)image.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsLastSibling();

            image.gameObject.SetActive(true);
            return image;
        }

        static void Release(Image image)
        {
            if (image == null) return;

            if (Pool.Count >= MaxPooled)
            {
                Object.Destroy(image.gameObject);
                return;
            }

            image.gameObject.SetActive(false);
            image.transform.SetParent(PoolRoot, false);
            Pool.Push(image);
        }

        static Transform PoolRoot
        {
            get
            {
                if (s_poolRoot != null) return s_poolRoot;
                var go = new GameObject("[EcoConfetti Pool]") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                go.SetActive(false);
                s_poolRoot = go.transform;
                return s_poolRoot;
            }
        }
    }
}
