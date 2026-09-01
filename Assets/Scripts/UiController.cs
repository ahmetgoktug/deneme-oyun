using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Tum arayuzu koddan kurar: ana menu, oyun ici skor tablosu, ralli sayaci,
/// duraklatma ekrani, oyun sonu ekrani ve tam ekran flas efekti.
/// </summary>
public class UiController : MonoBehaviour
{
    public event Action<Difficulty> DifficultyChosen;
    public event Action ResumeRequested;
    public event Action PlayAgainRequested;
    public event Action MenuRequested;

    const float PunchDuration = 0.32f;
    const float FlashDuration = 0.45f;

    PongAudio _audio;

    GameObject _menuPanel;
    GameObject _pausePanel;
    GameObject _gameOverPanel;
    GameObject _hudPanel;

    Text _playerScoreText;
    Text _aiScoreText;
    Text _difficultyText;
    Text _centerText;
    Text _rallyText;
    Text _gameOverTitle;
    Text _gameOverDetail;
    Text _menuTitle;
    Image _flashOverlay;

    float _playerPunch;
    float _aiPunch;
    float _centerPunch;
    float _rallyPunch;
    float _flashTimer;
    float _flashStrength;
    Color _flashColor;

    int _lastPlayerScore;
    int _lastAiScore;

    public void Build(PongAudio audio)
    {
        _audio = audio;
        EnsureEventSystem();

        var canvas = CreateCanvas();

        // Olusturma sirasi cizim sirasidir: HUD en altta, flas en ustte.
        _hudPanel = BuildHudPanel(canvas);
        _menuPanel = BuildMenuPanel(canvas);
        _pausePanel = BuildPausePanel(canvas);
        _gameOverPanel = BuildGameOverPanel(canvas);
        _flashOverlay = BuildFlashOverlay(canvas);
    }

    // ---------------------------------------------------------------- durumlar

    public void ShowMenu()
    {
        _menuPanel.SetActive(true);
        _hudPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _gameOverPanel.SetActive(false);
    }

    public void ShowGame(DifficultySettings settings)
    {
        _menuPanel.SetActive(false);
        _hudPanel.SetActive(true);
        _pausePanel.SetActive(false);
        _gameOverPanel.SetActive(false);
        _difficultyText.text = settings.DisplayName + "  -  FIRST TO " + settings.ScoreToWin;

        _lastPlayerScore = 0;
        _lastAiScore = 0;
        SetRally(0);
    }

    public void SetPauseVisible(bool visible)
    {
        _pausePanel.SetActive(visible);
    }

    public void ShowGameOver(bool playerWon, int playerScore, int aiScore)
    {
        _gameOverPanel.SetActive(true);
        _gameOverTitle.text = playerWon ? "YOU WIN" : "YOU LOSE";
        _gameOverTitle.color = playerWon ? PongField.PlayerColor : PongField.AiColor;
        _gameOverDetail.text = playerScore + "  -  " + aiScore;
    }

    public void UpdateScore(int playerScore, int aiScore)
    {
        _playerScoreText.text = playerScore.ToString();
        _aiScoreText.text = aiScore.ToString();

        // Yalnizca degisen tarafi zipplat.
        if (playerScore != _lastPlayerScore) _playerPunch = PunchDuration;
        if (aiScore != _lastAiScore) _aiPunch = PunchDuration;

        _lastPlayerScore = playerScore;
        _lastAiScore = aiScore;
    }

    public void SetCenterMessage(string message)
    {
        string next = message ?? string.Empty;
        if (next != _centerText.text && next.Length > 0) _centerPunch = PunchDuration;
        _centerText.text = next;
    }

    /// <summary>Ralli uzadikca ortada sayac gosterir; kisa rallilerde gizli kalir.</summary>
    public void SetRally(int count)
    {
        if (count < 3)
        {
            _rallyText.text = string.Empty;
            return;
        }

        if (_rallyText.text.Length == 0 || _rallyText.text != "RALLY " + count) _rallyPunch = 0.22f;
        _rallyText.text = "RALLY " + count;

        // Ralli uzadikca renk isinir.
        _rallyText.color = Color.Lerp(new Color(0.878f, 0.925f, 1f, 0.5f), PongField.BallHot,
            Mathf.Clamp01((count - 3) / 12f));
    }

    /// <summary>Tum ekrani kisa sureligine verilen renge boyar.</summary>
    public void FlashScreen(Color color, float strength)
    {
        _flashColor = color;
        _flashStrength = strength;
        _flashTimer = FlashDuration;
    }

