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
        }
        sealed class KitItem { public RailArtContract.Model contract; public GameObject prefab; }
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
        public static bool ModelContract(GameObject prefab,RailArtContract.Model definition,out string reason)
        {
            reason=null;if(!prefab||!prefab.activeSelf){reason="Missing/inactive model "+definition.Name;return false;}
            foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                if(!component||!(component is Transform)&&!(component is MeshFilter)&&!(component is MeshRenderer))
                {reason="Unexpected imported physics/animation/script component in "+definition.Name;return false;}
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);var renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);
            if(filters.Length!=1||renderers.Length!=1||!renderers[0].enabled||!filters[0].gameObject.activeSelf||renderers[0].sharedMaterials.Length!=1)
            {reason="Exactly one active static mesh/renderer/slot is required for "+definition.Name;return false;}
            var mesh=filters[0].sharedMesh;
            if(!mesh||mesh.isReadable||!mesh.name.StartsWith(definition.Name,StringComparison.Ordinal)||mesh.vertexCount<=0||mesh.subMeshCount!=1||mesh.GetTopology(0)!=MeshTopology.Triangles
                ||mesh.GetIndexCount(0)!=(uint)(definition.Triangles*3)||!mesh.HasVertexAttribute(VertexAttribute.Normal)||mesh.GetVertexAttributeDimension(VertexAttribute.Normal)!=3
                ||!mesh.HasVertexAttribute(VertexAttribute.Tangent)||mesh.GetVertexAttributeDimension(VertexAttribute.Tangent)!=4||!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)||mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0)<2)
            {reason="Triangle/attribute/unreadable import contract failed for "+definition.Name;return false;}
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
            foreach(var definition in RailArtContract.Models)
            {
                var prefab=Resources.Load<GameObject>(RailArtContract.ResourceRoot+definition.Name);
                if(!ModelContract(prefab,definition,out reason))return false;
                kit.Add(definition.Name,new KitItem{contract=definition,prefab=prefab});
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
                    {renderer.sharedMaterials=new[]{owner.Shared};renderer.enabled=true;renderer.receiveShadows=true;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;}
                    var filter=model.GetComponentInChildren<MeshFilter>(true);
                    var actual=TransformBounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);
                    var metreFrame=target.Parent.localToWorldMatrix*Matrix4x4.Scale(inverse)*Matrix4x4.Translate(target.MetreOffset);
                    var expected=TransformBounds(source.contract.Bounds,metreFrame);
                    float error=Mathf.Max(Vector3.Distance(actual.min,expected.min),Vector3.Distance(actual.max,expected.max));
                    if(error>.002f)throw new InvalidOperationException("Counter-scaled world bounds differ for "+target.Asset+": "+error);
                    receipts.Add(new BindingReceipt{model=target.Asset,parent=PathOf(target.Parent),originalRendererIds=target.Originals.Select(x=>x.GetInstanceID()).ToArray(),physicalParentScale=target.Parent.localScale,counterScale=inverse,metreOffset=target.MetreOffset,
                        expectedMin=expected.min,expectedMax=expected.max,actualMin=actual.min,actualMax=actual.max,boundsError=error,actualColliders=container.GetComponentsInChildren<Collider>(true).Length,actualBehaviours=container.GetComponentsInChildren<MonoBehaviour>(true).Length});
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
            Bounds result=default;for(int i=0;i<8;i++)
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
