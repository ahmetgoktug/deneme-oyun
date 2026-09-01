using System;
using System.Collections;
using EcoSort.Core;
using EcoSort.Data;
using EcoSort.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EcoSort.View
{
    /// <summary>
    /// Uzerine kart birakilabilen her sey (kategori slotu, baska bir kart, tableau yuvasi)
    /// bu arayuzu uygular. CardView sadece bu sozlesmeyi bilir; kimin kabul ettigini bilmez.
    /// </summary>
    public interface ICardDropTarget
    {
        /// <summary>Kart kabul edildiyse true doner. False donerse kart evine geri uctu demektir.</summary>
        bool TryAcceptCard(CardView card);
    }

    /// <summary>
    /// Canvas UI tabanli kart. Surukleme, birakma, kapali/acik yuz ve tum
    /// dokunsal geri bildirimlerden (juice) sorumludur.
    ///
    /// Kart oyun kurallarini BILMEZ: neyin eslestigine CategoryManager karar verir.
    /// Kart yalnizca "birakildim" der ve sonuca gore ya oturur ya evine doner.
    ///
    /// Sahne kurulumu:
    ///   Card (RectTransform + CanvasGroup + Image[raycast target] + CardView)
    ///     +- Face      : Image  (kart on yuzu / cerceve)
    ///     +- Artwork   : Image  (kart gorseli)
    ///     +- Label     : TMP_Text
    ///     +- Back      : Image  (kapali yuz - en ustte)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class CardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        // ---------------------------------------------------------------- referanslar

        [Header("Gorsel Parcalar")]
        [SerializeField] Image _artworkImage;
        [SerializeField] Image _frameImage;
        [SerializeField] TMP_Text _nameLabel;
        [Tooltip("Kapali yuz. Acik kartlarda kapatilir.")]
        [SerializeField] GameObject _backRoot;
        [Tooltip("Opsiyonel golge. Surukleme sirasinda buyur.")]
        [SerializeField] RectTransform _shadow;

        [Header("Surukleme Hissi")]
        [SerializeField, Range(1f, 1.4f)] float _dragScale = 1.07f;
        [SerializeField] float _liftDuration = 0.12f;
        [Tooltip("Yanlis yere birakilinca eve donus suresi.")]
        [SerializeField] float _returnDuration = 0.3f;
        [Tooltip("Dogru yere oturma suresi.")]
        [SerializeField] float _placeDuration = 0.22f;
        [SerializeField] float _flipDuration = 0.26f;
        [Tooltip("Kart govdesinin kategori rengiyle tonlanma miktari. Mobilde okunurluk icin dusuk tut.")]
        [SerializeField, Range(0f, 1f)] float _frameTintStrength = 0.10f;

        [Header("Durum")]
        [SerializeField] bool _faceUp = true;
        [SerializeField] bool _interactable = true;
        [Tooltip("Acikken tek dokunus karti kendi kategorisinin slotuna ucurur (mobil konfor).")]
        [SerializeField] bool _tapToPlay = true;

        // ---------------------------------------------------------------- olaylar

        /// <summary>Kart suruklenmeye baslandi.</summary>
        public event Action<CardView> DragStarted;

        /// <summary>Kart birakildi. bool: bir hedef tarafindan kabul edildi mi?</summary>
        public event Action<CardView, bool> DragEnded;

        /// <summary>Kart tek dokunusla oynanmak istendi (mobil "tap to play").</summary>
        public event Action<CardView> Tapped;

        // ---------------------------------------------------------------- ic durum

        static RectTransform s_dragLayer;

        RectTransform _rect;
        CanvasGroup _canvasGroup;

        CardData _data;

        // Kartin "evi": yanlis birakisda buraya geri doner.
        Transform _homeParent;
        Vector2 _homeAnchoredPos;
        int _homeSiblingIndex;

        Vector3 _baseScale = Vector3.one;
        Vector3 _grabWorldOffset;

        bool _isDragging;
        bool _consumedByTarget;
        bool _pendingRejectShake;

        TweenHandle _moveTween;
        TweenHandle _scaleTween;
        Coroutine _shakeRoutine;

        // ---------------------------------------------------------------- ozellikler

        public CardData Data => _data;
        public CategoryData Category => _data != null ? _data.Category : null;
        public RectTransform Rect => _rect;

        public bool IsFaceUp => _faceUp;
        public bool IsDragging => _isDragging;

        /// <summary>Kart su an suruklenebilir mi? Kapali kartlar ve kilitli kartlar surunmez.</summary>
        public bool CanDrag => _interactable && _faceUp && _data != null;

        /// <summary>
        /// Tum kartlarin surukleme sirasinda tasinacagi ust katman.
        /// Tahta kurulurken bir kez atanir; atanmazsa root Canvas kullanilir.
        /// </summary>
        public static void SetDragLayer(RectTransform layer) => s_dragLayer = layer;

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            _rect = (RectTransform)transform;
            _canvasGroup = GetComponent<CanvasGroup>();
            _baseScale = _rect.localScale;
            CaptureHome();
        }

        void OnDisable()
        {
            // Sahne kapanirken yarim kalan tween'ler yok edilmis nesneye dokunmasin.
            _moveTween?.Kill();
            _scaleTween?.Kill();
            if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        }

        // ---------------------------------------------------------------- kurulum

        /// <summary>Karti bir veriye baglar ve gorselini tazeler.</summary>
        public void Bind(CardData data)
        {
            _data = data;
            Refresh();
        }

        /// <summary>
        /// Gorsel parcalari kod tarafindan baglar. Prefab yerine kartlari calisma
        /// zamaninda ureten kurucular (BoardBuilder / SandboxBoard) icin.
        /// </summary>
        public void ConfigureVisuals(Image frame, Image artwork = null, GameObject back = null,
            TMP_Text label = null, RectTransform shadow = null)
        {
            _frameImage = frame;
            _artworkImage = artwork;
            _backRoot = back;
            _nameLabel = label;
            _shadow = shadow;
            Refresh();
        }

        public void Refresh()
        {
            if (_data == null) return;

            if (_artworkImage != null)
            {
                _artworkImage.sprite = _data.Artwork;
                _artworkImage.color = _data.Tint;
                _artworkImage.enabled = _data.Artwork != null;
            }

            if (_nameLabel != null) _nameLabel.text = _data.DisplayName;

            // Govdeyi kategori rengiyle cok hafif tonla: oyuncu gruplari renkten de okur
            // ama kart beyaz kalir, uzerindeki yazi okunakli olur.
            if (_frameImage != null && _data.Category != null)
                _frameImage.color = Color.Lerp(EcoPalette.CardFace, _data.Category.AccentColor, _frameTintStrength);

            ApplyFaceState();
        }

        /// <summary>Kartin mevcut konumunu yeni "ev" olarak kaydeder.</summary>
        public void CaptureHome()
        {
            _homeParent = _rect.parent;
            _homeAnchoredPos = _rect.anchoredPosition;
            _homeSiblingIndex = _rect.GetSiblingIndex();
        }

        /// <summary>
        /// Karti oynanabilir/oynanamaz yapar. Oynanamaz kart raycast'i da birakir:
        /// boylece bir slota oturmus kartlarin ustune yeni kart birakilabilir.
        /// </summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = value;
        }

        // ---------------------------------------------------------------- yuz cevirme

        /// <summary>Kapali/acik yuz durumunu ayarlar. Animasyonlu cevirmede kart ortadan buzulur.</summary>
        public void SetFaceUp(bool faceUp, bool animate = true)
        {
            if (_faceUp == faceUp)
            {
                ApplyFaceState();
                return;
            }

            if (!animate || !gameObject.activeInHierarchy)
            {
                _faceUp = faceUp;
                ApplyFaceState();
                return;
            }

            _scaleTween?.Kill();
            float half = _flipDuration * 0.5f;
            var flat = new Vector3(0.02f, _baseScale.y, _baseScale.z);

            _scaleTween = EcoTween.Scale(_rect, flat, half, EcoEase.InOutQuad, () =>
            {
                _faceUp = faceUp;
                ApplyFaceState();
                _scaleTween = EcoTween.Scale(_rect, _baseScale, half, EcoEase.OutBack);
            });
        }

        void ApplyFaceState()
        {
            if (_backRoot != null) _backRoot.SetActive(!_faceUp);
        }

        // ---------------------------------------------------------------- surukleme

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag)
            {
                // Surukleme baslamadi: OnDrag/OnEndDrag'de is yapmayalim.
                _isDragging = false;
                return;
            }

            _isDragging = true;
            _consumedByTarget = false;

            CaptureHome();

            var dragLayer = ResolveDragLayer();

            // Parmagin karti "yakaladigi" noktayi koru; kart merkeze zipla-masin.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    dragLayer, eventData.position, eventData.pressEventCamera, out var grabWorld))
                _grabWorldOffset = _rect.position - grabWorld;
            else
                _grabWorldOffset = Vector3.zero;

            _moveTween?.Kill();
            _scaleTween?.Kill();

            // Ust katmana tasi ve en one al.
            _rect.SetParent(dragLayer, true);
            _rect.SetAsLastSibling();

            // Altindaki drop hedefleri raycast alabilsin diye kart raycast'i kapatilir.
            _canvasGroup.blocksRaycasts = false;

            _scaleTween = EcoTween.Scale(_rect, _baseScale * _dragScale, _liftDuration, EcoEase.OutQuad);
            if (_shadow != null) _shadow.localScale = Vector3.one * 1.15f;

            DragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            var dragLayer = ResolveDragLayer();
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    dragLayer, eventData.position, eventData.pressEventCamera, out var world))
                _rect.position = world + _grabWorldOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            _isDragging = false;
            // Kart bir slota girdiyse SetInteractable(false) cagrilmistir; raycast'i geri acmayalim.
            _canvasGroup.blocksRaycasts = _interactable;
            if (_shadow != null) _shadow.localScale = Vector3.one;

            // Not: uGUI'de hedefin OnDrop'u OnEndDrag'den ONCE calisir.
            // Yani buraya geldigimizde _consumedByTarget zaten dogru degeri tasir.
            bool accepted = _consumedByTarget;
            if (!accepted) ReturnHome();

            DragEnded?.Invoke(this, accepted);
        }

        /// <summary>Kart baska bir kartin uzerine birakildi.</summary>
        public void OnDrop(PointerEventData eventData)
        {
            var dragged = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardView>() : null;
            if (dragged == null || dragged == this) return;

            // Karari biz vermiyoruz. Kartta ozel bir hedef davranisi varsa (ICardDropTarget)
            // o karar verir; yoksa kural motoru iki kartin tematik bagina bakar.
            var custom = GetComponent<ICardDropTarget>();
            bool accepted = custom != null
                ? custom.TryAcceptCard(dragged)
                : CategoryManager.Instance != null && CategoryManager.Instance.TryMatchCards(dragged, this);

            if (accepted) dragged.MarkConsumed();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isDragging || !CanDrag) return;
            if (eventData.dragging) return;

            Tapped?.Invoke(this);

            if (_tapToPlay && CategoryManager.Instance != null)
                CategoryManager.Instance.TryAutoPlace(this);
        }

        RectTransform ResolveDragLayer()
        {
            if (s_dragLayer != null) return s_dragLayer;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) s_dragLayer = (RectTransform)canvas.rootCanvas.transform;

            return s_dragLayer != null ? s_dragLayer : (RectTransform)_rect.parent;
        }

        // ---------------------------------------------------------------- yerlesme / geri donus

        /// <summary>Bir hedef karti kabul ettiginde cagirir; kart artik eve donmez.</summary>
        public void MarkConsumed() => _consumedByTarget = true;

        /// <summary>Karti kaydedilmis evine yumusak bir yayla geri gonderir.</summary>
        public void ReturnHome(Action onComplete = null)
        {
            _moveTween?.Kill();
            _scaleTween?.Kill();

            if (_homeParent != null && _rect.parent != _homeParent)
                _rect.SetParent(_homeParent, true);   // dunya konumu korunur, tween'i oradan aliriz

            _scaleTween = EcoTween.Scale(_rect, _baseScale, _returnDuration * 0.6f, EcoEase.OutQuad);
            _moveTween = EcoTween.MoveAnchored(_rect, _homeAnchoredPos, _returnDuration, EcoEase.OutBack, () =>
            {
                _rect.SetSiblingIndex(_homeSiblingIndex);

                if (_pendingRejectShake)
                {
                    _pendingRejectShake = false;
                    PlayRejected();
                }

                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Karti yeni bir ebeveyne oturtur (kategori slotu gibi) ve orayi yeni evi yapar.
        /// </summary>
        public void PlaceInto(Transform parent, Vector2 anchoredPosition, Action onComplete = null)
        {
            _moveTween?.Kill();
            _scaleTween?.Kill();

            _rect.SetParent(parent, true);
            _rect.SetAsLastSibling();

            _scaleTween = EcoTween.Scale(_rect, _baseScale, _placeDuration, EcoEase.OutQuad);
            _moveTween = EcoTween.MoveAnchored(_rect, anchoredPosition, _placeDuration, EcoEase.OutBack, () =>
            {
                CaptureHome();
                onComplete?.Invoke();
            });
        }

        // ---------------------------------------------------------------- geri bildirim

        /// <summary>Dogru eslesme: kisa bir pop.</summary>
        public void PlayAccepted()
        {
            EcoTween.Punch(_rect, 0.14f, 0.3f);
        }

        /// <summary>
        /// Yanlis eslesme: kucuk bir "hayir" sallanmasi.
        /// Kart hala parmagin ucundaysa sallanmayi eve donduktan sonraya erteleriz,
        /// aksi halde iki animasyon ayni anda konumu yazar.
        /// </summary>
        public void PlayRejected()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_isDragging)
            {
                _pendingRejectShake = true;
                return;
            }

            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(10f, 0.25f));
        }

        IEnumerator ShakeRoutine(float strength, float duration)
        {
            // Ev konumu etrafinda sonumlenen yatay salinim.
            Vector2 origin = _homeAnchoredPos;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damping = 1f - Mathf.Clamp01(elapsed / duration);
                float offset = Mathf.Sin(elapsed * 55f) * strength * damping;
                _rect.anchoredPosition = origin + new Vector2(offset, 0f);
                yield return null;
            }

            _rect.anchoredPosition = origin;
            _shakeRoutine = null;
        }

        /// <summary>
        /// Grup tamamlandiginda kartin panodan silinme animasyonu:
        /// hafifce buyur, sonra kucuLup soner. Bitince onComplete cagrilir.
        /// </summary>
        public void PlayClearAndDespawn(float delay, Action onComplete = null)
        {
            StartCoroutine(ClearRoutine(delay, onComplete));
        }

        IEnumerator ClearRoutine(float delay, Action onComplete)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            _interactable = false;
            _canvasGroup.blocksRaycasts = false;

            EcoTween.Scale(_rect, _baseScale * 1.18f, 0.12f, EcoEase.OutQuad);
            yield return new WaitForSecondsRealtime(0.12f);

            EcoTween.Scale(_rect, _baseScale * 0.2f, 0.22f, EcoEase.OutQuad);
            EcoTween.Fade(_canvasGroup, 0f, 0.22f);
            yield return new WaitForSecondsRealtime(0.24f);

            onComplete?.Invoke();
        }
    }
}
