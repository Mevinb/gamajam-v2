using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>
    /// WorldTextFont's dynamic-atlas binding pattern for wholly runtime-owned Northline signs.
    /// The font's UI material and saved world materials are never modified.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class RailWorldLabelMaterial : MonoBehaviour
    {
        public const string ShaderName = "Parasite/WorldText";
        public Material OwnedMaterial => owned;
        TextMesh textMesh;
        MeshRenderer meshRenderer;
        Material owned, previous;
        ShadowCastingMode previousShadowCasting;
        bool previousReceiveShadows, refreshing;

        void OnEnable()
        {
            Font.textureRebuilt += OnFontRebuilt;
            RefreshAtlas();
        }

        /// <summary>Rebind after an explicit font/text replacement as well as dynamic-atlas rebuilds.</summary>
        public void RefreshAtlas() => BindAtlas(requestCharacters: true);

        void OnFontRebuilt(Font font)
        {
            if (textMesh && textMesh.font == font) BindAtlas(requestCharacters: false);
        }

        void BindAtlas(bool requestCharacters)
        {
            if (!isActiveAndEnabled || refreshing) return;
            if (!textMesh) textMesh = GetComponent<TextMesh>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!textMesh || !textMesh.font || !meshRenderer) return;
            refreshing = true;
            try
            {
                if (!owned)
                {
                    var shader = Shader.Find(ShaderName);
                    if (!shader) throw new System.InvalidOperationException("Northline world-label shader is missing: " + ShaderName);
                    previous = meshRenderer.sharedMaterial;
                    previousShadowCasting = meshRenderer.shadowCastingMode;
                    previousReceiveShadows = meshRenderer.receiveShadows;
                    owned = new Material(shader)
                    {
                        name = "Northline owned world-label material", hideFlags = HideFlags.DontSave,
                        enableInstancing = true,
                    };
                    owned.SetColor("_Color", Color.white); // TextMesh supplies the authored vertex tint.
                }
                if (requestCharacters && textMesh.font.dynamic)
                    textMesh.font.RequestCharactersInTexture(textMesh.text, textMesh.fontSize, textMesh.fontStyle);
                owned.mainTexture = textMesh.font.material.mainTexture;
                meshRenderer.sharedMaterial = owned;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
            finally { refreshing = false; }
        }

        void OnDisable() => Release();
        void OnDestroy() => Release();

        void Release()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            if (!owned) return;
            // A later renderer owner may have installed a different material. Never overwrite that handoff.
            if (meshRenderer && meshRenderer.sharedMaterial == owned)
            {
                meshRenderer.sharedMaterial = previous;
                meshRenderer.shadowCastingMode = previousShadowCasting;
                meshRenderer.receiveShadows = previousReceiveShadows;
            }
            var retired = owned; owned = null; previous = null;
            if (Application.isPlaying) Destroy(retired);
            else DestroyImmediate(retired);
        }
    }
}
