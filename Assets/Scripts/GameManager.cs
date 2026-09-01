using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Menu,
    Countdown,
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// Oyun akisini yonetir: menu, servis geri sayimi, raunt, duraklatma ve oyun sonu.
/// Ses ve efektleri de buradan tetikliyoruz.
/// </summary>
public class GameManager : MonoBehaviour
{
    const float StartCountdown = 3f;
    const float PointCountdown = 1.6f;

    Ball _ball;
    PlayerPaddle _player;
    AiPaddle _ai;
    UiController _ui;
    Juice _juice;
    PongAudio _audio;

    DifficultySettings _settings;
    GameState _state = GameState.Menu;
    GameState _stateBeforePause = GameState.Playing;

    int _playerScore;
    int _aiScore;
    float _countdown;
    int _lastTick = -1;
    int _serveDirection = 1;

    public void Bind(Ball ball, PlayerPaddle player, AiPaddle ai, UiController ui, Juice juice, PongAudio audio)
    {
        _ball = ball;
        _player = player;
        _ai = ai;
        _ui = ui;
        _juice = juice;
        _audio = audio;

        _ui.DifficultyChosen += StartGame;
        _ui.ResumeRequested += Resume;
        _ui.PlayAgainRequested += PlayAgain;
        _ui.MenuRequested += GoToMenu;

        _ball.PaddleHit += OnPaddleHit;
        _ball.WallHit += OnWallHit;

        GoToMenu();
    }

    void OnDestroy()
    {
        if (_ui != null)
        {
            _ui.DifficultyChosen -= StartGame;
            _ui.ResumeRequested -= Resume;
            _ui.PlayAgainRequested -= PlayAgain;
            _ui.MenuRequested -= GoToMenu;
        }

        if (_ball != null)
        {
            _ball.PaddleHit -= OnPaddleHit;
            _ball.WallHit -= OnWallHit;
        }
    }

    void Update()
    {
        HandleHotkeys();

        if (_state == GameState.Countdown) TickCountdown();
        else if (_state == GameState.Playing) CheckForGoal();
    }

    // ---------------------------------------------------------------- carpisma tepkileri

    void OnPaddleHit(Paddle paddle, Vector2 point, float speedRatio)
    {
        paddle.PlayHitFeedback();
        _audio.PlayPaddleHit(speedRatio);

        // Sert vuruslarda daha uzun donma ve daha genis sarsinti.
        _juice.HitStop(0.025f + 0.035f * speedRatio);
        _juice.Shake(0.06f + 0.13f * speedRatio, 0.13f);

        var color = paddle is PlayerPaddle ? PongField.PlayerColor : PongField.AiColor;
        _juice.Burst(point, new Vector2(paddle.FacingSign, 0f), color,
            10 + Mathf.RoundToInt(12f * speedRatio), 5f + 5f * speedRatio, 42f);

        _ui.SetRally(_ball.RallyCount);
    }

    void OnWallHit(Vector2 point, float speedRatio)
    {
        _audio.PlayWallHit();
        _juice.Shake(0.03f + 0.05f * speedRatio, 0.09f);

        var normal = point.y > 0f ? Vector2.down : Vector2.up;
        _juice.Burst(point, normal, PongField.Neutral,
            5 + Mathf.RoundToInt(6f * speedRatio), 3.5f + 2f * speedRatio, 55f);
    }

    // ---------------------------------------------------------------- akis

    void GoToMenu()
    {
        _juice.CancelHitStop();
        Time.timeScale = 1f;
        _state = GameState.Menu;

        _ball.Park();
        _player.Active = false;
        _ai.Active = false;
        _player.ResetToCenter();
        _ai.ResetToCenter();

        _ui.SetCenterMessage(string.Empty);
        _ui.SetRally(0);
        _ui.ShowMenu();
    }

    void StartGame(Difficulty difficulty)
    {
        _juice.CancelHitStop();
        Time.timeScale = 1f;
        _settings = DifficultySettings.For(difficulty);

        _playerScore = 0;
        _aiScore = 0;

        _player.Speed = DifficultySettings.PlayerSpeed;
        _ai.Bind(_ball, _settings);
        _ball.Configure(_settings);

        _player.Active = true;
        _ai.Active = true;

        _ui.ShowGame(_settings);
        _ui.UpdateScore(0, 0);

        _serveDirection = Random.value < 0.5f ? -1 : 1;
        BeginCountdown(StartCountdown);
    }

