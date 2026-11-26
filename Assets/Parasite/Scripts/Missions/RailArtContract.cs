using System;
using System.Collections.Generic;
using UnityEngine;
namespace Parasite.Missions
{
    /// <summary>Frozen eleven-model source contract; imported runtime data is checked before any provisional face is hidden.</summary>
    public static class RailArtContract
    {
        public const string ResourceRoot = "NorthlineRail/", MaterialName = "NorthlineSharedPBR";
        public const string LegacyAuthorManifestSha256 = "19ffc57f79c8a693339f863e1d90ce285625ec6215e9db8353f0c32237c3ed60";
        public const string AuthorManifestSha256 = "0de23f46b09641b5cfe94ff2fdb52bdaf9adb9f544e3359784b1ab51e915c5e1";
        public const string AuthorSourceDirectory = "ArtSource/NorthlineCoach";
        public const string CoachName = "RailDepartureCoach", CoachResourceRoot = ResourceRoot + "Coach/";
        public const string CoachOpaqueName = "RailCoachOpaquePBR", CoachGlassName = "RailCoachGlass";
        public const string CoachSeatLayoutSha256 = "8bdeef038f4ec4585187ab147baca473e650bc14f34d3c2b0a1536279cc039a9";
        public const int DistinctTriangles = 15972, CoachTriangles = 11144, CoachGlassTriangles = 120, TextureMipCount = 12;
        public const long CoachTextureBudgetBytes = 17L * 1024 * 1024;
        public static readonly Color CoachGlassTint = new(.87f, .94f, .96f, .18f);
        public const float CoachGlassSmoothness = .92f;
        public sealed class CoachTexture
        {
            public readonly string Name, Property, SourceSha256; public readonly long Bytes;
            public readonly TextureFormat Format; public readonly bool Srgb, NormalMap;
            public string ResourcePath => CoachResourceRoot + "Textures/" + Name;
            public CoachTexture(string name,string property,string sha,long bytes,TextureFormat format,bool srgb,bool normalMap)
            { Name=name;Property=property;SourceSha256=sha;Bytes=bytes;Format=format;Srgb=srgb;NormalMap=normalMap; }
        }
        public static readonly IReadOnlyList<CoachTexture> CoachTextures = Array.AsReadOnly(new[] {
            new CoachTexture("RailCoach_A","_BaseMap","dc8634e2507ef6e541c2ebdc2029ac09792bd480185080c005ad8060384a16bf",63918,TextureFormat.BC7,true,false),
            new CoachTexture("RailCoach_N","_BumpMap","db5cdd7330a964c330210802682b4793af7663f2c76d46a7465965bdf348dc02",86286,TextureFormat.BC5,false,true),
            new CoachTexture("RailCoach_M","_MetallicGlossMap","4b58d7d946ef46c905b1f4a78eed14bd61a3eba0f63090108c650ea4d4fd0e60",267499,TextureFormat.BC7,false,false),
        });
        public sealed class Model
        {
            public readonly string Name, SourceSha256; public readonly int Triangles; public readonly Vector3 Min, Max;
            public readonly IReadOnlyList<int> SubmeshTriangles;
            public bool IsCoach => Name == CoachName;
            public Model(string name,int triangles,Vector3 min,Vector3 max,string sourceSha,params int[] submeshTriangles)
            {
                Name=name;Triangles=triangles;Min=min;Max=max;SourceSha256=sourceSha;
                var counts=submeshTriangles.Length==0 ? new[]{triangles} : (int[])submeshTriangles.Clone();
                int total=0;foreach(int count in counts){if(count<=0)throw new ArgumentException("Positive submesh counts required");total+=count;}
                if(total!=triangles)throw new ArgumentException("Submesh counts must equal the pinned model total");
                SubmeshTriangles=Array.AsReadOnly(counts);
            }
            public Bounds Bounds => new((Min+Max)*.5f,Max-Min);
        }
        public static readonly IReadOnlyList<Model> Models = Array.AsReadOnly(new Model[]
        {
            new Model("RailTram",1724,new Vector3(-3f,0.0200000107f,-1.20000005f),new Vector3(3f,1.78999996f,1.20000005f),"8a525bfec240a0801d3b2ac36dad2201ea4385fb1e3db7610037ee26a907706a"),
            new Model("RailTransferCradle",736,new Vector3(-1.5f,0.0199999958f,-1f),new Vector3(1.5f,1.46499991f,1f),"9670653dbe6b4c5d9d037cb8dac335429c7184c560bbc290597775befea9ca0a"),
            new Model("RailControl",464,new Vector3(-0.349999994f,0f,-0.261999995f),new Vector3(0.349999994f,1.4375f,0.25f),"3fcd06215f7e7974055a8d73b643fcc9cbb84fd19ba1fac92706e8488c7fdad3"),
            new Model("RailLowSignal",236,new Vector3(-0.239999995f,0f,-0.200000003f),new Vector3(0.239999995f,0.790000021f,0.200000003f),"1358c817e91a9191554f46ce25db52406cfc303d7fd3b6ecf9586e62bcb70541"),
            new Model("RailPointBlade",264,new Vector3(-1.39999998f,-0.0450000018f,-0.0500000007f),new Vector3(1.39999998f,0.0450000018f,0.0500000007f),"44e0fdd769084c55cbe5aca61b4c19d07c679229613409aa4f3b02abab142783"),
            new Model("RailCrossingBarrier",364,new Vector3(-4f,-1.29999995f,-0.0925000012f),new Vector3(4f,1.29999995f,0.0700000003f),"661bcc7ddeaf6bf850d6b2863fe93a0efa8bf98ff35312cc808c0bb1a4aa3917"),
            new Model("RailServiceGate",236,new Vector3(-0.0899999961f,-0.5f,-0.699999988f),new Vector3(0.0700000003f,0.5f,0.699999988f),"17cb557af9810ffc9fe75a932967596489f5d491d7610d4555d6abaccf95e964"),
            new Model("RailPlatformGate",280,new Vector3(-0.150000006f,-1.5f,-2f),new Vector3(0.125f,1.5f,2f),"45953d537a0db33bf0dd8cbc26e444526531152e58de5fb34e2ac5885f19e35d"),
            new Model("RailPortalGate",368,new Vector3(-2.0999999f,-1.70000005f,-0.125f),new Vector3(2.0999999f,1.70000005f,0.100000001f),"5d6df3ae90de25a99443f8ef113e39f34de4e7a0c6678eb1f5b167655c11edaf"),
            new Model("RailDepartureCoach",11144,new Vector3(-1.602f,0.150000006f,-7.00950003f),new Vector3(1.602f,3.28999996f,7.00950003f),"9e1a0dcbbc15a61a9e0bf9962bab9918af01f2a21980cfc5550b57dcd9d0d70b",11024,120),
            new Model("RailBoardingRamp",156,new Vector3(0f,0.129999995f,-1f),new Vector3(3.3499999f,0.75f,1f),"b88c25cb431e4263ed7e2198ba423ebfee0fb4655ac0fd22a666b12519a14e3d"),
        });
    }
}
