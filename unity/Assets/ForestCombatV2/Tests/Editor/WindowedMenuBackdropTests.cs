using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Presentation.Unity;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Stage1VillageExperiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class WindowedMenuBackdropTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static int clicks;
        static void Clicked()=>clicks++;
        static Type RuntimeType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Game."+name)).First(t=>t!=null);
        static object Field(Component c,string name)=>c.GetType().GetField(name,Hidden).GetValue(c);
        static void Call(Component c,string name,params object[] args)=>c.GetType().GetMethods(Hidden|BindingFlags.Public).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(c,args);
        [TestCase(960,600,0,30,960,540)] [TestCase(1200,750,120,105,960,540)]
        [TestCase(1280,720,160,90,960,540)] [TestCase(1440,900,240,180,960,540)]
        [TestCase(1920,1080,0,0,1920,1080)] [TestCase(2222,922,631,191,960,540)]
        public void MenusUseTheExistingGameFrameInsteadOfExpandingTheBackground(int width,int height,int x,int y,int w,int h)
        {
            var expected=new Rect(x,y,w,h);
            Assert.That(VillagePixelViewport.CalculateDisplayRect(width,height),Is.EqualTo(expected));
            Assert.That(PixelGameViewport.UiRect(width,height),Is.EqualTo(expected));
            Assert.That(SketchRewardChoiceScreen.Viewport(width,height),Is.EqualTo(expected),"reward graphics and pointer mapping share the uGUI frame");
        }
        [TestCase(701,499)] [TestCase(959,539)] [TestCase(961,541)]
        public void OddAndSubNativeFramesContainTheWholeMenu(int width,int height)
        {
            var world=VillagePixelViewport.CalculateDisplayRect(width,height);var menu=PixelGameViewport.UiRect(width,height);
            Assert.That(menu.xMin,Is.GreaterThanOrEqualTo(world.xMin-.001f));Assert.That(menu.yMin,Is.GreaterThanOrEqualTo(world.yMin-.001f));
            Assert.That(menu.xMax,Is.LessThanOrEqualTo(world.xMax+.001f));Assert.That(menu.yMax,Is.LessThanOrEqualTo(world.yMax+.001f));
            Assert.That(menu.width/menu.height,Is.EqualTo(16f/9).Within(.0001f));Assert.That(menu.center,Is.EqualTo(world.center));
        }
        [UnityTearDown] public IEnumerator LeavePlay(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest] public IEnumerator BagFitsTheUnchangedWorldAndItsButtonRemainsClickableAcrossResize()
        {
            EditorSceneManager.playModeStartScene=null;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Windowed UI test camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;
            new GameObject("Windowed UI test light").AddComponent<Light>().type=LightType.Directional;
            var root=new GameObject("Native world fixture");var viewport=root.AddComponent<VillagePixelViewport>();viewport.sourceCamera=camera;
            root.AddComponent(RuntimeType("TemporaryGuiWorldOutput"));
            yield return new EnterPlayMode();yield return null;yield return null;
            viewport=Object.FindAnyObjectByType<VillagePixelViewport>();Assert.That(viewport.Ready,Is.True);
            var output=viewport.GetComponent(RuntimeType("TemporaryGuiWorldOutput"));
            var gui=new GameObject("Actual shared bag presentation",typeof(RectTransform)).AddComponent(RuntimeType("TemporaryGameGui"));
            Call(gui,"BuildRoot");Call(gui,"InitJournal");Call(gui,"BuildApprovedSketchBook");
            var bag=(GameObject)Field(gui,"bag");var dim=(GameObject)Field(gui,"dim");bag.SetActive(true);dim.SetActive(true);
            Assert.That(bag,Is.Not.Null);Assert.That(dim,Is.Not.Null);
            var buttonObject=new GameObject("Viewport regression button",typeof(RectTransform));
            buttonObject.transform.SetParent(bag.transform,false);
            buttonObject.AddComponent<Image>();buttonObject.AddComponent<Button>();
            var buttonRect=buttonObject.GetComponent<RectTransform>();buttonRect.anchorMin=buttonRect.anchorMax=buttonRect.pivot=new Vector2(0,1);
            buttonRect.anchoredPosition=new Vector2(850,-460);buttonRect.sizeDelta=new Vector2(90,50);
            clicks=0;buttonObject.GetComponent<Button>().onClick.AddListener(Clicked);
            var editor=typeof(EditorWindow).Assembly;var view=EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));view.Show();view.Focus();
            var selected=view.GetType().GetProperty("selectedSizeIndex",Hidden|BindingFlags.Public);int original=(int)selected.GetValue(view);
            var sizesType=editor.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            int added=0;
            try
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1200,750),new Vector2Int(701,499)})
                {
                    var spec=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size.x,size.y,"Windowed backdrop regression");
                    group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{spec});added++;
                    selected.SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);
                    yield return null;yield return null;Call(gui,"FitToWorld");Call(gui,"RefreshScreenBackdrop");yield return null;yield return null;
                    Assert.That(Screen.width,Is.EqualTo(size.x));Assert.That(Screen.height,Is.EqualTo(size.y));
                    Canvas.ForceUpdateCanvases();
                    var clip=(RectTransform)Field(output,"clip");var world=viewport.DisplayRect;var frame=PixelGameViewport.UiRect(size.x,size.y);
                    Assert.That(clip.sizeDelta,Is.EqualTo(world.size));Assert.That(clip.anchoredPosition,Is.EqualTo(new Vector2(world.x,-world.y)));
                    var corners=new Vector3[4];((RectTransform)bag.transform).GetWorldCorners(corners);
                    Assert.That(corners[0].x,Is.EqualTo(frame.xMin).Within(1));Assert.That(corners[0].y,Is.EqualTo(size.y-frame.yMax).Within(1));
                    Assert.That(corners[2].x,Is.EqualTo(frame.xMax).Within(1));Assert.That(corners[2].y,Is.EqualTo(size.y-frame.yMin).Within(1));
                    buttonRect.GetWorldCorners(corners);var point=(corners[0]+corners[2])*.5f;
                    var pointer=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();
                    gui.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    Assert.That(hits.First().gameObject,Is.EqualTo(buttonObject),"rendered button and pointer hit rectangle agree after resize");
                    ExecuteEvents.Execute(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);Assert.That(clicks,Is.EqualTo(added));
                    pointer.position=Vector2.zero;hits.Clear();gui.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    foreach(var hit in hits)Assert.That(hit.gameObject,Is.Not.EqualTo(buttonObject));
                    string folder=Path.GetFullPath("../outputs/windowed-menu-regression");Directory.CreateDirectory(folder);
                    string path=Path.Combine(folder,"ui-fit-"+size.x+"x"+size.y+"-"+Guid.NewGuid().ToString("N")+".png");ScreenCapture.CaptureScreenshot(path);
                    for(int i=0;i<60&&!File.Exists(path);i++)yield return null;
                    Assert.That(File.Exists(path),Is.True,"native Game View capture completed");
                    var image=new Texture2D(2,2);ImageConversion.LoadImage(image,File.ReadAllBytes(path));
                    var border=image.GetPixel(Mathf.RoundToInt(frame.x+frame.width*.03f),size.y/2);var centre=image.GetPixel(size.x/2,size.y/2);
                    Assert.That(Mathf.Abs(border.r-centre.r)+Mathf.Abs(border.g-centre.g)+Mathf.Abs(border.b-centre.b),Is.LessThan(.03f),"no brighter world rectangle inside the menu");
                    if(world.xMin>2)Assert.That(image.GetPixel((int)world.xMin-2,size.y/2).grayscale,Is.LessThan(.01f),"opening the bag never enlarges the world into its letterbox");
                    Object.Destroy(image);
                }
                bag.SetActive(false);dim.SetActive(false);Call(gui,"RefreshScreenBackdrop");yield return null;yield return null;
                var gameplay=viewport.DisplayRect;Assert.That(((RectTransform)Field(output,"clip")).sizeDelta,Is.EqualTo(gameplay.size));
                bag.SetActive(true);dim.SetActive(true);Call(gui,"RefreshScreenBackdrop");gui.gameObject.SetActive(false);
                yield return null;Assert.That(((RectTransform)Field(output,"clip")).sizeDelta,Is.EqualTo(gameplay.size),"disabling a menu never changes the background size");
            }
            finally
            {
                selected.SetValue(view,original);
                for(int i=0;i<added;i++){int n=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{n});}
            }
        }
    }
}
