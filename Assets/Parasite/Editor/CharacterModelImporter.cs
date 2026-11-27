using System.Collections.Generic;
using System.IO;
using Parasite.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.EditorTools
{
    /// <summary>
    /// Imports the MPFB-generated characters (Tools/blender/characters/build_characters.sh) and turns them into runtime
    /// prefabs: Art/Characters/Models/&lt;Id&gt;.fbx (+ .json metadata) and Art/Characters/Textures/* →
    /// Resources/Characters/Materials (Parasite/Character shader), Resources/Characters/Prefabs/&lt;Id&gt;.prefab (LODGroup,
    /// humanoid Animator) and Resources/Characters/CharacterCatalog.asset.
    /// Batch: Tools/unity.sh chars-import -executeMethod Parasite.EditorTools.CharacterModelImporter.RebuildBatch
    /// </summary>
    public class CharacterModelImporter : AssetPostprocessor
    {
        public const string ModelsDir = "Assets/Parasite/Art/Characters/Models";
        public const string TexturesDir = "Assets/Parasite/Art/Characters/Textures";
        public const string ResourcesDir = "Assets/Parasite/Resources/Characters";
        public const string MaterialsDir = ResourcesDir + "/Materials";
        public const string PrefabsDir = ResourcesDir + "/Prefabs";
        public const string CatalogPath = ResourcesDir + "/CharacterCatalog.asset";
        const string ShaderName = "Parasite/Character";

        static bool IsOurs(string path) =>
            path.StartsWith(ModelsDir + "/", System.StringComparison.Ordinal) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsOurs(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importConstraints = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.optimizeGameObjects = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = false;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.optimizeBones = false; // keep every humanoid bone in the skin even if decimation left it unweighted
            mi.weldVertices = true;
            mi.addCollider = false;
            // materials are named Skin / Eyes / Hair / Cloth in the FBX; prefabs get the real ones by name
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        // ------------------------------------------------------------------ metadata

        [System.Serializable]
        class Tints { public float[] A, B, C; }

        [System.Serializable]
        class Palettes { public string A = "", B = "", C = ""; }

        [System.Serializable]
        class TexNames { public string skin, skinN, skinM, eyes, clothA, clothN, clothM, hairA; }

        [System.Serializable]
        class Meta
        {
            public string id, profession, skin, eyes;
            public string[] names;
            public bool unique;
            public bool oralAnatomy;
            public float gender, age, height;
            public float[] skinTint;
            public Tints tints;
            public Palettes palettes;
            public int[] lodTris;
            public TexNames textures;
        }

        // ------------------------------------------------------------------ batch

        [MenuItem("Parasite/Characters/Rebuild Character Prefabs")]
        public static void Rebuild() => RebuildInternal(false);

        static void RebuildInternal(bool lakeGirlOnly)
        {
            AssetDatabase.Refresh();
            EnsureFolder(ResourcesDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            var shader = Shader.Find(ShaderName);
            if (!shader) throw new System.InvalidOperationException("Shader " + ShaderName + " is missing (Assets/Parasite/Shaders/Character.shader).");

            var metas = new List<Meta>();
            foreach (var json in Directory.GetFiles(ModelsDir, "*.json"))
            {
                var m = JsonUtility.FromJson<Meta>(File.ReadAllText(json));
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                if (lakeGirlOnly && m.id != "LakeGirl") continue;
                if (!File.Exists(Path.Combine(ModelsDir, m.id + ".fbx"))) { Debug.LogWarning("[Chars] missing fbx for " + m.id); continue; }
                metas.Add(m);
            }
            metas.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (lakeGirlOnly && (metas.Count != 1 || !metas[0].oralAnatomy))
                throw new System.InvalidOperationException("LakeGirl oral pilot metadata is not installed.");

            // textures first (explicit settings, no project-wide texture postprocessor)
            if (!lakeGirlOnly) ConfigureTexture(TexturesDir + "/Skin_VeinMask.png", TexKind.Data, 2048);
            var configuredSurfaces = new HashSet<string>();
            foreach (var m in metas)
            {
                if (!lakeGirlOnly)
                {
                    ConfigureTexture(TexturesDir + "/" + m.textures.skin, TexKind.SkinAlbedo, 2048);
                    if (!string.IsNullOrEmpty(m.textures.skinN) && configuredSurfaces.Add(m.textures.skinN))
                        ConfigureTexture(TexturesDir + "/" + m.textures.skinN, TexKind.SkinNormal, 2048);
                    if (!string.IsNullOrEmpty(m.textures.skinM) && configuredSurfaces.Add(m.textures.skinM))
                        ConfigureTexture(TexturesDir + "/" + m.textures.skinM, TexKind.SkinSurface, 1024);
                    ConfigureTexture(TexturesDir + "/" + m.textures.eyes, TexKind.EyeAlbedo, 1024);
                }
                ConfigureTexture(TexturesDir + "/" + m.textures.clothA, m.oralAnatomy ? TexKind.OralAlbedo : TexKind.Albedo, m.oralAnatomy ? 4096 : 2048);
                ConfigureTexture(TexturesDir + "/" + m.textures.clothN, m.oralAnatomy ? TexKind.OralNormal : TexKind.Normal, m.oralAnatomy ? 4096 : 2048);
                ConfigureTexture(TexturesDir + "/" + m.textures.clothM, m.oralAnatomy ? TexKind.OralMask : TexKind.Mask, m.oralAnatomy ? 2048 : 1024);
                if (!lakeGirlOnly) ConfigureTexture(TexturesDir + "/" + m.textures.hairA, TexKind.Hair, 2048);
            }
            // models (force our import settings even if Unity imported them before this postprocessor existed)
            foreach (var m in metas)
            {
                string path = ModelsDir + "/" + m.id + ".fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                if (mi == null) continue;
                if (mi.animationType != ModelImporterAnimationType.Human || !mi.bakeAxisConversion || mi.importAnimation || mi.optimizeBones)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (!LoadAvatar(path)) ResetAvatarMapping(mi, path);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (!lakeGirlOnly) catalog.entries.Clear();
            else if (!catalog.entries.Exists(e => e.id == "LakeGirl"))
                throw new System.InvalidOperationException("Existing LakeGirl catalog entry is required for the pilot.");
            var vein = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/Skin_VeinMask.png");
            int ok = 0;
            foreach (var m in metas)
            {
                try
                {
                    var entry = BuildCharacter(m, shader, vein);
                    if (entry != null)
                    {
                        if (lakeGirlOnly) catalog.entries[catalog.entries.FindIndex(e => e.id == entry.id)] = entry;
                        else catalog.entries.Add(entry);
                        ok++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Chars] failed " + m.id + ": " + e);
                }
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Chars] catalog: {ok}/{metas.Count} characters → {CatalogPath}");
            if (ok != metas.Count) throw new System.InvalidOperationException("Some characters failed to build.");
        }

        public static void RebuildBatch()
        {
            try { Rebuild(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // Explicit pilot entry: reuse shared maps and leave other 24 catalog entries/prefabs alone.
        public static void RebuildLakeGirlOralPilotBatch()
        {
            try { RebuildInternal(true); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Avatar LoadAvatar(string fbx)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (a is Avatar av && av.isValid && av.isHuman) return av;
            return null;
        }

        /// <summary>
        /// The bone mapping is stored in the .meta on first import; if the FBX changed since (regenerated character), a
        /// stale mapping makes the avatar invalid. Clear it so Unity auto-maps the (Unity-named) skeleton again.
        /// </summary>
        static void ResetAvatarMapping(ModelImporter mi, string path)
        {
            Debug.Log("[Chars] re-mapping humanoid avatar for " + path);
            mi.humanDescription = new HumanDescription
            {
                human = new HumanBone[0], skeleton = new SkeletonBone[0],
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            mi.autoGenerateAvatarMappingIfUnspecified = true;
            mi.SaveAndReimport();
            if (!LoadAvatar(path)) Debug.LogError("[Chars] avatar still invalid after re-mapping: " + path);
        }

        // Canonical CPU measurement + seven image hashes live beside the Blender recipe.
        // These are photographic magnification pivots, not optical-axis or gaze targets.
        const string EyeCalibrationPath = "Tools/blender/characters/eye_atlas_calibration.json";
        const string EyeCalibrationSHA256 = "c66f9eeddc217c1aa3b42dfb0dd9d5852e42dc3e7bfe737424a9cd5328d1e95b";
        [System.Serializable] class EyeAtlas { public string path, sha256; public float[] centres; }
        [System.Serializable] class EyeActor { public string id, atlas, metadataSHA256; }
        [System.Serializable] class EyeCalibration { public int schema; public EyeAtlas[] atlases; public EyeActor[] actors; }
        static string EyeHash(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static EyeCalibration LoadEyeCalibration()
        {
            if (EyeHash(EyeCalibrationPath) != EyeCalibrationSHA256) throw new System.InvalidOperationException("Eye calibration dataset changed; remeasure/review before updating its pin");
            var data = JsonUtility.FromJson<EyeCalibration>(File.ReadAllText(EyeCalibrationPath));
            if (data == null || data.schema != 1 || data.atlases == null || data.atlases.Length != 7 || data.actors == null || data.actors.Length != 25)
                throw new System.InvalidOperationException("Expected seven measured photographs and25 actor records");
            var paths = new HashSet<string>(); var ids = new HashSet<string>();
            foreach (var atlas in data.atlases)
                if (atlas == null || !paths.Add(atlas.path) || atlas.path != "Eyes/" + Path.GetFileName(atlas.path) || atlas.centres == null || atlas.centres.Length != 4 || EyeHash(TexturesDir + "/" + atlas.path) != atlas.sha256)
                    throw new System.InvalidOperationException("Measured eye photograph/path/hash changed: " + atlas?.path);
            foreach (var actor in data.actors)
                if (actor == null || !ids.Add(actor.id) || !paths.Contains(actor.atlas)) throw new System.InvalidOperationException("Invalid finite eye actor/atlas record");
            return data;
        }
        static Vector4 CalibratedEyeCentres(EyeCalibration data, string id, string atlasPath)
        {
            var actor = System.Array.Find(data.actors, a => a.id == id);
            if (actor == null || actor.atlas != atlasPath) throw new System.InvalidOperationException("Eye metadata differs from calibration: " + id);
            if (id == "LakeGirl") return new Vector4(.292f, .289f, .707f, .704f);
            var c = System.Array.Find(data.atlases, a => a.path == atlasPath).centres;
            return new Vector4(c[0], c[1], c[2], c[3]);
        }
        static Vector4 CalibratedEyeCentres(string id, string atlasPath) => CalibratedEyeCentres(LoadEyeCalibration(), id, atlasPath);

        /// <summary>After all25 visual approvals: preflight everything, then update only existing Eyes materials.</summary>
        public static void ApplyEyeAtlasCalibrationBatch()
        {
            try
            {
                var data = LoadEyeCalibration(); // pins all seven actual source photographs before any material write
                var updates = new List<(string id, Material material, Vector4 centres)>(); var ids = new HashSet<string>();
                foreach (string path in Directory.GetFiles(ModelsDir, "*.json"))
                {
                    var m = JsonUtility.FromJson<Meta>(File.ReadAllText(path));
                    if (m == null || string.IsNullOrEmpty(m.id) || m.textures == null) throw new System.InvalidOperationException("Invalid eye metadata: " + path);
                    var record = System.Array.Find(data.actors, a => a.id == m.id);
                    if (!ids.Add(m.id) || record == null || EyeHash(path) != record.metadataSHA256 || record.atlas != m.textures.eyes)
                        throw new System.InvalidOperationException("Finite eye metadata id/atlas/hash changed: " + path);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + m.id + "_Eyes.mat");
                    string expectedAtlas = TexturesDir + "/" + record.atlas;
                    var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(expectedAtlas);
                    if (!material || !material.shader || material.shader.name != ShaderName || !material.HasProperty("_EyeAtlasCentres") || !material.HasProperty("_IrisUVScale") || !atlas || material.GetTexture("_BaseMap") != atlas || AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) != expectedAtlas || atlas.width != 1024 || atlas.height != 1024 || atlas.wrapMode != TextureWrapMode.Clamp)
                        throw new System.InvalidOperationException("Existing Character Eyes shader/actual BaseMap binding differs: " + m.id + " expected " + expectedAtlas);
                    updates.Add((m.id, material, CalibratedEyeCentres(data, m.id, record.atlas)));
                    Debug.Log($"[EyeCalibration] PREFLIGHT {m.id} material={AssetDatabase.GetAssetPath(material)} atlas={expectedAtlas} imageSHA256={EyeHash(expectedAtlas)}");
                }
                if (updates.Count != 25) throw new System.InvalidOperationException("Exactly25 existing Eyes materials required");
                foreach (var update in updates)
                {
                    update.material.SetVector("_EyeAtlasCentres", update.centres); update.material.SetFloat("_IrisUVScale", 1.42f);
                    EditorUtility.SetDirty(update.material); AssetDatabase.SaveAssetIfDirty(update.material);
                    Debug.Log($"[EyeCalibration] {update.id} photographPivot={update.centres:R} scale=1.42; existing GUID retained");
                }
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        static CharacterCatalog.Entry BuildCharacter(Meta m, Shader shader, Texture2D vein)
        {
            string fbx = ModelsDir + "/" + m.id + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (!model) throw new System.Exception("model not imported: " + fbx);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx);
            Avatar av = null;
            foreach (var a in avatar) if (a is Avatar x) av = x;
            if (!av || !av.isValid || !av.isHuman) throw new System.Exception("avatar invalid / not humanoid for " + m.id);

            Texture2D T(string rel) => string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + rel);
            var skin = Mat(m.id + "_Skin", shader, mat =>
            {
                mat.SetTexture("_BaseMap", T(m.textures.skin));
                var normal = T(m.textures.skinN);
                var surface = T(m.textures.skinM);
                if ((!string.IsNullOrEmpty(m.textures.skinN) && !normal) || (!string.IsNullOrEmpty(m.textures.skinM) && !surface))
                    throw new System.InvalidOperationException(m.id + ": authored skin surface is missing; run the Blender skin bake before rebuilding.");
                bool authored = normal && surface;
                mat.SetTexture("_BumpMap", normal);
                mat.SetTexture("_SkinSurfaceMap", surface);
                if (authored) mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_SPECULAR_SETUP");
                // Plain Color (no HDR/Gamma flag): SetColor stores raw linear F0.
                mat.SetColor("_SpecColor", new Color(0.028f, 0.028f, 0.028f));
                mat.SetFloat("_BumpScale", m.age < 0.45f ? 0.75f : 1f);
                mat.SetFloat("_SkinSurfaceEnabled", authored ? 1f : 0f);
                mat.SetFloat("_OcclusionStrength", 0.65f);
                var st = m.skinTint != null && m.skinTint.Length >= 3 ? new Color(m.skinTint[0], m.skinTint[1], m.skinTint[2], 1) : Color.white;
                mat.SetColor("_BaseColor", st);
                mat.SetFloat("_Smoothness", authored ? 1f : 0.42f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_CharacterMode", 0f);
                mat.SetTexture("_VeinMap", vein);
                mat.SetColor("_VeinColor", Color.black);
                mat.SetFloat("_Wrap", 0.45f);
                mat.SetFloat("_Cull", 2f);
            });
            var eyes = Mat(m.id + "_Eyes", shader, mat =>
            {
                mat.SetTexture("_BaseMap", T(m.textures.eyes));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.38f);
                mat.SetColor("_SpecColor", new Color(0.025f, 0.025f, 0.025f));
                mat.EnableKeyword("_SPECULAR_SETUP");
                mat.EnableKeyword("_CLEARCOAT");
                mat.SetFloat("_ClearCoatMask", 0.9f);
                mat.SetFloat("_ClearCoatSmoothness", 0.94f);
                mat.SetFloat("_CharacterMode", 1f);
                // Photographic magnification pivot only; no destination/gaze recentering.
                // LakeGirl retains its accepted fixed-centre1.42 configuration.
                mat.SetVector("_EyeAtlasCentres", CalibratedEyeCentres(m.id, m.textures.eyes));
                mat.SetFloat("_IrisUVScale", 1.42f);
                mat.SetFloat("_Cull", 2f);
            });
            var hair = Mat(m.id + "_Hair", shader, mat =>
            {
                mat.SetTexture("_BaseMap", T(m.textures.hairA));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.43f);
                mat.SetFloat("_CharacterMode", 2f);
                mat.SetFloat("_Cutoff", 0.4f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_HairForwardCutoff", 0.02f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetFloat("_Cull", 0f);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            });
            var cloth = Mat(m.id + "_Cloth", shader, mat =>
            {
                mat.SetTexture("_BaseMap", T(m.textures.clothA));
                mat.SetTexture("_BumpMap", T(m.textures.clothN));
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MaskMap", T(m.textures.clothM));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_BumpScale", m.id == "LakeGirl" ? 0.30f : 0.65f);
                mat.SetFloat("_CharacterMode", 3f);
                mat.SetFloat("_Cull", 2f);
                mat.SetColor("_TintA", Lin(m.tints?.A));
                mat.SetColor("_TintB", Lin(m.tints?.B));
                mat.SetColor("_TintC", Lin(m.tints?.C));
            });
            var byName = new Dictionary<string, Material> { ["Skin"] = skin, ["Eyes"] = eyes, ["Hair"] = hair, ["Cloth"] = cloth };

            var inst = (GameObject)Object.Instantiate(model);
            inst.name = m.id;
            try
            {
                var lods = new Renderer[3][];
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int matCount = 0;
                foreach (var r in smrs)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string n = mats[i] ? mats[i].name : "";
                        foreach (var kv in byName) if (n == kv.Key || n.StartsWith(kv.Key + ".") || n.StartsWith(kv.Key + " ")) { mats[i] = kv.Value; break; }
                        if (!System.Array.Exists(new[] { skin, eyes, hair, cloth }, x => x == mats[i]))
                            throw new System.Exception($"{m.id}: unexpected material '{n}' on {r.name}");
                    }
                    matCount = Mathf.Max(matCount, mats.Length);
                    r.sharedMaterials = mats;
                    r.updateWhenOffscreen = false;
                    r.skinnedMotionVectors = false;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    for (int l = 0; l < 3; l++)
                        if (r.name.EndsWith("_LOD" + l)) lods[l] = new Renderer[] { r };
                }
                for (int l = 0; l < 3; l++) if (lods[l] == null) throw new System.Exception($"{m.id}: LOD{l} renderer missing");
                var lg = inst.GetComponent<LODGroup>();
                if (!lg) lg = inst.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(0.16f, lods[0]), new LOD(0.055f, lods[1]), new LOD(0.01f, lods[2]) });
                lg.RecalculateBounds();
                var an = inst.GetComponent<Animator>();
                if (!an) an = inst.AddComponent<Animator>();
                an.avatar = av;
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                an.runtimeAnimatorController = null;
                var head = an.GetBoneTransform(HumanBodyBones.Head);
                if (!head) throw new System.Exception($"{m.id}: humanoid avatar has no Head bone");
                string prefabPath = PrefabsDir + "/" + m.id + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                Debug.Log($"[Chars] OK {m.id} ({m.profession}) tris {string.Join("/", m.lodTris ?? new int[0])} height {m.height:0.00} m, {matCount} materials, head '{head.name}'");
                var e = new CharacterCatalog.Entry
                {
                    id = m.id,
                    profession = System.Enum.TryParse<Profession>(m.profession, out var p) ? p : Profession.Civilian,
                    names = m.names ?? new string[0],
                    unique = m.unique,
                    gender = m.gender,
                    height = m.height,
                    prefab = prefab,
                    tintA = Lin(m.tints?.A), tintB = Lin(m.tints?.B), tintC = Lin(m.tints?.C),
                    hasTintA = m.tints?.A != null && m.tints.A.Length >= 3,
                    hasTintB = m.tints?.B != null && m.tints.B.Length >= 3,
                    hasTintC = m.tints?.C != null && m.tints.C.Length >= 3,
                    paletteA = m.palettes?.A ?? "", paletteB = m.palettes?.B ?? "", paletteC = m.palettes?.C ?? "",
                    lodTriangles = m.lodTris ?? new int[0],
                };
                return e;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>Linear tint from metadata → Color for Material.SetColor (which expects gamma values in a linear project).</summary>
        static Color Lin(float[] v)
        {
            if (v == null || v.Length < 3) return Color.white;
            return new Color(v[0], v[1], v[2], 1f).gamma;
        }

        static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;
            if (!m) { m = new Material(shader) { name = name }; created = true; }
            m.shader = shader;
            m.shaderKeywords = new string[0];
            // Explicit opaque defaults: only Hair opts into blended coverage.
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.renderQueue = -1;
            m.SetOverrideTag("RenderType", "Opaque");
            setup(m);
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        enum TexKind { Albedo, SkinAlbedo, EyeAlbedo, Normal, SkinNormal, SkinSurface, Mask, Hair, Data, OralAlbedo, OralNormal, OralMask }

        static void ConfigureTexture(string path, TexKind kind, int maxSize)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[Chars] texture missing: " + path); return; }
            bool skinSurface = kind == TexKind.SkinNormal || kind == TexKind.SkinSurface;
            bool oral = kind == TexKind.OralAlbedo || kind == TexKind.OralNormal || kind == TexKind.OralMask;
            bool mask = kind == TexKind.Mask || kind == TexKind.OralMask;
            if (oral) ti.npotScale = TextureImporterNPOTScale.None;
            bool portraitAlbedo = kind == TexKind.SkinAlbedo || kind == TexKind.EyeAlbedo;
            ti.textureType = kind == TexKind.Normal || kind == TexKind.SkinNormal || kind == TexKind.OralNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = kind == TexKind.Albedo || portraitAlbedo || kind == TexKind.OralAlbedo || kind == TexKind.Hair;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = !mask;
            ti.filterMode = mask ? FilterMode.Point : FilterMode.Trilinear;
            ti.anisoLevel = mask ? 0 : 4;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaSource = kind == TexKind.Hair ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = kind == TexKind.Hair;
            ti.mipMapsPreserveCoverage = kind == TexKind.Hair;
            if (kind == TexKind.Hair) ti.alphaTestReferenceValue = 0.4f;
            ti.textureCompression = mask || kind == TexKind.Hair ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            ti.crunchedCompression = false;
            ti.streamingMipmaps = skinSurface;
            if (skinSurface || portraitAlbedo || oral)
            {
                // The filtered surface atlas must never inherit the cloth mask's
                // point sampling/no-mip policy. +Y tangent normals need no flip.
                ti.flipGreenChannel = false;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Standalone", overridden = true, maxTextureSize = maxSize,
                    format = kind == TexKind.SkinNormal || kind == TexKind.OralNormal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
                    textureCompression = TextureImporterCompression.CompressedHQ,
                    compressionQuality = 100,
                });
            }
            ti.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
