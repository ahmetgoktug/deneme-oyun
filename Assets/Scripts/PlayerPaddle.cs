using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Klavye (W/S veya yon tuslari) ile kontrol edilen palet.
/// Fare hareket ettirilirse otomatik olarak fare kontroluna gecer.
/// </summary>
public class PlayerPaddle : Paddle
{
    bool _useMouse;
    Camera _camera;

    Camera Cam
    {
        get
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }
    }

    protected override float ComputeTargetY(float deltaTime)
    {
        float axis = ReadKeyboardAxis();
        if (!Mathf.Approximately(axis, 0f))
        {
            _useMouse = false;
            // Uzak bir hedef veriyoruz; asil sinirlamayi Paddle'daki hiz limiti yapiyor.
            return Body.position.y + axis * 100f;
        }

        var mouse = Mouse.current;
        if (mouse == null) return Body.position.y;

        if (mouse.delta.ReadValue().sqrMagnitude > 1f) _useMouse = true;
        if (!_useMouse || Cam == null) return Body.position.y;

        Vector2 screenPoint = mouse.position.ReadValue();
        var world = Cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -Cam.transform.position.z));
        return world.y;
    }

    static float ReadKeyboardAxis()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return 0f;

        float axis = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) axis += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) axis -= 1f;
        return axis;
    }
}
