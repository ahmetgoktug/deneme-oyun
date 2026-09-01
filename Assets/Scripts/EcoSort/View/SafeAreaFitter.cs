using UnityEngine;

namespace EcoSort.View
{
    /// <summary>
    /// Bagli oldugu RectTransform'u cihazin guvenli alanina oturtur:
    /// centik, delik kamera ve alttaki gesture cubugu iceriklerin uzerine binmez.
    /// Ekran donunce veya guvenli alan degisince kendini gunceller.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;

        void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            // Ekran boyutu/oryantasyon degisimini yakalamanin ucuz yolu.
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x ||
                Screen.height != _lastScreenSize.y)
                Apply();
        }

        void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var safeArea = Screen.safeArea;
            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
