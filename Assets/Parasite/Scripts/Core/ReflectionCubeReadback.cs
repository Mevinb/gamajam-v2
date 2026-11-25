using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Parasite
{
    /// <summary>GPU samples any supported cube format, including BC6H, then reads only an uncompressed half-float target.</summary>
    public sealed class ReflectionCubeReadback : IDisposable
    {
        Material material;
        RenderTexture target;
        Texture2D pixels;
        CommandBuffer commands;
        readonly Texture cube;
        readonly bool fullFace;
        readonly int size;
        public ReflectionCubeReadback(Texture source,Vector4 decode,int size=4,bool fullFace=false)
        {
            if(!source||source.dimension!=TextureDimension.Cube)throw new ArgumentException("A valid cubemap is required.");
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Cubemap image validation requires an actual GPU.");
            cube=source;this.size=size;this.fullFace=fullFace;
            try
            {
                var shader=Resources.Load<Shader>("Shaders/ReflectionCubeReadback");
                if(!shader)throw new InvalidOperationException("Cubemap readback shader is missing.");
                material=new Material(shader){hideFlags=HideFlags.DontSave};material.SetTexture("_ReflectionCube",cube);material.SetVector("_CubeHDR",decode);
                target=new RenderTexture(size,size,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){hideFlags=HideFlags.DontSave};
                if(!target.Create())throw new InvalidOperationException("Uncompressed cubemap readback target allocation failed.");
                pixels=new Texture2D(size,size,TextureFormat.RGBAHalf,false,true){hideFlags=HideFlags.DontSave};
                commands=new CommandBuffer{name="Reflection cubemap validation samples"};
            }
            catch{Dispose();throw;}
        }
        public Color[] Read(CubemapFace face,int mip)
        {
            if(!material||!target||!pixels)throw new ObjectDisposedException(nameof(ReflectionCubeReadback));
            var old=RenderTexture.active;
            try
            {
                material.SetFloat("_Face",(int)face);material.SetFloat("_Mip",mip);
                material.SetFloat("_SampleScale",fullFace?1f:Mathf.Min(1f,(float)size/Mathf.Max(1,cube.width>>mip)));
                commands.Clear();commands.SetRenderTarget(target);commands.SetViewport(new Rect(0,0,size,size));
                commands.ClearRenderTarget(false,true,Color.black);
                commands.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,size,size),0,0,false);pixels.Apply(false);
                return pixels.GetPixels();
            }
            finally{RenderTexture.active=old;}
        }
        public void Dispose()
        {
            commands?.Release();commands=null;
            DestroyOwned(material);material=null;
            if(target){target.Release();DestroyOwned(target);}target=null;
            DestroyOwned(pixels);pixels=null;
        }
        static void DestroyOwned(Object value){if(!value)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
