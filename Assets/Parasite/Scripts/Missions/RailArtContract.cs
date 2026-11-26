using System.Collections.Generic;
using UnityEngine;
namespace Parasite.Missions
{
    /// <summary>Frozen eleven-model source contract; imported runtime data is checked before any provisional face is hidden.</summary>
    public static class RailArtContract
    {
        public const string ResourceRoot = "NorthlineRail/", MaterialName = "NorthlineSharedPBR";
        public const string AuthorManifestSha256 = "19ffc57f79c8a693339f863e1d90ce285625ec6215e9db8353f0c32237c3ed60";
        public const int DistinctTriangles = 7336;
        public sealed class Model
        {
            public readonly string Name, SourceSha256; public readonly int Triangles; public readonly Vector3 Min, Max;
            public Model(string name,int triangles,Vector3 min,Vector3 max,string sourceSha) { Name=name;Triangles=triangles;Min=min;Max=max;SourceSha256=sourceSha; }
            public Bounds Bounds => new((Min+Max)*.5f,Max-Min);
        }
        public static readonly IReadOnlyList<Model> Models = new Model[]
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
            new Model("RailDepartureCoach",2508,new Vector3(-1.602f,0.150000006f,-7.00950003f),new Vector3(1.602f,3.28999996f,7.00950003f),"9617e03537fbd206776dca703c32b0f57cf779ac2709ad51f07c506b5d287cc0"),
            new Model("RailBoardingRamp",156,new Vector3(0f,0.129999995f,-1f),new Vector3(3.3499999f,0.75f,1f),"b88c25cb431e4263ed7e2198ba423ebfee0fb4655ac0fd22a666b12519a14e3d"),
        };
    }
}
