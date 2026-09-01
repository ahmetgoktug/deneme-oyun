using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Menu butonlarina uzerine gelince buyume ve ses ekler.
/// Duraklatma menusunde de calismasi icin olceksiz zaman kullanir.
/// </summary>
public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    PongAudio _audio;
    Vector3 _baseScale = Vector3.one;
    float _current = 1f;
    float _target = 1f;

    public void Bind(PongAudio audio)
    {
        _audio = audio;
        _baseScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _target = 1.06f;
        if (_audio != null) _audio.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _target = 1f;
    }

    void OnDisable()
    {
        // Panel kapanip acildiginda buyumus halde kalmasin.
        _target = 1f;
        _current = 1f;
        transform.localScale = _baseScale;
    }

    void Update()
    {
        _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        transform.localScale = _baseScale * _current;
    }
}
