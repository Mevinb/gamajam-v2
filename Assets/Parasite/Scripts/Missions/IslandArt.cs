using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Missions
{
    /// <summary>Original Blender island assemblies with separate, reviewed simple navigation collision recipes.</summary>
    public sealed class IslandArt : MonoBehaviour
    {
        [Serializable] public class BoxRecipe { public string name; public float[] center, size; }
        [Serializable] public class Group { public string asset; public BoxRecipe[] boxes; }
        [Serializable] public class Recipes { public Group[] groups; }
        readonly Dictionary<string, Material> materials = new();
        readonly List<Material> owned = new();
        readonly List<Mesh> ownedMeshes = new();
        Recipes recipes;

        public Material Surface(string name)
        {
            if (materials.TryGetValue(name, out var material)) return material;
            string texture = null; Color color = Color.white; float smooth = .35f, metal = 0;
            if (name.Contains("Concrete")) { texture = "IslandConcrete"; smooth = .12f; }
            else if (name.Contains("Metal")) { texture = "IslandMetal"; smooth = .55f; metal = .75f; }
            else if (name.Contains("Oxide")) { texture = "IslandOxide"; smooth = .23f; metal = .4f; }
            else if (name.Contains("Clinic")) { texture = "IslandClinic"; smooth = .5f; }
            else if (name.Contains("Timber")) { texture = "IslandTimber"; smooth = .23f; }
            else if (name.Contains("Fabric")) { texture = "IslandFabric"; smooth = .12f; }
            else if (name.Contains("Glass")) { color = new Color(.08f,.15f,.18f); smooth = .95f; metal = .2f; }
            else if (name.Contains("Safety")) color = new Color(.8f,.56f,.12f);
            else if (name.Contains("Signal")) color = new Color(.12f,.65f,.43f);
            else color = new Color(.05f,.065f,.07f);
            material = MatFactory.LitUnique(color,smooth,metal); material.name = name + " island PBR";
            if (texture != null)
            {
                var albedo = Resources.Load<Texture2D>("Island/Textures/" + texture + "_A");
                var normal = Resources.Load<Texture2D>("Island/Textures/" + texture + "_N");
                var mask = Resources.Load<Texture2D>("Island/Textures/" + texture + "_M");
                if (!albedo || !normal || !mask) throw new InvalidOperationException("Missing island PBR surface " + texture);
                material.SetTexture("_BaseMap",albedo); material.SetTexture("_BumpMap",normal); material.SetFloat("_BumpScale",.6f); material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap",mask); material.SetFloat("_Smoothness",1); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (name.Contains("Signal")) MatFactory.SetEmission(material,color*1.5f);
            materials.Add(name,material); owned.Add(material); return material;
        }

        public GameObject Model(string asset, Vector3 localPosition, float yaw = 0, bool colliders = true)
        {
            var prefab = Resources.Load<GameObject>("Island/" + asset);
            if (!prefab) throw new InvalidOperationException("Missing island Blender assembly " + asset);
            var instance = Instantiate(prefab,transform,false); instance.name=asset+" (Blender)";
            instance.transform.localPosition=localPosition; instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++) slots[i]=Surface(slots[i]?slots[i].name:"IslandMetal");
                renderer.sharedMaterials=slots; renderer.receiveShadows=true; renderer.shadowCastingMode=ShadowCastingMode.On;
            }
            if (colliders) AddCollision(asset,instance.transform);
            return instance;
        }

        void AddCollision(string asset,Transform parent)
        {
            if (recipes==null)
            {
                var data=Resources.Load<TextAsset>("Island/collision_recipes");
                if(!data) throw new InvalidOperationException("Island collider recipes are missing.");
                recipes=JsonUtility.FromJson<Recipes>(data.text);
            }
            foreach(var group in recipes.groups)
            {
                if(group.asset!=asset)continue;
                foreach(var recipe in group.boxes)
                {
                    var go=new GameObject(recipe.name);go.transform.SetParent(parent,false);
                    go.transform.localPosition=Vector(recipe.center);
                    go.AddComponent<BoxCollider>().size=Vector(recipe.size);
                }
                break;
            }
        }
        public void ProjectBoxUV(GameObject box,float metresPerTile=1)
        {
            var filter=box.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)return;
            var mesh=Instantiate(filter.sharedMesh);mesh.name=box.name+" metre UVs";
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];var scale=box.transform.localScale;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=Vector3.Scale(vertices[i],scale);var n=normals[i];
                uv[i]=(Mathf.Abs(n.x)>.5f?new Vector2(n.x>0?-p.z:p.z,p.y):Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z):new Vector2(n.z>0?p.x:-p.x,p.y))/metresPerTile;
            }
            mesh.uv=uv;mesh.RecalculateTangents();filter.sharedMesh=mesh;ownedMeshes.Add(mesh);
        }
        static Vector3 Vector(float[] p) => new(p[0],p[1],p[2]);
        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)if(mesh)Destroy(mesh);
            foreach(var material in owned)if(material)Destroy(material);
        }
    }
}
