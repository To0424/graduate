using UnityEngine;

/// <summary>
/// Generates a simple white square sprite at runtime.
/// All runtime-created visuals use this since there are no sprite assets yet.
/// </summary>
public static class RuntimeSprite
{
    private static Sprite _whiteSquare;

    /// <summary>
    /// Returns a 4x4 white square sprite. Created once, reused forever.
    /// </summary>
    public static Sprite WhiteSquare
    {
        get
        {
            if (_whiteSquare == null)
            {
                Texture2D tex = new Texture2D(4, 4);
                Color[] pixels = new Color[16];
                for (int i = 0; i < 16; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                _whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return _whiteSquare;
        }
    }

    private static Sprite _circle;

    /// <summary>
    /// Returns a simple circle sprite (16x16 with soft edges).
    /// </summary>
    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
            {
                int size = 16;
                Texture2D tex = new Texture2D(size, size);
                float center = size / 2f;
                float radius = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        float alpha = dist < radius - 1 ? 1f : (dist < radius ? radius - dist : 0f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                }
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return _circle;
        }
    }
}
