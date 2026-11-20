using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.EditorTools
{
    /// <summary>Repeatable project configuration: runtime material templates, player settings.</summary>
    public static class ProjectSetup
    {
        const string MatDir = "Assets/Parasite/Resources/Materials";

        [MenuItem("Parasite/Setup/Project Setup")]
        public static void Run()
        {
            Directory.CreateDirectory(MatDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            var m = Ensure($"{MatDir}/LitTemplate.mat", lit);
            m.EnableKeyword("_EMISSION");
            m.EnableKeyword("_NORMALMAP");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);

            var t = Ensure($"{MatDir}/TransparentTemplate.mat", lit);
            t.SetFloat("_Surface", 1f);
            t.SetFloat("_Blend", 0f);
            t.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            t.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            t.SetFloat("_ZWrite", 0f);
            t.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            t.renderQueue = (int)RenderQueue.Transparent;
            t.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(t);

            var u = Ensure($"{MatDir}/UnlitTemplate.mat", unlit);
            EditorUtility.SetDirty(u);

            PlayerSettings.companyName = "Parasite Jam Team";
            PlayerSettings.productName = "PARASITE";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Parasite] Project setup complete.");
        }

        static Material Ensure(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }
    }
}
