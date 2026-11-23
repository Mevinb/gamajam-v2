using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>
    /// Fading slime decals on the ground (one world-space mesh, ring buffer of quads, glossy wet material with
    /// screen-space reflections). Used for the free amoeba's trail and for splats where it hits, lands or oozes out.
    /// </summary>
    public class SlimeTrail : MonoBehaviour
    {
        const int Max = 64;
        public float life = 7f;

        Mesh mesh;
        Vector3[] verts;
        Color[] cols;
        Vector2[] uvs;
        float[] born, strength;
        int head;
        bool dirty;

        static SlimeTrail world;
        /// <summary>Time source for new decals (edit-mode photo captures override it).</summary>
        public static float ClockOverride = -1f;
        static float Clock => ClockOverride >= 0f ? ClockOverride : Time.time;

        /// <summary>Shared splat layer for impacts (lives until the scene unloads).</summary>
        public static SlimeTrail World
        {
            get
            {
                if (world) return world;
                world = Create("SlimeSplats", 10f);
                return world;
            }
        }

        public static SlimeTrail Create(string name, float life)
        {
            var mat = FxAssets.Slime;
            if (!mat) return null;
            var go = new GameObject(name);
            var t = go.AddComponent<SlimeTrail>();
            t.life = life;
            t.Init(mat);
            return t;
        }

        void Init(Material mat)
        {
            verts = new Vector3[Max * 4]; cols = new Color[Max * 4]; uvs = new Vector2[Max * 4];
            born = new float[Max]; strength = new float[Max];
            var tris = new int[Max * 6];
            for (int i = 0; i < Max; i++)
            {
                int v = i * 4, k = i * 6;
                tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1; tris[k + 3] = v; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
                uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(1, 0); uvs[v + 2] = new Vector2(1, 1); uvs[v + 3] = new Vector2(0, 1);
                born[i] = -999f;
            }
            mesh = new Mesh { name = "SlimeDecals" };
            mesh.MarkDynamic();
            mesh.vertices = verts; mesh.uv = uvs; mesh.colors = cols; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        /// <summary>Drop a decal lying on the surface below <paramref name="pos"/> (raycast), oriented along <paramref name="along"/>.</summary>
        public bool Drop(Vector3 pos, float width, float length, Vector3 along, float alpha = 1f, float maxDrop = 2.5f)
        {
            if (mesh == null) return false;
            if (!Physics.Raycast(pos + Vector3.up * 0.25f, Vector3.down, out var hit, maxDrop + 0.25f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return false;
            var n = hit.normal;
            if (n.y < 0.6f) return false;
            var f = Vector3.ProjectOnPlane(along.sqrMagnitude > 1e-4f ? along : Vector3.forward, n).normalized;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.Cross(n, Vector3.right).normalized;
            var r = Vector3.Cross(n, f).normalized;
            var c = hit.point + n * 0.022f;
            int i = head; head = (head + 1) % Max;
            int v = i * 4;
            Vector3 hr = r * (width * 0.5f), hf = f * (length * 0.5f);
            verts[v] = c - hr - hf; verts[v + 1] = c + hr - hf; verts[v + 2] = c + hr + hf; verts[v + 3] = c - hr + hf;
            born[i] = Clock; strength[i] = Mathf.Clamp01(alpha);
            mesh.vertices = verts;
            dirty = true;
            return true;
        }

        void LateUpdate() => Tick(Time.time);

        /// <summary>Update the fade (LateUpdate; edit-mode callers pass their own clock).</summary>
        public void Tick(float now)
        {
            if (mesh == null) return;
            bool any = false;
            for (int i = 0; i < Max; i++)
            {
                float age = now - born[i];
                float a = age < 0f || age > life ? 0f : strength[i] * Mathf.Pow(1f - age / life, 1.6f) * Mathf.Clamp01(age * 8f + 0.3f);
                var c = new Color(1f, 1f, 1f, a);
                int v = i * 4;
                if (cols[v].a != a || dirty) { cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = c; any = true; }
            }
            if (any) mesh.colors = cols;
            dirty = false;
        }

        void OnDestroy()
        {
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (world == this) world = null;
        }
    }
}
