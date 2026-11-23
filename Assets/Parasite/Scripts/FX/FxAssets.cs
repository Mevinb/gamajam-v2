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

        /// <summary>Blobby slime splat: lumpy disc with a few satellite droplets (deterministic).</summary>
        public static Texture2D Splat(int size = 128)
        {
            var rnd = new System.Random(5);
            var drops = new Vector3[7];
            for (int i = 0; i < drops.Length; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.62f + (float)rnd.NextDouble() * 0.3f;
                drops[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.05f + (float)rnd.NextDouble() * 0.08f);
            }
            return Build(size, size, "Splat", (u, v) =>
            {
                float ang = Mathf.Atan2(v, u), r = Mathf.Sqrt(u * u + v * v);
                float edge = 0.5f + 0.07f * Mathf.Sin(ang * 5f + 1.3f) + 0.05f * Mathf.Sin(ang * 9f + 0.4f) + 0.03f * Mathf.Sin(ang * 17f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - r) / 0.06f));
                foreach (var d in drops)
                {
                    float dr = Mathf.Sqrt((u - d.x) * (u - d.x) + (v - d.y) * (v - d.y));
                    a = Mathf.Max(a, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d.z - dr) / 0.03f)));
                }
                return a;
            });
        }

        /// <summary>Horizontal streak: uniform along u, gaussian across v — trails.</summary>
        public static Texture2D Streak(int w = 64, int h = 32) => Build(w, h, "Streak", (u, v) => Mathf.Exp(-v * v * 5f) * (1f - v * v));
    }

    /// <summary>Material helpers shared by runtime fallbacks and the editor setup.</summary>
    public static class FxMaterials
    {
        public const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        public const string AmoebaShader = "Parasite/Amoeba";
        public const string NucleusShader = "Parasite/AmoebaNucleus";
        public const string SlimeShader = "Parasite/GroundWater";

        /// <summary>GroundWater as a glossy green slime decal: splat alpha x vertex alpha, faint bioluminescence.</summary>
        public static void ConfigureSlime(Material m, Texture tex)
        {
            if (!m) return;
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.08f, 0.36f, 0.1f, 1f));
            m.SetColor("_GlowColor", new Color(0.16f, 1.0f, 0.3f, 1f));
            m.SetFloat("_BedOpacity", 0.55f);
            m.SetFloat("_BedOpacityDeep", 0.7f);
            m.SetFloat("_Smoothness", 0.93f);
            m.SetFloat("_Coverage", 0.92f);
            m.SetFloat("_EdgeSoftness", 0.12f);
            m.SetFloat("_RippleStrength", 0.004f);
            m.SetFloat("_ReflectionBoost", 1.1f);
            m.SetFloat("_VertexCoverage", 1f);
            m.EnableKeyword("_VERTEX_COVERAGE");
            m.SetFloat("_WetFilm", 0f);
            m.DisableKeyword("_WETFILM");
            m.renderQueue = (int)RenderQueue.Transparent - 9;
        }

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
        static Material amoeba, nucleus, additive, alpha, ring, trail, slime;
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
        /// <summary>Slime decal material (GroundWater + vertex coverage) or null when the shader is unavailable.</summary>
        public static Material Slime
        {
            get
            {
                if (slime) return slime;
                slime = Resources.Load<Material>("FX/SlimeTrail");
                if (!slime)
                {
                    var sh = Shader.Find(FxMaterials.SlimeShader);
                    if (sh && sh.isSupported)
                    {
                        slime = new Material(sh) { name = "SlimeTrail (runtime)", hideFlags = HideFlags.DontUnloadUnusedAsset };
                        FxMaterials.ConfigureSlime(slime, FxTextures.Splat());
                    }
                }
                return slime;
            }
        }

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

        static Mesh ico;
        /// <summary>Smooth icosphere (radius 0.5 like the built-in sphere, 4 subdivisions = 5,120 triangles) for the jelly,
        /// so vertex wobble/pseudopods stay round instead of showing the UV-sphere poles.</summary>
        public static Mesh Icosphere()
        {
            if (ico) return ico;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new System.Collections.Generic.List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new System.Collections.Generic.List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < 4; s++)
            {
                var mid = new System.Collections.Generic.Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized); return mid[key] = v.Count - 1;
                }
                var nf = new System.Collections.Generic.List<int>(f.Count * 4);
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            var n = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) { n[i] = v[i]; v[i] *= 0.5f; }
            ico = new Mesh { name = "JellyIcosphere" };
            ico.SetVertices(v); ico.SetNormals(n); ico.SetTriangles(f, 0);
            ico.RecalculateBounds();
            ico.bounds = new Bounds(Vector3.zero, Vector3.one * 1.6f);   // room for wobble + pseudopods
            return ico;
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