    // ---------------------------------------------------------------- animasyon

    void Update()
    {
        // Duraklatmada ve hit stop sirasinda da akmasi icin olceksiz zaman.
        float dt = Time.unscaledDeltaTime;

        _playerPunch = Tick(_playerPunch, dt);
        _aiPunch = Tick(_aiPunch, dt);
        _centerPunch = Tick(_centerPunch, dt);
        _rallyPunch = Tick(_rallyPunch, dt);

        ApplyPunch(_playerScoreText, _playerPunch, PunchDuration, 0.55f);
        ApplyPunch(_aiScoreText, _aiPunch, PunchDuration, 0.55f);
        ApplyPunch(_centerText, _centerPunch, PunchDuration, 0.7f);
        ApplyPunch(_rallyText, _rallyPunch, 0.22f, 0.35f);

        UpdateFlash(dt);
        UpdateMenuTitle();
    }

    static float Tick(float timer, float dt)
    {
        return timer <= 0f ? 0f : Mathf.Max(0f, timer - dt);
    }

    /// <summary>Buyuyup normale donen kisa vurgu.</summary>
    static void ApplyPunch(Text target, float timer, float duration, float amount)
    {
        if (target == null) return;

        float t = duration <= 0f ? 0f : timer / duration;
        // Basta sert, sonra yumusak sonen bir egri.
        float scale = 1f + amount * t * t;
        target.rectTransform.localScale = Vector3.one * scale;
    }

    void UpdateFlash(float dt)
    {
        if (_flashTimer <= 0f)
        {
            if (_flashOverlay.color.a > 0f) _flashOverlay.color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        _flashTimer = Mathf.Max(0f, _flashTimer - dt);
        float t = _flashTimer / FlashDuration;
        _flashOverlay.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, t * t * _flashStrength);
    }

    void UpdateMenuTitle()
    {
        if (_menuTitle == null || !_menuPanel.activeSelf) return;

        // Baslikta yavas bir nefes alip verme.
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.025f;
        _menuTitle.rectTransform.localScale = Vector3.one * pulse;
    }

    // ---------------------------------------------------------------- kurulum

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
        // Proje sadece yeni Input System kullaniyor, bu yuzden eski
        // StandaloneInputModule yerine bu modulu ekliyoruz.
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    static Transform CreateCanvas()
    {
        var go = new GameObject("PongCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    Image BuildFlashOverlay(Transform canvas)
    {
        var rect = CreateRect("FlashOverlay", canvas);
        Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.Square;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        return image;
    }

    GameObject BuildMenuPanel(Transform canvas)
    {
        var panel = CreatePanel("MenuPanel", canvas, new Color(0.043f, 0.058f, 0.098f, 0.92f));

        _menuTitle = CreateText("Title", panel, "PONG", 170, TextAnchor.MiddleCenter, PongField.Neutral);
        Place(_menuTitle.rectTransform, new Vector2(0f, 290f), new Vector2(900f, 220f));

        var subtitle = CreateText("Subtitle", panel, "SELECT DIFFICULTY", 36, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.55f));
        Place(subtitle.rectTransform, new Vector2(0f, 165f), new Vector2(900f, 50f));

        CreateMenuButton(panel, "EASY", PongField.PlayerColor, 40f, Difficulty.Easy);
        CreateMenuButton(panel, "MEDIUM", PongField.Neutral, -70f, Difficulty.Medium);
        CreateMenuButton(panel, "HARD", PongField.AiColor, -180f, Difficulty.Hard);

        var hint = CreateText("Hint", panel,
            "Move: W / S  or  Up / Down  or  Mouse        Pause: ESC        Quick pick: 1 / 2 / 3",
            26, TextAnchor.MiddleCenter, new Color(0.878f, 0.925f, 1f, 0.4f));
        Place(hint.rectTransform, new Vector2(0f, -330f), new Vector2(1400f, 40f));

        return panel.gameObject;
    }

    void CreateMenuButton(RectTransform parent, string label, Color accent, float y, Difficulty difficulty)
    {
        CreateButton(parent, label, accent, new Vector2(0f, y), new Vector2(460f, 92f),
            () => DifficultyChosen?.Invoke(difficulty));
    }

    GameObject BuildHudPanel(Transform canvas)
    {
        var panel = CreateRect("HudPanel", canvas);
        Stretch(panel);

        _playerScoreText = CreateText("PlayerScore", panel, "0", 120, TextAnchor.MiddleCenter, PongField.PlayerColor);
        Place(_playerScoreText.rectTransform, new Vector2(-220f, 380f), new Vector2(260f, 150f));

        _aiScoreText = CreateText("AiScore", panel, "0", 120, TextAnchor.MiddleCenter, PongField.AiColor);
        Place(_aiScoreText.rectTransform, new Vector2(220f, 380f), new Vector2(260f, 150f));

        _difficultyText = CreateText("DifficultyLabel", panel, string.Empty, 28, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.45f));
        Place(_difficultyText.rectTransform, new Vector2(0f, 470f), new Vector2(900f, 40f));

        _centerText = CreateText("CenterMessage", panel, string.Empty, 110, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.85f));
        Place(_centerText.rectTransform, new Vector2(0f, 0f), new Vector2(1200f, 200f));

