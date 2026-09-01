using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sahnedeki tek giris noktasi. Saha, paletler, top, efektler ve arayuz
/// calisma aninda buradan uretilir; projede hicbir prefab, sprite veya ses dosyasi yoktur.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PongBootstrap : MonoBehaviour
{
    static PhysicsMaterial2D _bounceMaterial;

    /// <summary>Surtunmesiz ve tam sekmeli malzeme: top duvarlarda enerji kaybetmez.</summary>
    static PhysicsMaterial2D BounceMaterial
    {
        get
        {
            if (_bounceMaterial == null)
            {
                _bounceMaterial = new PhysicsMaterial2D("PongBounce");
                _bounceMaterial.friction = 0f;
                _bounceMaterial.bounciness = 1f;
            }
            return _bounceMaterial;
        }
    }

    Transform _root;

    void Awake()
    {
        _root = new GameObject("PongScene").transform;

        var camera = SetupCamera();
        SetupPostProcessing(camera);

        BuildNet();
        BuildWall("WallTop", PongField.HalfHeight + PongField.WallThickness * 0.5f);
        BuildWall("WallBottom", -(PongField.HalfHeight + PongField.WallThickness * 0.5f));

        var ball = BuildBall();
        var player = BuildPaddle<PlayerPaddle>("PlayerPaddle", -PongField.PaddleX, 1, PongField.PlayerColor);
        var ai = BuildPaddle<AiPaddle>("AiPaddle", PongField.PaddleX, -1, PongField.AiColor);

        var audio = new GameObject("PongAudio").AddComponent<PongAudio>();
        audio.transform.SetParent(_root, false);
        audio.Build();

        var juice = new GameObject("Juice").AddComponent<Juice>();
        juice.transform.SetParent(_root, false);
        juice.Build(camera);

        var ui = new GameObject("UiController").AddComponent<UiController>();
        ui.transform.SetParent(_root, false);
        ui.Build(audio);

        var manager = gameObject.AddComponent<GameManager>();
        manager.Bind(ball, player, ai, ui, juice, audio);
    }

    // ---------------------------------------------------------------- kamera

    Camera SetupCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            camera = go.GetComponent<Camera>();
        }

        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = PongField.Background;

        // Sahanin tamami her ekran oraninda gorunsun diye dikey olcuyu
        // gerekirse genislige gore buyutuyoruz.
        float sizeNeededForWidth = (PongField.HalfWidth + 0.6f) / Mathf.Max(camera.aspect, 0.1f);
        camera.orthographicSize = Mathf.Max(PongField.HalfHeight + 0.6f, sizeNeededForWidth);

        return camera;
    }

    /// <summary>Bloom parlak nesnelere neon his veriyor, vignette kenarlari karartiyor.</summary>
    void SetupPostProcessing(Camera camera)
    {
        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData != null) cameraData.renderPostProcessing = true;

        var go = new GameObject("PongVolume");
        go.transform.SetParent(_root, false);

        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        // Esik yuksek, sacilim dar: yalnizca top ve paletler parlasin,
        // tum ekrana yayilan sis olusmasin.
        bloom.threshold.Override(0.88f);
        bloom.intensity.Override(0.85f);
        bloom.scatter.Override(0.55f);
        bloom.tint.Override(new Color(0.85f, 0.92f, 1f));

        var vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        volume.profile = profile;
    }

    // ---------------------------------------------------------------- saha

    void BuildWall(string name, float y)
    {
        var size = new Vector2(PongField.HalfWidth * 2f + 1.6f, PongField.WallThickness);
        var go = PlaceholderArt.CreateSprite(name, PlaceholderArt.Square, size, PongField.WallColor, 1);
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(0f, y, 0f);

        var collider = go.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = BounceMaterial;
    }

    void BuildNet()
    {
        const int dashCount = 15;
        var netRoot = new GameObject("Net").transform;
        netRoot.SetParent(_root, false);

        float spacing = PongField.HalfHeight * 2f / dashCount;
        for (int i = 0; i < dashCount; i++)
        {
            float y = -PongField.HalfHeight + spacing * (i + 0.5f);
            var dash = PlaceholderArt.CreateSprite("Dash", PlaceholderArt.Square,
                new Vector2(0.11f, spacing * 0.55f), PongField.NetColor, 0);
            dash.transform.SetParent(netRoot, false);
            dash.transform.position = new Vector3(0f, y, 0f);
        }
    }

    // ---------------------------------------------------------------- top

    Ball BuildBall()
    {
        float diameter = PongField.BallRadius * 2f;

        // Kok nesnenin olcegi 1'de kalir: collider yaricapi ve iz genisligi
        // ezilme animasyonundan etkilenmesin diye gorsel ayri bir alt nesnede.
        var go = new GameObject("Ball");
        go.transform.SetParent(_root, false);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        // Hizli topun paletin icinden gecmemesi icin surekli carpisma tespiti.
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.sharedMaterial = BounceMaterial;

        var collider = go.AddComponent<CircleCollider2D>();
        collider.radius = PongField.BallRadius;
        collider.sharedMaterial = BounceMaterial;

        var glow = AttachGlow(go.transform, Vector3.one * (diameter * 3.6f), PongField.Neutral, 0.28f, 3);

        var visual = PlaceholderArt.CreateSprite("Sprite", PlaceholderArt.Circle,
            new Vector2(diameter, diameter), PongField.Neutral, 4);
        visual.transform.SetParent(go.transform, false);

        var trail = BuildTrail(go, diameter);

        var ball = go.AddComponent<Ball>();
        ball.AttachVisuals(visual.transform, visual.GetComponent<SpriteRenderer>(), trail, glow);
        return ball;
    }

    static TrailRenderer BuildTrail(GameObject ball, float diameter)
    {
        var trail = ball.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = diameter * 0.85f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.02f;
        trail.numCapVertices = 4;
        trail.autodestruct = false;
        trail.sharedMaterial = PlaceholderArt.SpriteMaterial;
        trail.sortingOrder = 3;
        // Iz topun ic olcegini miras almasin diye dunya uzayinda kalir.
        trail.alignment = LineAlignment.View;
        return trail;
    }

    // ---------------------------------------------------------------- paletler

    T BuildPaddle<T>(string name, float x, int facingSign, Color color) where T : Paddle
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(x, 0f, 0f);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = PongField.PaddleSize;
        collider.sharedMaterial = BounceMaterial;

        var glow = AttachGlow(go.transform, new Vector3(1.35f, 2.9f, 1f), color, 0.34f, 1);

        var visual = PlaceholderArt.CreateSprite("Sprite", PlaceholderArt.Square, PongField.PaddleSize, color, 2);
        visual.transform.SetParent(go.transform, false);

        var paddle = go.AddComponent<T>();
        paddle.Setup(PongField.PaddleSize, facingSign, visual.transform, visual.GetComponent<SpriteRenderer>());
        paddle.AttachGlow(glow);
        paddle.Active = false;
        return paddle;
    }

    /// <summary>Verilen nesnenin arkasina yumusak isik lekesi ekler.</summary>
    static Transform AttachGlow(Transform parent, Vector3 localScale, Color color, float alpha, int sortingOrder)
    {
        var go = PlaceholderArt.CreateSprite("Glow", PlaceholderArt.Glow, Vector2.one,
            new Color(color.r, color.g, color.b, alpha), sortingOrder);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = localScale;
        return go.transform;
    }
}
