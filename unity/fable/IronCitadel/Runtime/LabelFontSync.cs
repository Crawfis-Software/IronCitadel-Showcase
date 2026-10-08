using UnityEngine;

namespace IronCitadel
{
    /// <summary>Keeps a label material's texture pointed at the dynamic font atlas, which Unity rebuilds at will.</summary>
    [ExecuteAlways]
    public class LabelFontSync : MonoBehaviour
    {
        public Font font;
        Material _mat;

        void OnEnable()
        {
            var mr = GetComponent<MeshRenderer>();
            _mat = mr != null ? mr.sharedMaterial : null;
            var tm = GetComponent<TextMesh>();
            if (font != null && tm != null) font.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);
            Apply();
            Font.textureRebuilt += OnRebuilt;
        }

        void OnDisable() { Font.textureRebuilt -= OnRebuilt; }

        void OnRebuilt(Font f) { if (f == font) Apply(); }

        void Apply()
        {
            if (font == null || _mat == null || font.material == null) return;
            if (_mat.mainTexture != font.material.mainTexture) _mat.mainTexture = font.material.mainTexture;
        }
    }
}
