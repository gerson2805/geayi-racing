// UILabel.cs — ayudante: crea una etiqueta de texto (Unity UI) legible
// con la fuente por defecto del proyecto. Las letras se ven con el
// material por defecto de Unity; si la fuente falta, usa Arial.
using UnityEngine;
using UnityEngine.UI;

namespace Geayi.UI
{
    public static class UILabel
    {
        public static Text CreateLabel(Transform parent, string text,
            int fontSize, Color color)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) t.font = f;
            return t;
        }
    }
}
