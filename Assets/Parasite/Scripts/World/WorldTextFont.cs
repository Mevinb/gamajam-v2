using UnityEngine;

namespace Parasite.World
{
    /// <summary>Keeps depth-tested TextMesh signs bound to Unity's dynamic font atlas without changing UI materials.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class WorldTextFont : MonoBehaviour
    {
        [SerializeField] Material template;
        TextMesh textMesh;
        MeshRenderer meshRenderer;
        Material instance;

        public void Configure(Material material)
        {
            template = material;
            RefreshAtlas();
        }

        void OnEnable()
        {
            Font.textureRebuilt += OnFontRebuilt;
            RefreshAtlas();
        }

        void OnDisable()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            if (meshRenderer && template) meshRenderer.sharedMaterial = template;
            if (instance)
            {
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
                instance = null;
            }
        }

        void OnFontRebuilt(Font font)
        {
            if (textMesh && textMesh.font == font) RefreshAtlas();
        }

        void RefreshAtlas()
        {
            if (!textMesh) textMesh = GetComponent<TextMesh>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!template || !textMesh || !textMesh.font || !meshRenderer) return;
            // Runtime clones keep Play Mode from modifying the saved template. Edit Mode uses the shared template
            // so generated scenes serialize a real material asset rather than a temporary hidden material.
            var material = template;
            if (Application.isPlaying)
            {
                if (!instance) instance = new Material(template) { name = template.name + " (World Sign)", hideFlags = HideFlags.DontSave };
                material = instance;
            }
            material.mainTexture = textMesh.font.material.mainTexture;
            meshRenderer.sharedMaterial = material;
        }
    }
}
