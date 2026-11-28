using UnityEngine;
using UnityEngine.UIElements;

// A row of 3 stars, drawn with UI Toolkit's vector painter since the fonts have no star character.
public static class Stars
{
    static readonly Color Lit = new(0.95f, 0.78f, 0.36f), Dim = new(1f, 1f, 1f, 0.15f);

    public static void Fill(VisualElement row, int count, float size, int of = 3)
    {
        row.Clear();
        for (int i = 0; i < of; i++)
        {
            var star = new VisualElement { pickingMode = PickingMode.Ignore };
            star.AddToClassList("star");
            star.style.width = size;
            star.style.height = size;
            star.style.marginLeft = star.style.marginRight = size * 0.1f;
            var color = i < count ? Lit : Dim;
            star.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                float r = size / 2f;
                p.fillColor = color;
                p.BeginPath();
                for (int k = 0; k < 10; k++)
                {
                    float a = -Mathf.PI / 2f + k * Mathf.PI / 5f, len = k % 2 == 0 ? r : r * 0.45f;
                    var pt = new Vector2(r + Mathf.Cos(a) * len, r + Mathf.Sin(a) * len);
                    if (k == 0) p.MoveTo(pt); else p.LineTo(pt);
                }
                p.ClosePath();
                p.Fill();
            };
            row.Add(star);
        }
    }
}
