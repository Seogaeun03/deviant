using UnityEngine;

namespace Deviant.Presentation.Unity
{
    // Shared screen-space contract for the native world, modal UI and pointer mapping.
    public static class PixelGameViewport
    {
        public const int Width = 960, Height = 540;

        public static Rect DisplayRect(int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rect();
            float fit = Mathf.Min(width / (float)Width, height / (float)Height);
            float scale = fit >= 1 ? Mathf.Floor(fit) : fit;
            float w = Mathf.Round(Width * scale), h = Mathf.Round(Height * scale);
            return new Rect(Mathf.Floor((width - w) * .5f), Mathf.Floor((height - h) * .5f), w, h);
        }

        public static Rect UiRect(int width, int height)
        {
            var frame = DisplayRect(width, height);
            // Rounded sub-native frames can differ slightly from 16:9. Keep every UI edge inside them.
            float scale = Mathf.Min(frame.width / Width, frame.height / Height);
            var size = new Vector2(Width, Height) * scale;
            return new Rect(frame.center - size * .5f, size);
        }

        // The root is a centred 960x540 RectTransform; the canvas can retain its HUD scale.
        public static void FitUi(RectTransform root, float canvasScale, int width, int height)
        {
            if (!root || canvasScale <= 0) return;
            var frame = UiRect(width, height);
            root.localScale = Vector3.one * (frame.width / Width / canvasScale);
            root.anchoredPosition = new Vector2(frame.center.x - width * .5f, height * .5f - frame.center.y) / canvasScale;
        }
    }
}