        _rallyText = CreateText("Rally", panel, string.Empty, 34, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.5f));
        Place(_rallyText.rectTransform, new Vector2(0f, -390f), new Vector2(700f, 50f));

        var hint = CreateText("HudHint", panel, "ESC  -  pause", 24, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.3f));
        Place(hint.rectTransform, new Vector2(0f, -480f), new Vector2(600f, 40f));

        return panel.gameObject;
    }

    GameObject BuildPausePanel(Transform canvas)
    {
        var panel = CreatePanel("PausePanel", canvas, new Color(0.043f, 0.058f, 0.098f, 0.85f));

        var title = CreateText("Title", panel, "PAUSED", 110, TextAnchor.MiddleCenter, PongField.Neutral);
        Place(title.rectTransform, new Vector2(0f, 150f), new Vector2(900f, 160f));

        CreateButton(panel, "RESUME", PongField.PlayerColor, new Vector2(0f, 0f), new Vector2(460f, 92f),
            () => ResumeRequested?.Invoke());
        CreateButton(panel, "MAIN MENU", PongField.Neutral, new Vector2(0f, -110f), new Vector2(460f, 92f),
            () => MenuRequested?.Invoke());

        return panel.gameObject;
    }

    GameObject BuildGameOverPanel(Transform canvas)
    {
        var panel = CreatePanel("GameOverPanel", canvas, new Color(0.043f, 0.058f, 0.098f, 0.88f));

        _gameOverTitle = CreateText("Title", panel, "YOU WIN", 120, TextAnchor.MiddleCenter, PongField.Neutral);
        Place(_gameOverTitle.rectTransform, new Vector2(0f, 200f), new Vector2(1000f, 170f));

        _gameOverDetail = CreateText("Detail", panel, "0  -  0", 48, TextAnchor.MiddleCenter,
            new Color(0.878f, 0.925f, 1f, 0.6f));
        Place(_gameOverDetail.rectTransform, new Vector2(0f, 90f), new Vector2(600f, 70f));

        CreateButton(panel, "PLAY AGAIN", PongField.PlayerColor, new Vector2(0f, -40f), new Vector2(460f, 92f),
            () => PlayAgainRequested?.Invoke());
        CreateButton(panel, "MAIN MENU", PongField.Neutral, new Vector2(0f, -150f), new Vector2(460f, 92f),
            () => MenuRequested?.Invoke());

        return panel.gameObject;
    }

    // ---------------------------------------------------------------- yardimcilar

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform CreatePanel(string name, Transform parent, Color background)
    {
        var rect = CreateRect(name, parent);
        Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.Square;
        image.color = background;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Ekran merkezine gore konumlandirir; olcegi CanvasScaler halleder.</summary>
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor, Color color)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = PlaceholderArt.Font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.text = content;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    Button CreateButton(Transform parent, string label, Color accent, Vector2 position, Vector2 size,
        UnityAction onClick)
    {
        var rect = CreateRect("Button_" + label, parent);
        Place(rect, position, size);

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.Square;
        image.color = Color.white;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = new Color(accent.r, accent.g, accent.b, 0.14f);
        colors.highlightedColor = new Color(accent.r, accent.g, accent.b, 0.32f);
        colors.pressedColor = new Color(accent.r, accent.g, accent.b, 0.55f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.AddListener(onClick);
        button.onClick.AddListener(() => { if (_audio != null) _audio.PlayUiClick(); });

        var text = CreateText("Label", rect, label, 42, TextAnchor.MiddleCenter, accent);
        Stretch(text.rectTransform);

        var effects = rect.gameObject.AddComponent<MenuButtonEffects>();
        effects.Bind(_audio);

        return button;
    }
}
