using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Parasite.Missions
{
    /// <summary>All-or-nothing visual-only kit binding. Owns instances, never imported meshes, materials, physics or controls.</summary>
    public sealed class RailAuthoredArt : MonoBehaviour
    {
        public sealed class Target
        {
            public string Asset; public Transform Parent; public Vector3 MetreOffset; public MeshRenderer[] Originals;
            public Target(string asset,Transform parent,Vector3 offset,IEnumerable<MeshRenderer> originals)
            { Asset=asset;Parent=parent;MetreOffset=offset;Originals=originals.ToArray(); }
        }
        [Serializable] public sealed class BindingReceipt
        {
            public string model,parent; public int[] originalRendererIds; public Vector3 physicalParentScale,counterScale,metreOffset;
            public Vector3 expectedMin,expectedMax,actualMin,actualMax; public float boundsError; public int actualColliders,actualBehaviours;
            public int[] materialInstanceIds; public string[] materialNames; public uint[] submeshIndexCounts;
        }
        sealed class KitItem { public RailArtContract.Model contract; public GameObject prefab; public Material[] materials; }
        public bool Applied { get; private set; }
        public string LastFailure { get; private set; } = "Not bound";
        public string PhysicsBeforeSha256 { get; private set; } = "";
        public string PhysicsAfterSha256 { get; private set; } = "";
        public IReadOnlyList<BindingReceipt> Bindings => receipts;
        readonly List<BindingReceipt> receipts = new();
        readonly List<GameObject> owned = new();
        readonly Dictionary<MeshRenderer,bool> originals = new();

        public static bool MaterialContract(Material material,out string reason)
        {
            reason=null;
            if(!material||!material.shader||material.shader.name!="Universal Render Pipeline/Lit"||material.GetFloat("_Surface")!=0||material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
                ||!material.IsKeywordEnabled("_NORMALMAP")||!material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")||material.GetFloat("_Metallic")!=1||material.GetFloat("_Smoothness")!=1)
            {reason="Complete opaque packed URP PBR material is required.";return false;}
            foreach(string property in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"})
            {
                var texture=material.GetTexture(property) as Texture2D;
                if(!texture||texture.GetType()!=typeof(Texture2D)||texture.dimension!=TextureDimension.Tex2D||texture.width!=2048||texture.height!=2048||texture.mipmapCount<=1||texture.format!=TextureFormat.BC7
                    ||GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)!=(property=="_BaseMap"))
                {reason="Original three2D/2K/BC7/mipmapped atlas maps and colour spaces are required.";return false;}
            }
            return true;
        }
        static bool Near(float a,float b)=>!float.IsNaN(a)&&!float.IsInfinity(a)&&Mathf.Abs(a-b)<=.0001f;
        static bool ColourIs(Color actual,Color expected)=>Near(actual.r,expected.r)&&Near(actual.g,expected.g)&&Near(actual.b,expected.b)&&Near(actual.a,expected.a);
        static bool Floats(Material material,params (string property,float value)[] values)
            =>values.All(pair=>material.HasProperty(pair.property)&&Near(material.GetFloat(pair.property),pair.value));
        static bool Keywords(Material material,params string[] expected)=>new HashSet<string>(material.shaderKeywords,StringComparer.Ordinal).SetEquals(expected);
        static long BlockMipBytes(Texture2D texture)
        {
            // BC5 and BC7 both occupy16 bytes per4x4 block. This is the exact
            // compressed mip payload estimate, not a measured GPU allocation.
            long bytes=0;int width=texture.width,height=texture.height;
            for(int mip=0;mip<texture.mipmapCount;mip++){bytes+=(long)((width+3)/4)*((height+3)/4)*16;width=Mathf.Max(1,width/2);height=Mathf.Max(1,height/2);}
            return bytes;
        }
        static bool CoachState(Material material,bool glass,out string reason)
        {
            reason=null;
            string expectedName=glass?RailArtContract.CoachGlassName:RailArtContract.CoachOpaqueName;
            if(!material||material.name!=expectedName||!material.shader||material.shader.name!="Universal Render Pipeline/Lit")
            {reason="Exact named coach URP Lit material is required: "+expectedName;return false;}
            if(!Floats(material,("_Surface",glass?1:0),("_Blend",0),("_BlendModePreserveSpecular",1),("_WorkflowMode",1),
                ("_SrcBlend",1),("_DstBlend",glass?10:0),("_SrcBlendAlpha",1),("_DstBlendAlpha",glass?10:0),
                ("_ZWrite",glass?0:1),("_Cull",2),("_AlphaClip",0),("_AlphaToMask",0),("_QueueOffset",0),
                ("_Metallic",glass?0:1),("_Smoothness",glass?RailArtContract.CoachGlassSmoothness:1),("_SmoothnessTextureChannel",0),
                ("_BumpScale",1),("_SpecularHighlights",1),("_EnvironmentReflections",1),("_ReceiveShadows",1),("_ClearCoatMask",0)))
            {reason="Coach surface/blend/PBR/depth/culling properties differ: "+expectedName;return false;}
            if(!material.HasProperty("_BaseColor")||!ColourIs(material.GetColor("_BaseColor"),glass?RailArtContract.CoachGlassTint:Color.white)
                ||!material.HasProperty("_EmissionColor")||!ColourIs(material.GetColor("_EmissionColor"),Color.black)
                ||material.renderQueue!=(glass?3000:2000)||material.GetTag("RenderType",false,"")!=(glass?"Transparent":"Opaque")
                ||material.GetShaderPassEnabled("ShadowCaster")==glass||material.GetShaderPassEnabled("DepthOnly")==glass)
            {reason="Coach tint/queue/pass contract differs: "+expectedName;return false;}
            bool keywords=glass?Keywords(material,"_SURFACE_TYPE_TRANSPARENT","_ALPHAPREMULTIPLY_ON")
                :Keywords(material,"_NORMALMAP","_METALLICSPECGLOSSMAP");
            if(!keywords){reason="Coach shader keywords differ: "+expectedName;return false;}
            return true;
        }
        public static bool CoachOpaqueContract(Material material,out string reason)
        {
            if(!CoachState(material,false,out reason))return false;
            long bytes=0;Texture2D baseMap=null;
            foreach(var definition in RailArtContract.CoachTextures)
            {
                var expected=Resources.Load<Texture2D>(definition.ResourcePath);
                var texture=material.GetTexture(definition.Property) as Texture2D;
                if(!expected||!texture||texture!=expected||texture.GetType()!=typeof(Texture2D)||texture.name!=definition.Name||texture.isReadable
                    ||texture.dimension!=TextureDimension.Tex2D||texture.width!=2048||texture.height!=2048||texture.mipmapCount!=RailArtContract.TextureMipCount
                    ||texture.format!=definition.Format||GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)!=definition.Srgb
                    ||texture.wrapMode!=TextureWrapMode.Clamp||texture.filterMode!=FilterMode.Trilinear||texture.anisoLevel!=4
                    ||Vector2.Distance(material.GetTextureScale(definition.Property),Vector2.one)>.0001f
                    ||Vector2.Distance(material.GetTextureOffset(definition.Property),Vector2.zero)>.0001f)
                {reason="Exact coach resource/map format, colour space, mip or sampling contract differs: "+definition.Name;return false;}
                if(definition.Property=="_BaseMap")baseMap=texture;
                bytes+=BlockMipBytes(texture);
            }
            foreach(string property in material.GetTexturePropertyNames())
            {
                var texture=material.GetTexture(property);if(!texture)continue;
                if(property=="_MainTex"&&texture==baseMap)continue;
                if(!RailArtContract.CoachTextures.Any(definition=>definition.Property==property))
                {reason="Unexpected additional coach map: "+property;return false;}
            }
            if(bytes>RailArtContract.CoachTextureBudgetBytes){reason="Coach compressed mip payload exceeds its17MiB budget.";return false;}
            return true;
        }
        public static bool CoachGlassContract(Material material,out string reason)
        {
            if(!CoachState(material,true,out reason))return false;
            if(material.GetTexturePropertyNames().Any(property=>material.GetTexture(property)))
            {reason="Thin coach glass must not reference opaque/normal/emissive/other maps.";return false;}
            return true;
        }
        public static bool CoachSeatContract(Transform coach,out string reason)
        {
            reason=null;var body=coach?coach.GetComponent<Rigidbody>():null;
            if(!coach||!coach.gameObject.activeInHierarchy||!body||!body.isKinematic||!body.detectCollisions||RailLayout.CoachSeatBoundsLocal.Length!=32)
            {reason="Actual kinematic coach and 32 precise seat-part shapes are required.";return false;}
            var seats=coach.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Northline coach solid seat ",StringComparison.Ordinal)).ToArray();
            if(seats.Length!=32){reason="Reachable coach seats need 32 real pan/back/support boxes.";return false;}
            for(int i=0;i<32;i++)
            {
                var expected=RailLayout.CoachSeatBoundsLocal[i];var seat=seats.SingleOrDefault(c=>c.name=="Northline coach solid seat "+i);
                if(!seat||seat.transform.parent!=coach||!seat.enabled||seat.isTrigger||!seat.gameObject.activeInHierarchy||seat.attachedRigidbody!=body
                    ||!Finite(seat.transform.localToWorldMatrix)||!Finite(seat.center)||!Finite(seat.size)||!Positive(seat.transform.localScale)
                    ||!Near(Mathf.Abs(Quaternion.Dot(seat.transform.localRotation,Quaternion.identity)),1)
                    ||!(Vector3.Distance(seat.transform.localPosition,expected.center)<=.0001f)||!(Vector3.Distance(seat.center,Vector3.zero)<=.0001f)
                    ||!(Vector3.Distance(Vector3.Scale(seat.size,seat.transform.localScale),expected.size)<=.0001f))
                {
                    reason="Actual coach seat collider/ownership/layout differs at index"+i
                        +" found="+(bool)seat+" parent="+(seat&&seat.transform.parent==coach)+" enabled="+(seat&&seat.enabled)
                        +" trigger="+(seat&&seat.isTrigger)+" active="+(seat&&seat.gameObject.activeInHierarchy)
                        +" body="+(seat&&seat.attachedRigidbody==body)
                        +" at="+(seat?seat.transform.localPosition.ToString("F6"):"missing")+" expectedAt="+expected.center.ToString("F6")
                        +" centre="+(seat?seat.center.ToString("F6"):"missing")+" size="+(seat?seat.size.ToString("F6"):"missing")
                        +" scale="+(seat?seat.transform.localScale.ToString("F6"):"missing")+" expectedSize="+expected.size.ToString("F6");
                    return false;
                }
            }
            return true;
        }
        public static bool ModelContract(GameObject prefab,RailArtContract.Model definition,out string reason)
        {
            reason=null;if(!prefab||!prefab.activeSelf){reason="Missing/inactive model "+definition.Name;return false;}
            foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                if(!component||!(component is Transform)&&!(component is MeshFilter)&&!(component is MeshRenderer))
                {reason="Unexpected imported physics/animation/script component in "+definition.Name;return false;}
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);var renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);
            if(filters.Length!=1||renderers.Length!=1||!renderers[0].enabled||!filters[0].gameObject.activeSelf||renderers[0].sharedMaterials.Length!=definition.SubmeshTriangles.Count)
            {reason="Exactly one active static mesh/renderer with its pinned slots is required for "+definition.Name;return false;}
            var mesh=filters[0].sharedMesh;
            if(!mesh||mesh.isReadable||!mesh.name.StartsWith(definition.Name,StringComparison.Ordinal)||mesh.vertexCount<=0||mesh.subMeshCount!=definition.SubmeshTriangles.Count
                ||!mesh.HasVertexAttribute(VertexAttribute.Normal)||mesh.GetVertexAttributeDimension(VertexAttribute.Normal)!=3
                ||!mesh.HasVertexAttribute(VertexAttribute.Tangent)||mesh.GetVertexAttributeDimension(VertexAttribute.Tangent)!=4||!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)||mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0)<2)
            {reason="Triangle/attribute/unreadable import contract failed for "+definition.Name;return false;}
            for(int i=0;i<definition.SubmeshTriangles.Count;i++)
                if(mesh.GetTopology(i)!=MeshTopology.Triangles||mesh.GetIndexCount(i)!=(uint)(definition.SubmeshTriangles[i]*3))
                {reason="Exact ordered submesh triangles differ for "+definition.Name+" slot"+i;return false;}
            Matrix4x4 matrix=prefab.transform.worldToLocalMatrix*filters[0].transform.localToWorldMatrix;
            if(!Finite(matrix)||matrix.determinant<=0){reason="Nonfinite/reflected model hierarchy in "+definition.Name;return false;}
            var bounds=TransformBounds(mesh.bounds,matrix);
            if(Vector3.Distance(bounds.min,definition.Min)>.001f||Vector3.Distance(bounds.max,definition.Max)>.001f)
            {reason="Metre bounds differ for "+definition.Name;return false;}
            return true;
        }
        bool TryLoad(HarborArt owner,out Dictionary<string,KitItem> kit,out string reason)
        {
            kit=new Dictionary<string,KitItem>();reason=null;
            var material=Resources.Load<Material>(RailArtContract.ResourceRoot+RailArtContract.MaterialName);
            if(!MaterialContract(material,out reason)||!MaterialContract(owner.Shared,out reason))return false;
            if(material.shader!=owner.Shared.shader||new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"}.Any(p=>material.GetTexture(p)!=owner.Shared.GetTexture(p)))
            {reason="Northline and existing owned surface must use the same original atlas objects.";return false;}
            var coachOpaque=Resources.Load<Material>(RailArtContract.CoachResourceRoot+RailArtContract.CoachOpaqueName);
            var coachGlass=Resources.Load<Material>(RailArtContract.CoachResourceRoot+RailArtContract.CoachGlassName);
            if(!CoachOpaqueContract(coachOpaque,out reason)||!CoachGlassContract(coachGlass,out reason))return false;
            foreach(var definition in RailArtContract.Models)
            {
                var prefab=Resources.Load<GameObject>(RailArtContract.ResourceRoot+definition.Name);
                if(!ModelContract(prefab,definition,out reason))return false;
                kit.Add(definition.Name,new KitItem{contract=definition,prefab=prefab,materials=definition.IsCoach?new[]{coachOpaque,coachGlass}:new[]{owner.Shared}});
            }
            return kit.Count==11;
        }
        public bool TryApply(HarborArt owner,IReadOnlyList<Target> targets)
        {
            if(Applied)return true;LastFailure="";
            if(!owner||owner.transform!=transform||targets==null||targets.Count!=21){LastFailure="Exact owned root and21 hero/control bindings are required.";return false;}
            if(!TryLoad(owner,out var kit,out string failure)){LastFailure=failure;return false;}
            var seen=new HashSet<MeshRenderer>();
            foreach(var target in targets)
            {
                if(target==null||!kit.ContainsKey(target.Asset)||!target.Parent||!target.Parent.IsChildOf(transform)||!target.Parent.gameObject.activeInHierarchy
                    ||!Positive(target.Parent.localScale)||!Finite(target.MetreOffset)||target.Originals==null||target.Originals.Length==0)
                {LastFailure="Invalid target or missing provisional renderer set.";return false;}
                foreach(var renderer in target.Originals)
                    if(!renderer||!renderer.transform.IsChildOf(target.Parent)||renderer.sharedMaterial!=owner.Shared||!seen.Add(renderer))
                    {LastFailure="A provisional renderer is missing, outside its target or bound twice.";return false;}
                // The only non-unit scale in this branch is the physical primitive itself. Ancestor scaling would change metre contracts.
                for(var ancestor=target.Parent.parent;ancestor;ancestor=ancestor.parent)
                {if(Vector3.Distance(ancestor.localScale,Vector3.one)>.0001f){LastFailure="Unexpected scaled ancestor outside the physical target.";return false;}if(ancestor==transform)break;}
            }
            if (!new HashSet<string>(targets.Select(t=>t.Asset)).SetEquals(kit.Keys)) { LastFailure="Every authored assembly must have its intended target."; return false; }
            var coaches=targets.Where(target=>target.Asset==RailArtContract.CoachName).ToArray();
            if(coaches.Length!=1){LastFailure="Exactly one actual coach target is required.";return false;}
            if(!CoachSeatContract(coaches[0].Parent,out failure)){LastFailure=failure;return false;}
            PhysicsBeforeSha256=PhysicalSignature(transform);
            try
            {
                foreach(var target in targets)
                {
                    var source=kit[target.Asset];var container=new GameObject("Northline authored "+target.Asset);container.SetActive(false);owned.Add(container);
                    container.transform.SetParent(target.Parent,false);
                    Vector3 inverse=new(1/target.Parent.localScale.x,1/target.Parent.localScale.y,1/target.Parent.localScale.z);
                    container.transform.localPosition=Vector3.Scale(target.MetreOffset,inverse);container.transform.localRotation=Quaternion.identity;container.transform.localScale=inverse;
                    var model=Instantiate(source.prefab,container.transform,false);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
                    foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                    {renderer.sharedMaterials=source.materials;renderer.enabled=true;renderer.receiveShadows=true;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;}
                    var filter=model.GetComponentInChildren<MeshFilter>(true);
                    var actual=TransformBounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);
                    var metreFrame=target.Parent.localToWorldMatrix*Matrix4x4.Scale(inverse)*Matrix4x4.Translate(target.MetreOffset);
                    var expected=TransformBounds(source.contract.Bounds,metreFrame);
                    float error=Mathf.Max(Vector3.Distance(actual.min,expected.min),Vector3.Distance(actual.max,expected.max));
                    if(error>.002f)throw new InvalidOperationException("Counter-scaled world bounds differ for "+target.Asset+": "+error);
                    receipts.Add(new BindingReceipt{model=target.Asset,parent=PathOf(target.Parent),originalRendererIds=target.Originals.Select(x=>x.GetInstanceID()).ToArray(),physicalParentScale=target.Parent.localScale,counterScale=inverse,metreOffset=target.MetreOffset,
                        expectedMin=expected.min,expectedMax=expected.max,actualMin=actual.min,actualMax=actual.max,boundsError=error,actualColliders=container.GetComponentsInChildren<Collider>(true).Length,actualBehaviours=container.GetComponentsInChildren<MonoBehaviour>(true).Length,
                        materialInstanceIds=source.materials.Select(value=>value.GetInstanceID()).ToArray(),materialNames=source.materials.Select(value=>value.name).ToArray(),
                        submeshIndexCounts=Enumerable.Range(0,filter.sharedMesh.subMeshCount).Select(index=>filter.sharedMesh.GetIndexCount(index)).ToArray()});
                }
                // All source models and every prospective instance are valid before the first original face changes.
                foreach(var target in targets)foreach(var renderer in target.Originals){originals.Add(renderer,renderer.enabled);renderer.enabled=false;}
                foreach(var item in owned)item.SetActive(true);
                PhysicsAfterSha256=PhysicalSignature(transform);
                if(PhysicsBeforeSha256!=PhysicsAfterSha256)throw new InvalidOperationException("Visual binding changed a physical/navigation/control contract.");
                Applied=true;return true;
            }
            catch(Exception error){LastFailure=error.Message;RestoreOriginals();DestroyOwned();receipts.Clear();return false;}
        }
        public static MeshRenderer[] CaptureOriginals(Transform parent,Material shared,params Transform[] except)
        {
            return parent.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial==shared&&!except.Any(x=>x&&r.transform.IsChildOf(x))).ToArray();
        }
        public static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
        {
            Bounds result=default;for(int i=0;i<32;i++)
            {var point=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(i==0)result=new Bounds(point,Vector3.zero);else result.Encapsulate(point);}return result;
        }
        static bool Positive(Vector3 value)=>Finite(value)&&value.x>.000001f&&value.y>.000001f&&value.z>.000001f;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool Finite(Matrix4x4 matrix){for(int i=0;i<16;i++)if(!Finite(matrix[i]))return false;return true;}
        static string PathOf(Transform value){var parts=new List<string>();for(var t=value;t;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        public static string PhysicalSignature(Transform root)
        {
            var text=new StringBuilder();var culture=CultureInfo.InvariantCulture;
            void V(Vector3 value){text.Append(value.x.ToString("R",culture)).Append(',').Append(value.y.ToString("R",culture)).Append(',').Append(value.z.ToString("R",culture));}
            void T(Component c){text.Append(c.GetType().FullName).Append('#').Append(c.GetInstanceID()).Append('|').Append(PathOf(c.transform)).Append('|');for(int i=0;i<16;i++)text.Append(c.transform.localToWorldMatrix[i].ToString("R",culture)).Append(',');}
            foreach(var collider in root.GetComponentsInChildren<Collider>(true).OrderBy(c=>c.GetInstanceID()))
            {
                T(collider);text.Append(collider.enabled).Append(collider.isTrigger).Append(collider.gameObject.activeSelf);
                if(collider is BoxCollider box){V(box.center);V(box.size);}else if(collider is MeshCollider mesh)text.Append(mesh.sharedMesh?mesh.sharedMesh.GetInstanceID():0).Append(mesh.convex);
            }
            foreach(var body in root.GetComponentsInChildren<Rigidbody>(true).OrderBy(c=>c.GetInstanceID())){T(body);text.Append(body.isKinematic).Append(body.detectCollisions).Append(body.useGravity).Append(body.interpolation);V(body.position);}
            foreach(var obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true).OrderBy(c=>c.GetInstanceID())){T(obstacle);text.Append(obstacle.enabled).Append(obstacle.carving).Append(obstacle.carveOnlyStationary).Append(obstacle.shape);V(obstacle.center);V(obstacle.size);}
            foreach(var control in root.GetComponentsInChildren<RailWorkControl>(true).OrderBy(c=>c.GetInstanceID())){T(control);text.Append(control.kind).Append(control.enabled);V(control.InteractPoint);}
            using var digest=SHA256.Create();return BitConverter.ToString(digest.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
        void RestoreOriginals(){foreach(var pair in originals)if(pair.Key)pair.Key.enabled=pair.Value;originals.Clear();Applied=false;}
        void DestroyOwned(){foreach(var item in owned)if(item){item.SetActive(false);Destroy(item);}owned.Clear();}
        void OnDestroy(){RestoreOriginals();DestroyOwned();}
    }
}
