using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.UI.Theme
{
    /// <summary>Approved handoff v3, loaded from the pinned resource deployment. No local paths or preview state.</summary>
    public static class ApprovedTheme
    {
        // Screen footer hints share the inventory baseline in the 960 x 540 design viewport.
        public const int ControlHintFontSize = 10;
        public const float ControlHintHeight = 18.7f;

        public static Text ControlHint(Text label)
        {
            label.font = Font ?? label.font;
            label.fontSize = ControlHintFontSize;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ControlHintHeight);
            return label;
        }

        [Serializable] public sealed class SpriteRecord { public string id,file; public float[] view,rect; }
        [Serializable] public sealed class Mark { public float[] center; public float size; }
        [Serializable] public sealed class InkRecord { public string id,name,color,bottle,effect,condition,description; public float[] view; public Mark label,badge; }
        [Serializable] sealed class Package { public int version; public SpriteRecord[] sprites; public InkRecord[] inks; }
        [Serializable] sealed class RuntimeInks { public RuntimeInk[] inks; }
        [Serializable] sealed class RuntimeInk { public string id,name,description,effect; }
        static Package package;
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,SpriteRecord> records=new Dictionary<string,SpriteRecord>();
        static readonly Dictionary<string,InkRecord> inks=new Dictionary<string,InkRecord>();
        static Font font;
        // Dominant opaque eye pixel from ApprovedCombatHUD/sprites/hero-portrait.png (sRGB).
        public static readonly Color CharacterEye = new Color32(254,196,3,255);
        public static readonly Color Paper=new Color32(243,239,229,255), Muted=new Color32(174,182,192,255), Accent=new Color32(165,204,215,255), Gold=new Color32(230,205,125,255), Surface=new Color32(36,39,43,246);
        // All menu veils share the augment screen backdrop; panel surfaces stay separate.
        public static readonly Color MenuBackdrop=new Color32(36,39,43,244);
        public static readonly Color NavigationBackdrop=MenuBackdrop;
        public static readonly Color PauseBackdrop=MenuBackdrop;
        public static readonly Color ConfirmationBackdrop=MenuBackdrop;
        static ApprovedTheme() { UnityEngine.Font.textureRebuilt += OnFontTextureRebuilt; }
        public static Font Font
        {
            get
            {
                if(!font)font=Resources.Load<Font>("ApprovedTheme/Fonts/MemomentKkukkukk");
                SmoothFont(font);return font;
            }
        }
        // This is a smooth outline font; bitmap sprites and Galmuri keep their pixel sampling.
        public static bool ConfigureFontSampling(Font candidate)
        {
            if(!candidate||candidate!=Font)return false;
            SmoothFont(candidate);return true;
        }
        static void OnFontTextureRebuilt(Font rebuilt) { if(rebuilt==font)SmoothFont(rebuilt); }
        static void SmoothFont(Font value)
        {
            if(value&&value.material&&value.material.mainTexture)
                value.material.mainTexture.filterMode=FilterMode.Bilinear;
        }
        public static bool Ready { get { Load();return package!=null&&Font; } }
        static void Load()
        {
            if(package!=null)return;
            var asset=Resources.Load<TextAsset>("ApprovedTheme/manifest");if(!asset)return;
            var parsed=JsonUtility.FromJson<Package>(asset.text);if(parsed==null||parsed.version!=3)throw new InvalidOperationException("Approved UI theme v3 is required.");
            package=parsed;foreach(var s in package.sprites)records[s.id]=s;foreach(var i in package.inks)inks[i.id]=i;
            // New gameplay inks reuse approved bottle/condition geometry and the existing attack-speed mark.
            // Their names and text still come from the current generated gameplay catalog.
            var runtime=Resources.Load<TextAsset>("RunInks");
            if(runtime)foreach(var ink in JsonUtility.FromJson<RuntimeInks>(runtime.text).inks)
            {
                if(ink.effect!="basic_attack_speed_increase"||inks.ContainsKey(ink.id))continue;
                string reference=ink.id.Replace("basic_attack_speed_increase","basic_attack_damage_increase");
                if(!inks.TryGetValue(reference,out var basis))continue;
                inks[ink.id]=new InkRecord { id=ink.id,name=ink.name,description=ink.description,color=basis.color,
                    bottle=basis.bottle,effect="ui-assets-v1/attackSpeed",condition=basis.condition,
                    view=basis.view,label=basis.label,badge=basis.badge };
            }
        }
        public static SpriteRecord Find(string id){Load();return id!=null&&records.TryGetValue(id,out var value)?value:null;}
        public static InkRecord FindInk(string id){Load();return id!=null&&inks.TryGetValue(id,out var value)?value:null;}
        // Sources drawn directly as sprites (frames) stay loaded. Sources that are only cropped are "working" sources: their
        // CPU pixels are read once and kept while a screen is being built, then released after a short idle, so the Web
        // build's WebAssembly heap (which never shrinks) does not keep every atlas the player has ever seen.
        static readonly Dictionary<string,Texture2D> working=new Dictionary<string,Texture2D>();
        public const float WorkingIdleSeconds=2f;
        static float lastWork;
        static Texture2D Source(string file)
        {
            string key="source:"+file;if(textures.TryGetValue(key,out var t))return t;
            if(working.TryGetValue(file,out t)&&t)working.Remove(file);else t=LoadSource(file);
            if(t)textures[key]=t;return t;
        }
        static Texture2D LoadSource(string file)
        {
            var t=Resources.Load<Texture2D>("ApprovedTheme/"+file.Substring(0,file.LastIndexOf('.')));
            if(t){t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;}return t;
        }
        static Texture2D WorkingSource(string file)
        {
            KeepWorking();
            if(textures.TryGetValue("source:"+file,out var pinned)&&pinned)return pinned;
            if(working.TryGetValue(file,out var t)&&t)return t;
            t=LoadSource(file);if(t)working[file]=t;return t;
        }
        static void KeepWorking()
        {
            if(!Application.isPlaying)return;
            lastWork=Time.unscaledTime;WorkingRelease.Ensure();
        }
        /// <summary>Drops the cropped-only sources and every source pixel copy. Finished art, sprites and frames stay.</summary>
        public static void ReleaseWorkingSources()
        {
            foreach(var t in working.Values)if(t)Resources.UnloadAsset(t);
            working.Clear();sourcePixels.Clear();
        }
        public static int WorkingSourceCount=>working.Count;
        public static int SourcePixelCopyCount=>sourcePixels.Count;
        sealed class WorkingRelease : MonoBehaviour
        {
            static WorkingRelease instance;
            public static void Ensure()
            {
                if(instance)return;
                var go=new GameObject("Approved theme source release"){hideFlags=HideFlags.HideAndDontSave};
                DontDestroyOnLoad(go);instance=go.AddComponent<WorkingRelease>();
            }
            void LateUpdate()
            {
                if((working.Count>0||sourcePixels.Count>0)&&Time.unscaledTime-lastWork>=WorkingIdleSeconds)ReleaseWorkingSources();
            }
        }
        // Finished art is only drawn, so its CPU copy is dropped on upload. TryReadPixels rebuilds the same pixels.
        static readonly Dictionary<Texture2D,string> recipes=new Dictionary<Texture2D,string>();
        static Texture2D Finish(string key,int width,int height,Color[] pixels)
        {
            var t=new Texture2D(width,height,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            t.SetPixels(pixels);t.Apply(false,true);textures[key]=t;recipes[t]=key;return t;
        }
        /// <summary>Pixels (GetPixels order) of a crop, ink or palette made here; false for any other texture.</summary>
        public static bool TryReadPixels(Texture2D texture,out Color[] pixels)
        {
            pixels=null;if(!texture||!recipes.TryGetValue(texture,out var key))return false;
            if(key.StartsWith("ink:",StringComparison.Ordinal))pixels=InkPixels(key.Substring(4),out _,out _);
            else if(key.StartsWith("palette:",StringComparison.Ordinal))
            {
                int split=key.IndexOf(':',8);
                pixels=PalettePixels(key.Substring(8,split-8),split+1<key.Length?key.Substring(split+1):null);
            }
            else pixels=CropPixels(key,out _,out _);
            // Match Finish's RGBA32 upload before callers filter these pixels (for example the shelf).
            if(pixels!=null&&(key.StartsWith("ink:",StringComparison.Ordinal)||key.StartsWith("palette:",StringComparison.Ordinal)))
                for(int i=0;i<pixels.Length;i++)
                {
                    var c=pixels[i];pixels[i]=new Color32(Rgba32Channel(c.r),Rgba32Channel(c.g),Rgba32Channel(c.b),Rgba32Channel(c.a));
                }
            return pixels!=null&&pixels.Length==texture.width*texture.height;
        }
        // Native RGBA32 upload rounds half up; Color32's float cast uses ties-to-even instead.
        static byte Rgba32Channel(float value)=>(byte)Mathf.FloorToInt(Mathf.Clamp01(value)*255f+.5f);
        public static Texture2D Texture(string id)
        {
            if(id==null)return null;if(textures.TryGetValue(id,out var cached))return cached;
            var pixels=CropPixels(id,out int w,out int h);return pixels==null?null:Finish(id,w,h,pixels);
        }
        static Color[] CropPixels(string id,out int w,out int h)
        {
            w=h=0;var record=Find(id);if(record==null)return null;var source=WorkingSource(record.file);if(!source)return null;
            var v=record.view;var clip=record.rect??v;w=Mathf.CeilToInt(v[2]);h=Mathf.CeilToInt(v[3]);var pixels=new Color[w*h];
            var src=Pixels(source);int sw=source.width,sh=source.height;
            // Source column, and source row start, of each output column and row; -1 outside the clip or the atlas.
            var columns=new int[w];var rows=new int[h];
            for(int x=0;x<w;x++){float sx=v[0]+(x+.5f)*v[2]/w;columns[x]=sx<clip[0]||sx>=clip[0]+clip[2]||!(sx>=0&&sx<sw)?-1:(int)sx;}
            for(int y=0;y<h;y++){float sy=v[1]+(y+.5f)*v[3]/h;rows[y]=sy<clip[1]||sy>=clip[1]+clip[3]||!(sy>=0&&sy<sh)?-1:(sh-1-(int)sy)*sw;}
            for(int y=0;y<h;y++)
            {
                int row=rows[y];if(row<0)continue;int at=(h-1-y)*w;
                for(int x=0;x<w;x++){int column=columns[x];if(column>=0)pixels[at+x]=src[row+column];}
            }
            return pixels;
        }
        // Spreads a picture's first composition over calls, one step each, so a caller can give it one step a frame:
        // PrepareSource loads the authored ink or fallback atlases in the background and reads their pixels once;
        // Prepare then builds the final sprite or ink. True once nothing is left, so Texture(id) or Ink(id) costs
        // a cache look-up. Skipping them gives the same picture, all in one call.
        static readonly Dictionary<string,ResourceRequest> pendingSources=new Dictionary<string,ResourceRequest>();
        public static bool PrepareSource(string id)
        {
            var ink=FindInk(id);
            if(ink!=null)
            {
                string file="sources/inventory-ink-pixel/"+id+".png";
                if(!PrepareFile(file))return false;
                if(working.TryGetValue(file,out var artwork)&&artwork)return true;
                return PrepareAtlas(ink.bottle)&&PrepareAtlas(ink.effect)&&PrepareAtlas(ink.condition);
            }
            return PrepareAtlas(id);
        }
        public static bool Prepare(string id)
        {
            string key=FindInk(id)==null?id:"ink:"+id;
            if(key!=null&&textures.ContainsKey(key))return true;
            if(!PrepareSource(id))return false;
            var ink=FindInk(id);
            if(ink==null)return !Crop(id);
            if(textures.ContainsKey("ink:"+id))return true;
            return !Ink(id);
        }
        // True when this call cropped the sprite (false when it was cropped already or cannot be).
        static bool Crop(string id)=>id!=null&&!textures.ContainsKey(id)&&Texture(id);
        static bool PrepareAtlas(string id)
        {
            var record=Find(id);if(record==null)return true;
            return PrepareFile(record.file);
        }
        static bool PrepareFile(string file)
        {
            KeepWorking();
            string key="source:"+file;
            if(!textures.TryGetValue(key,out var t)&&!working.TryGetValue(file,out t))
            {
                string path="ApprovedTheme/"+file.Substring(0,file.LastIndexOf('.'));
                if(!pendingSources.TryGetValue(key,out var request))pendingSources[key]=request=Resources.LoadAsync<Texture2D>(path);
                if(!request.isDone)return false;
                pendingSources.Remove(key);t=request.asset as Texture2D;
                if(t){t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;}working[file]=t;return false;
            }
            if(!t||sourcePixels.ContainsKey(t))return true;
            Pixels(t);return false;
        }
        // One CPU copy per working source, read once while a screen is built. A GetPixel call per pixel cost about half a
        // million native calls for each ink bottle (bottle crop, then its blit and two marks), which stalled the frame that
        // first drew a maintenance shelf or bag slot (QA 2026-11-16). GetPixels32 returns the same bytes GetPixel reads.
        static readonly Dictionary<Texture2D,Color32[]> sourcePixels=new Dictionary<Texture2D,Color32[]>();
        static Color32[] Pixels(Texture2D texture)
        {
            KeepWorking();
            if(sourcePixels.TryGetValue(texture,out var cached)&&cached.Length==texture.width*texture.height)return cached;
            var read=texture.GetPixels32();sourcePixels[texture]=read;return read;
        }
        static Sprite MakeSprite(string key,Texture2D texture,Vector4 border=default)
        {
            if(sprites.TryGetValue(key,out var s))return s;if(!texture)return null;
            s=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,border);s.name=key;s.hideFlags=HideFlags.DontSave;sprites[key]=s;return s;
        }
        public static Sprite Sprite(string id)=>MakeSprite(id,Texture(id));
        public static Texture2D SelectionWash=>Texture("common/maintenance-palette-v1__assets__selection-wash-v1");
        public static Sprite FrameLarge=>MakeSprite("FrameLarge",Source("sources/combat-hud-v2/assets/frame-large.png"),Vector4.one*260);
        public static PixelFramedPanel CreateFrame(Transform parent,float x,float y,float width,float height,float referenceScale=1)
        {
            var go=new GameObject("Approved pixel frame",typeof(RectTransform));var rect=go.GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
            var frame=go.AddComponent<PixelFramedPanel>();frame.SetReferenceScale(referenceScale);frame.Configure(FrameLarge);
            frame.raycastTarget=false;return frame;
        }
        public static Sprite FrameSpeech=>MakeSprite("FrameSpeech",Source("sources/sketch-v3/assets/speech-frame-v1.png"));
        // The same sampling as blitting the finished crop texture: src is that crop's pixels in GetPixels order.
        static void Blit(Color[] destination,int width,int height,Color[] src,int srcWidth,int srcHeight,float x,float y,float w,float h)
        {
            if(src==null||w<=0||h<=0)return;
            int sw=srcWidth,sh=srcHeight;
            int x0=Mathf.Max(0,Mathf.FloorToInt(x)),x1=Mathf.Min(width,Mathf.CeilToInt(x+w)),y0=Mathf.Max(0,Mathf.FloorToInt(y)),y1=Mathf.Min(height,Mathf.CeilToInt(y+h));
            if(x1<=x0||y1<=y0)return;
            // The sample point is separable: source column and row start per destination column and row, -1 outside.
            var columns=new int[x1-x0];
            for(int ix=x0;ix<x1;ix++){float u=(ix+.5f-x)/w;columns[ix-x0]=u<0||u>=1?-1:Mathf.Clamp((int)(u*sw),0,sw-1);}
            for(int iy=y0;iy<y1;iy++)
            {
                float v=(iy+.5f-y)/h;if(v<0||v>=1)continue;
                int row=Mathf.Clamp(sh-1-(int)(v*sh),0,sh-1)*sw,line=(height-1-iy)*width;
                for(int ix=x0;ix<x1;ix++)
                {
                    int column=columns[ix-x0];if(column<0)continue;
                    var s=src[row+column];int at=line+ix;
                    // Exact shortcuts of the blend below: an opaque source pixel yields itself (alpha 1, the old term times 0),
                    // a clear one over a clear destination yields alpha 0, which the blend never writes.
                    if(s.a==1f){destination[at]=s;continue;}
                    var old=destination[at];if(s.a==0&&old.a<=0)continue;
                    Color c=s;float alpha=c.a+old.a*(1-c.a);
                    if(alpha>0)destination[at]=new Color((c.r*c.a+old.r*old.a*(1-c.a))/alpha,(c.g*c.a+old.g*old.a*(1-c.a))/alpha,(c.b*c.a+old.b*old.a*(1-c.a))/alpha,alpha);
                }
            }
        }
        static void BlitCrop(Color[] destination,int width,int height,string id,float x,float y,float w,float h)
        {
            var src=CropPixels(id,out int sw,out int sh);Blit(destination,width,height,src,sw,sh,x,y,w,h);
        }
        static void MarkBlit(Color[] pixels,int w,int h,string id,float x,float y,float size)
        {
            var src=CropPixels(id,out int tw,out int th);if(src==null)return;
            float scale=size/Mathf.Max(tw,th);float sw=tw*scale,sh=th*scale;Blit(pixels,w,h,src,tw,th,x-sw/2,y-sh/2,sw,sh);
        }
        public static Texture2D Ink(string id)
        {
            string key="ink:"+id;if(textures.TryGetValue(key,out var t))return t;
            var pixels=InkPixels(id,out int w,out int h);return pixels==null?null:Finish(key,w,h,pixels);
        }
        static Color[] InkPixels(string id,out int w,out int h)
        {
            w=h=0;var ink=FindInk(id);if(ink==null)return null;
            var artwork=WorkingSource("sources/inventory-ink-pixel/"+id+".png");
            if(artwork)
            {
                w=artwork.width;
                h=artwork.height;
                var source=Pixels(artwork);
                var authored=new Color[source.Length];
                for(int i=0;i<source.Length;i++)authored[i]=source[i];
                return authored;
            }
            w=Mathf.CeilToInt(ink.view[2]);h=Mathf.CeilToInt(ink.view[3]);var pixels=new Color[w*h];
            BlitCrop(pixels,w,h,ink.bottle,0,0,w,h);
            MarkBlit(pixels,w,h,ink.effect,ink.label.center[0]-ink.view[0],ink.label.center[1]-ink.view[1],ink.label.size);
            MarkBlit(pixels,w,h,ink.condition,ink.badge.center[0]-ink.view[0],ink.badge.center[1]-ink.view[1],ink.badge.size);
            return pixels;
        }
        public static Sprite InkSprite(string id)=>MakeSprite("ink:"+id,Ink(id));
        public static Texture2D Palette(string color,string inkId=null)
        {
            color=string.IsNullOrEmpty(color)?"neutral":color;string key="palette:"+color+":"+inkId;if(textures.TryGetValue(key,out var t))return t;
            return Finish(key,220,155,PalettePixels(color,inkId));
        }
        static Color[] PalettePixels(string color,string inkId)
        {
            var pixels=new Color[220*155];BlitCrop(pixels,220,155,"palette/bases/"+color,0,0,220,155);
            var ink=FindInk(inkId);if(ink!=null){BlitCrop(pixels,220,155,"palette/paint/"+ink.color,0,0,220,155);MarkBlit(pixels,220,155,ink.effect,110,78,80);}
            return pixels;
        }
        public static Sprite PaletteSprite(string color,string inkId=null)=>MakeSprite("palette:"+color+":"+inkId,Palette(color,inkId));
        static readonly float[] SpeechX={0,220,900,1080,1763,1983},SpeechY={0,180,570,793};
        static float[] SpeechWidths(float width,float scale)
        {float corner=Mathf.Min(220*scale,width*.2f),tail=Mathf.Min(180*scale,width*.2f),band=Mathf.Max(0,(width-2*corner-tail)/2);return new[]{corner,band,tail,band,corner};}
        static Texture2D speechGuiLinear;
        // IMGUI applies output gamma even to an sRGB source. Keep converted pixels in an sRGB-tagged
        // copy to avoid a second gamma lift; the imported PNG and uGUI FrameSpeech stay untouched.
        static Texture2D SpeechGuiTexture
        {
            get
            {
                var sprite=FrameSpeech;var source=sprite?sprite.texture:null;
                if(!source||QualitySettings.activeColorSpace!=ColorSpace.Linear)return source;
                if(speechGuiLinear)return speechGuiLinear;
                var pixels=source.GetPixels();
                for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].linear;
                var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,false)
                {name="Approved speech frame IMGUI linear",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                copy.SetPixels(pixels);copy.Apply(false,false);return speechGuiLinear=copy;
            }
        }
        public static void DrawSpeech(Rect rect,float scale=.1f)
        {
            var t=SpeechGuiTexture;if(!t)return;var widths=SpeechWidths(rect.width,scale);float[] heights={Mathf.Min(180*scale,rect.height*.25f),Mathf.Max(0,rect.height-403*scale),Mathf.Min(223*scale,rect.height*.35f)};
            // IMGUI rounds each transformed rectangle independently. Draw shared slice edges in
            // final screen pixels so adjacent pieces cannot leave a bright one-pixel gap.
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            try
            {
                float y=rect.y;
                for(int row=0;row<3;row++)
                {
                    float x=rect.x;
                    for(int col=0;col<5;col++)
                    {
                        var a=matrix.MultiplyPoint3x4(new Vector3(x,y,0));
                        var b=matrix.MultiplyPoint3x4(new Vector3(x+widths[col],y+heights[row],0));
                        var pixels=Rect.MinMaxRect(Mathf.Round(a.x),Mathf.Round(a.y),Mathf.Round(b.x),Mathf.Round(b.y));
                        var uv=new Rect(SpeechX[col]/1983,1-SpeechY[row+1]/793,(SpeechX[col+1]-SpeechX[col])/1983,(SpeechY[row+1]-SpeechY[row])/793);
                        GUI.DrawTextureWithTexCoords(pixels,t,uv);x+=widths[col];
                    }
                    y+=heights[row];
                }
            }
            finally{GUI.matrix=matrix;}
        }
        public static RectTransform CreateSpeech(Transform parent,float width,float height,float scale=.1f)
        {
            var go=new GameObject("Approved speech frame",typeof(RectTransform));var root=(RectTransform)go.transform;root.SetParent(parent,false);root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);root.sizeDelta=new Vector2(width,height);
            var t=FrameSpeech;if(!t)return root;var widths=SpeechWidths(width,scale);float[] heights={Mathf.Min(180*scale,height*.25f),Mathf.Max(0,height-403*scale),Mathf.Min(223*scale,height*.35f)};
            float y=0;for(int row=0;row<3;row++){float x=0;for(int col=0;col<5;col++){var cell=new GameObject("Frame segment",typeof(RectTransform),typeof(RawImage));var rt=(RectTransform)cell.transform;rt.SetParent(root,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(widths[col],heights[row]);var image=cell.GetComponent<RawImage>();image.raycastTarget=false;image.texture=t.texture;image.uvRect=new Rect(SpeechX[col]/1983,1-SpeechY[row+1]/793,(SpeechX[col+1]-SpeechX[col])/1983,(SpeechY[row+1]-SpeechY[row])/793);x+=widths[col];}y+=heights[row];}return root;
        }
    }
}
