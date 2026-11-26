using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Chapter2.Tests
{
    public sealed class RoseBlueReferenceImageTests
    {
        [UnityTearDown] public IEnumerator LeavePlay(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator ChapterTwoStartupFailureReachesTheLoadingFailureScreen()
        {
            EditorSceneManager.playModeStartScene=null;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Failure verification camera").AddComponent<Camera>();
            new GameObject("Failure verification light").AddComponent<Light>().type=LightType.Directional;
            yield return new EnterPlayMode();
            var holder=new GameObject("Loading failure fixture");holder.SetActive(false);
            var fade=holder.AddComponent<ForestCombatV2.ChapterSceneFade>();
            typeof(ForestCombatV2.ChapterSceneFade).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,fade);
            LogAssert.Expect(LogType.Warning,"Scene loading failed: Chapter2 scene bindings missing; run Tools/deviant/Chapter2.");
            LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("InvalidOperationException: Chapter2 scene bindings missing"));
            var world=new GameObject("Unbound chapter two").AddComponent<Chapter2World>();
            yield return null;yield return null;
            Assert.That(world.Ready,Is.False);Assert.That(ForestCombatV2.ChapterSceneFade.ShowingFailure,Is.True);
        }
        [TestCase("rose",TextureFormat.RGBA32)] [TestCase("blue",TextureFormat.RGBA32)]
        [TestCase("rose",TextureFormat.DXT1)] [TestCase("blue",TextureFormat.DXT1)]
        [TestCase("rose",TextureFormat.DXT5)] [TestCase("blue",TextureFormat.DXT5)]
        [TestCase("rose",TextureFormat.BC7)] [TestCase("blue",TextureFormat.BC7)]
        public void BackgroundRemovalWritesAnRgbaCopyAndPreservesTheCompressedSource(string name,TextureFormat format)
        {
            if(!SystemInfo.SupportsTextureFormat(format))Assert.Ignore("GPU format unavailable: "+format);
            var source=new Texture2D(640,640,TextureFormat.RGBA32,false);Sprite sprite=null;
            try
            {
                var pixels=Enumerable.Repeat(new Color32(255,255,255,255),640*640).ToArray();
                for(int y=250;y<330;y++)for(int x=250;x<330;x++)pixels[y*640+x]=new Color32(180,60,100,255);
                source.SetPixels32(pixels);source.Apply();
                if(format!=TextureFormat.RGBA32)EditorUtility.CompressTexture(source,format,TextureCompressionQuality.Normal);
                var original=source.GetPixels32();
                sprite=(Sprite)typeof(RoseBlueEncounter).GetMethod("CreateReferenceSprite",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source,name});
                Assert.That(source.format,Is.EqualTo(format));Assert.That(source.GetPixels32(),Is.EqualTo(original),"background removal must not modify the decoded source");
                Assert.That(sprite.texture.format,Is.EqualTo(TextureFormat.RGBA32));Assert.That(sprite.texture.isReadable,Is.False,"release the finished CPU copy");
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.rect,Is.EqualTo(name=="rose"?new Rect(150,52,468,408):new Rect(220,2,328,508)));
                var target=RenderTexture.GetTemporary(640,640,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
                var read=new Texture2D(640,640,TextureFormat.RGBA32,false);
                try
                {
                    Graphics.Blit(sprite.texture,target);RenderTexture.active=target;read.ReadPixels(new Rect(0,0,640,640),0,0);read.Apply();
                    var result=read.GetPixels32();Assert.That(result[0].a,Is.Zero,"white border must be transparent");
                    Assert.That(result[280*640+280].a,Is.EqualTo(255));Assert.That(result[280*640+280].r,Is.GreaterThan(result[280*640+280].g),"keep the coloured body");
                }
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(read);}
            }
            finally{if(sprite){Object.DestroyImmediate(sprite.texture);Object.DestroyImmediate(sprite);}Object.DestroyImmediate(source);}
        }
    }
}