    void PlayAgain()
    {
        StartGame(_settings != null ? _settings.Level : Difficulty.Medium);
    }

    void BeginCountdown(float duration)
    {
        _state = GameState.Countdown;
        _countdown = duration;
        _lastTick = -1;

        _ball.Park();
        _player.ResetToCenter();
        _ai.ResetToCenter();
        _ui.SetRally(0);
    }

    void TickCountdown()
    {
        _countdown -= Time.deltaTime;

        if (_countdown > 0f)
        {
            int remaining = Mathf.CeilToInt(_countdown);
            if (remaining != _lastTick)
            {
                _lastTick = remaining;
                _ui.SetCenterMessage(remaining.ToString());
                _audio.PlayCountdownTick(remaining);
            }
            return;
        }

        _ui.SetCenterMessage(string.Empty);
        _audio.PlayLaunch();
        _ball.Launch(_settings, _serveDirection);
        _state = GameState.Playing;
    }

    void CheckForGoal()
    {
        float x = _ball.Position.x;
        if (Mathf.Abs(x) < PongField.GoalLine) return;

        // Top sag cizgiyi gectiyse bilgisayarin arkasindan cikmistir: oyuncu sayi alir.
        bool playerScored = x > 0f;
        if (playerScored) _playerScore++;
        else _aiScore++;

        var color = playerScored ? PongField.PlayerColor : PongField.AiColor;
        _audio.PlayScore(playerScored);
        _juice.HitStop(0.09f);
        _juice.Shake(0.32f, 0.4f);
        _juice.BurstRing(_ball.Position, color, 28, 7.5f);
        _ui.FlashScreen(color, 0.32f);
        _ui.UpdateScore(_playerScore, _aiScore);

        // Servis, sayiyi yiyen tarafa dogru atilir.
        _serveDirection = playerScored ? 1 : -1;

        if (_playerScore >= _settings.ScoreToWin || _aiScore >= _settings.ScoreToWin) EndGame(playerScored);
        else BeginCountdown(PointCountdown);
    }

    void EndGame(bool playerWon)
    {
        _state = GameState.GameOver;
        _juice.CancelHitStop();
        Time.timeScale = 1f;

        _ball.Park();
        _player.Active = false;
        _ai.Active = false;

        if (playerWon) _audio.PlayWin();
        else _audio.PlayLose();

        _ui.SetCenterMessage(string.Empty);
        _ui.SetRally(0);
        _ui.ShowGameOver(playerWon, _playerScore, _aiScore);
    }

    void Pause()
    {
        // Hit stop devam ederse timeScale'i kendi basina 1'e cekip oyunu surdurur.
        _juice.CancelHitStop();

        _stateBeforePause = _state;
        _state = GameState.Paused;
        Time.timeScale = 0f;
        _ui.SetPauseVisible(true);
    }

    void Resume()
    {
        if (_state != GameState.Paused) return;
        _state = _stateBeforePause;
        Time.timeScale = 1f;
        _ui.SetPauseVisible(false);
    }

    // ---------------------------------------------------------------- klavye

    void HandleHotkeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        switch (_state)
        {
            case GameState.Menu:
                if (keyboard.digit1Key.wasPressedThisFrame) StartGame(Difficulty.Easy);
                else if (keyboard.digit2Key.wasPressedThisFrame) StartGame(Difficulty.Medium);
                else if (keyboard.digit3Key.wasPressedThisFrame) StartGame(Difficulty.Hard);
                break;

            case GameState.Countdown:
            case GameState.Playing:
                if (keyboard.escapeKey.wasPressedThisFrame) Pause();
                break;

            case GameState.Paused:
                if (keyboard.escapeKey.wasPressedThisFrame) Resume();
                break;

            case GameState.GameOver:
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) PlayAgain();
                else if (keyboard.escapeKey.wasPressedThisFrame) GoToMenu();
                break;
        }
    }
}
