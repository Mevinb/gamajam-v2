using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>Procedural particle textures. Used at runtime as a fallback and by FxSetup to bake PNGs.</summary>
    public static class FxTextures
    {
        static Texture2D New(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        static Texture2D Build(int w, int h, string name, System.Func<float, float, float> alpha)
        {
            var t = New(w, h, name);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Soft round dot (gaussian falloff) — droplets, sparkles, mist.</summary>
        public static Texture2D SoftDot(int size = 64) => Build(size, size, "SoftDot", (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return r >= 1f ? 0f : Mathf.Exp(-r * r * 4.5f) * (1f - r * r);
        });

        /// <summary>Soft glowing annulus with a faint fill — shockwaves and the target ring.</summary>
        public static Texture2D Ring(int size = 128) => Build(size, size, "Ring", (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r >= 1f) return 0f;
            float band = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.075f, 2f));
            float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.78f, r)) * 0.18f * (r < 0.8f ? 1f : 0f);
            return band + inner;
        });

        /// <summary>Horizontal streak: uniform along u, gaussian across v — trails.</summary>
        public static Texture2D Streak(int w = 64, int h = 32) => Build(w, h, "Streak", (u, v) => Mathf.Exp(-v * v * 5f) * (1f - v * v));
    }

    /// <summary>Material helpers shared by runtime fallbacks and the editor setup.</summary>
    public static class FxMaterials
    {
        public const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        public const string AmoebaShader = "Parasite/Amoeba";
        public const string NucleusShader = "Parasite/AmoebaNucleus";

        /// <summary>Configure a URP Particles/Unlit material as transparent alpha or additive (mirrors BaseShaderGUI).</summary>
        public static void ConfigureParticle(Material m, bool additive, Texture tex)
        {
            if (!m) return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            if (tex) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        }
    }

    /// <summary>Runtime access to FX materials: Resources/FX (baked by FxSetup) → runtime-built → MatFactory fallback.</summary>
    internal static class FxAssets
    {
        static Material amoeba, nucleus, additive, alpha, ring, trail;
        static Texture2D softTex, ringTex, streakTex;

        static Material LoadShaderMat(string res, string shader, ref Material slot)
        {
            if (slot) return slot;
            slot = Resources.Load<Material>(res);
            if (!slot)
            {
                var sh = Shader.Find(shader);
                if (sh && sh.isSupported) slot = new Material(sh) { name = shader, hideFlags = HideFlags.DontUnloadUnusedAsset };
            }
            return slot;
        }

        /// <summary>Template amoeba shell material or null if the shader is unavailable.</summary>
        public static Material AmoebaTemplate => LoadShaderMat("FX/AmoebaMaterial", FxMaterials.AmoebaShader, ref amoeba);
        public static Material NucleusTemplate => LoadShaderMat("FX/AmoebaNucleus", FxMaterials.NucleusShader, ref nucleus);

        /// <summary>A unique shell material (caller destroys). isShader=false means a URP Lit emissive fallback.</summary>
        public static Material NewShell(out bool isShader)
        {
            var t = AmoebaTemplate;
            isShader = t;
            return isShader ? new Material(t) : new Material(MatFactory.Emissive(new Color(0.3f, 0.9f, 0.32f), Palette.Amoeba * 0.5f));
        }

        public static Material NewNucleus(out bool isShader)
        {
            var t = NucleusTemplate;
            isShader = t;
            return isShader ? new Material(t) : new Material(MatFactory.Emissive(new Color(0.6f, 1f, 0.4f), Palette.Amoeba * 2f));
        }

        static Material Particle(ref Material slot, string res, bool add, Texture2D tex, Color fallback)
        {
            if (slot) return slot;
            slot = Resources.Load<Material>(res);
            if (slot) return slot;
            var sh = Shader.Find(FxMaterials.ParticleShader);
            if (sh && sh.isSupported)
            {
                slot = new Material(sh) { name = res, hideFlags = HideFlags.DontUnloadUnusedAsset };
                FxMaterials.ConfigureParticle(slot, add, tex);
            }
            else slot = MatFactory.Unlit(fallback);
            return slot;
        }

        static Texture2D Tex(ref Texture2D slot, string res, System.Func<Texture2D> make)
        {
            if (slot) return slot;
            slot = Resources.Load<Texture2D>(res);
            if (!slot) { slot = make(); slot.hideFlags = HideFlags.DontUnloadUnusedAsset; }
            return slot;
        }
        static Texture2D Soft => Tex(ref softTex, "FX/SoftDot", () => FxTextures.SoftDot());
        static Texture2D RingTex => Tex(ref ringTex, "FX/Ring", () => FxTextures.Ring());
        static Texture2D StreakTex => Tex(ref streakTex, "FX/Streak", () => FxTextures.Streak());

        public static Material Additive => additive ? additive : Particle(ref additive, "FX/ParticleAdditive", true, Soft, Palette.Amoeba);
        public static Material Alpha => alpha ? alpha : Particle(ref alpha, "FX/ParticleAlpha", false, Soft, Palette.Amoeba * 0.7f);
        public static Material Ring => ring ? ring : Particle(ref ring, "FX/RingAdditive", true, RingTex, Palette.Amoeba);
        public static Material Trail => trail ? trail : Particle(ref trail, "FX/TrailAdditive", true, StreakTex, Palette.Amoeba);
        /// <summary>True if Ring is a real textured particle material (not an opaque fallback).</summary>
        public static bool RingIsTextured => Ring && Ring.shader && Ring.shader.name == FxMaterials.ParticleShader;
    }

    /// <summary>Particle-system construction helpers.</summary>
    internal static class FxParticles
    {
        public static ParticleSystem Make(string name, Transform parent, Vector3 localPos, Material mat, bool worldSpace = true)
        {
            var go = new GameObject(name);
            if (parent) { go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); }
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        public static void Burst(ParticleSystem ps, int count, float delay = 0f)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
        }

        public static void FadeOut(ParticleSystem ps, float startAlpha = 1f, float holdFrac = 0.5f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, holdFrac), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static void SizeCurve(ParticleSystem ps, float start, float end)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
        }

        public static ParticleSystem.MinMaxGradient TwoTone(Color a, Color b) => new ParticleSystem.MinMaxGradient(a, b);
    }

    /// <summary>Collider-free primitive renderers (built-in meshes cached once; no physics side effects).</summary>
    internal static class FxPrim
    {
        static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> meshes = new();

        static Mesh MeshFor(PrimitiveType type)
        {
            if (meshes.TryGetValue(type, out var m) && m) return m;
            var tmp = GameObject.CreatePrimitive(type);
            m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            meshes[type] = m;
            return m;
        }

        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat,
            string name, Quaternion? localRot = null, bool shadows = false)
        {
            var go = new GameObject(name);
            if (parent) { go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); }
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(type);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }
    }
}
