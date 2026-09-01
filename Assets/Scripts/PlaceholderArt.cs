using UnityEngine;

/// <summary>
/// Placeholder gorseller. Projede hicbir sprite dosyasi olmadigi icin
/// kare ve daire texture'lari calisma aninda uretilir.
/// </summary>
public static class PlaceholderArt
{
    static Sprite _square;
    static Sprite _circle;
    static Sprite _glow;
    static Material _material;
    static Material _particleMaterial;
    static Font _font;

    public static Sprite Square
    {
        get
        {
            if (_square == null) _square = BuildSquare();
            return _square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (_circle == null) _circle = BuildCircle();
            return _circle;
        }
    }

    /// <summary>Merkezden disa sonen yumusak isik lekesi; top ve paletlerin arkasinda kullanilir.</summary>
    public static Sprite Glow
    {
        get
        {
            if (_glow == null) _glow = BuildGlow();
            return _glow;
        }
    }

    /// <summary>Parcaciklar icin ayri materyal: ayni shader, yuvarlak texture.</summary>
    public static Material ParticleMaterial
    {
        get
        {
            if (_particleMaterial == null)
            {
                _particleMaterial = new Material(SpriteMaterial.shader);
                _particleMaterial.name = "PongParticle";
                _particleMaterial.mainTexture = Circle.texture;
            }
            return _particleMaterial;
        }
    }

    /// <summary>Tum sprite'lar bu tek materyali paylasir.</summary>
    public static Material SpriteMaterial
    {
        get
        {
            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");

                _material = new Material(shader);
                _material.name = "PongPlaceholder";
            }
            return _material;
        }
    }

    /// <summary>Unity ile gelen dahili font; ayrica bir font asset'i gerektirmez.</summary>
    public static Font Font
    {
        get
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Dahili font bulunamazsa isletim sisteminden bir font uret,
            // aksi halde tum arayuz yazilari bos gorunur.
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return _font;
        }
    }

    static Sprite BuildSquare()
    {
        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PongSquare";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply();

        // pixelsPerUnit = size oldugu icin sprite tam 1x1 birim olur,
        // yani localScale dogrudan dunya olcusu anlamina gelir.
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
            0, SpriteMeshType.FullRect);
    }

    static Sprite BuildCircle()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PongCircle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                // Kenarda 1 piksellik yumusak gecis: kenarlar tirtikli gorunmesin.
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
            0, SpriteMeshType.FullRect);
    }

    static Sprite BuildGlow()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "PongGlow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Ikinci dereceden sonum: merkezde parlak, kenarda tamamen seffaf.
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
            0, SpriteMeshType.FullRect);
    }

    /// <summary>Verilen olcu ve renkte bir sprite nesnesi olusturur.</summary>
    public static GameObject CreateSprite(string name, Sprite sprite, Vector2 size, Color color, int sortingOrder = 0)
    {
        var go = new GameObject(name);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sharedMaterial = SpriteMaterial;
        renderer.sortingOrder = sortingOrder;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }
}
