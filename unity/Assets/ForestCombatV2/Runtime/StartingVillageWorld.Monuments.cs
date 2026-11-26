using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        FistKingMonumentData monumentData;
        SpriteRenderer fistMonument, jumpMapMonument;
        SpriteRenderer fistPlaque, jumpMapPlaque;
        readonly List<Material> monumentMaterials = new List<Material>();
        BoxCollider2D[] monumentPlatforms;
        Texture2D monumentMarkerTexture;
        ProfileSnapshot monumentProfile;
        string fistTime, jumpMapTime;
        public SpriteRenderer FistMonument => fistMonument;
        public SpriteRenderer JumpMapMonument => jumpMapMonument;
        public SpriteRenderer FistPlaque => fistPlaque;
        public SpriteRenderer JumpMapPlaque => jumpMapPlaque;
        public string FistMonumentTime => fistTime;
        public string JumpMapMonumentTime => jumpMapTime;
        public IReadOnlyList<BoxCollider2D> MonumentPlatforms => monumentPlatforms;

        void BuildMonumentGround()
        {
            monumentData = FistKingMonumentData.Load();
            if (GroundLeft >= 0) return;
            World.ExtendVillageGroundLeft(GroundLeft, GroundTop);
            monumentLeftLimit = World.transform.Find("Left world limit");
            MoveMonumentBoundary();
            World.CameraMinPixels = monumentData.entranceX * 32;
            Physics2D.SyncTransforms(); World.RefreshSurfaceIndex();
        }
        void BuildFistMonument()
        {
            fistMonument = BuildMonumentArt("Fist king monument", monumentData.sprite, monumentData.spriteRect, monumentData.height, monumentData.slotIndex);
            var jump = monumentData.jumpMap;
            jumpMapMonument = BuildMonumentArt("Sky parkour monument", jump.sprite, jump.spriteRect, jump.height, jump.slotIndex);
            fistPlaque = BuildMonumentPlaque(fistMonument);
            jumpMapPlaque = BuildMonumentPlaque(jumpMapMonument);
            BuildMonumentPlatforms();
            BuildMonumentNotice();
            RefreshFistMonument();
        }

        Vector3 MonumentPosition(int slot) =>
            new Vector3(monumentData.slotXs[slot], GroundTop + monumentData.platformHeight, 0);

        void BuildMonumentPlatforms()
        {
            int width = monumentData.platformWidth;
            var planks = new List<SpriteRenderer>(width);
            int firstWoodPiece = paired.Count;
            var markerSprite = BuildMonumentSlotMarker();
            int count = monumentData.slotXs.Length;
            monumentPlatforms = new BoxCollider2D[count];
            for (int slot = 0; slot < count; slot++)
            {
                var top = MonumentPosition(slot);
                var platform = new GameObject("Monument platform");
                platform.transform.SetParent(World.transform, false);
                const float thickness = .125f;
                platform.transform.position = top - Vector3.up * (thickness / 2);
                var surface = platform.AddComponent<ArenaSurface>();
                surface.oneWay = true;
                surface.footstepMaterial = FootstepMaterial.Wood;
                var hitbox = platform.AddComponent<BoxCollider2D>();
                hitbox.size = new Vector2(width, thickness);
                monumentPlatforms[slot] = hitbox;

                planks.Clear();
                WoodPlatform("Monument platform wood", top.x - width / 2f, top.y - GroundTop, width, -6, planks);
                foreach (var plank in planks) plank.transform.SetParent(platform.transform, true);

                var marker = Renderer("Empty monument slot", markerSprite, fistMonument.sortingOrder);
                marker.transform.SetParent(platform.transform, false);
                marker.transform.position = top + Vector3.up * .625f;
            }
            ApplyMonumentPieces(firstWoodPiece);
            Physics2D.SyncTransforms();
        }

        void ApplyMonumentPieces(int first)
        {
            for (int i = first; i < paired.Count; i++)
            {
                var piece = paired[i];
                bool sketch = Preview ?? !IsColoured(piece.region);
                piece.erased = sketch ? 1 : 0;
                ApplyPiece(piece, sketch);
            }
        }

        Sprite BuildMonumentSlotMarker()
        {
            const int size = 11, center = size / 2;
            var pixels = new Color32[size * size];
            var paper = new Color32(243, 231, 207, 255);
            var ink = new Color32(57, 51, 45, 220);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                if (distance >= 3 && distance <= 5) pixels[y * size + x] = distance == 4 ? paper : ink;
            }
            monumentMarkerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Empty monument marker", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            monumentMarkerTexture.SetPixels32(pixels);
            monumentMarkerTexture.Apply(false, true);
            return Make(monumentMarkerTexture, Vector2.one * .5f);
        }

        SpriteRenderer BuildMonumentPlaque(SpriteRenderer art)
        {
            var label = Renderer(art.name + " record", null, art.sortingOrder + 1);
            label.transform.SetParent(art.transform, false);
            var material = new Material(art.sharedMaterial) { hideFlags = HideFlags.DontSave, mainTexture = null };
            monumentMaterials.Add(material);
            label.sharedMaterial = material;
            return label;
        }
        SpriteRenderer BuildMonumentArt(string name, string path, float[] rect, float height, int slot)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) throw new System.InvalidOperationException("Monument art is missing: " + path);
            var sprite = Sprite.Create(texture, new Rect(rect[0], rect[1], rect[2], rect[3]), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect);
            made.Add(sprite);
            // Keep the plaque in front of garden decoration and behind the player.
            var art = Renderer(name, sprite, World.arena.Player.visual.sortingOrder - 2);
            art.transform.position = new Vector3(monumentData.slotXs[slot], GroundTop, 0);
            art.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
            var shader = Resources.Load<Shader>("FistMonumentInk");
            if (!shader) throw new System.InvalidOperationException("FistMonumentInk shader is missing.");
            // Keep a distinct texture binding for each prop: Unity can batch sprite renderers with the same material.
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, mainTexture = texture };
            monumentMaterials.Add(material);
            art.sharedMaterial = material;
            return art;
        }
        void RefreshFistMonument()
        {
            if (!fistMonument) return;
            var service = Chapter1ProfileHost.Service;
            if (monumentProfile != service.Snapshot)
            {
                monumentProfile = service.Snapshot;
                var fist = monumentProfile.FistKing;
                var jump = monumentProfile.BestSkyParkour;
                var nextFistTime = fist == null ? null : FistKingRecord.Format(fist.milliseconds);
                var nextJumpTime = jump == null ? null : FistKingRecord.Format((long)System.Math.Round(jump.seconds * 1000d));
                if (fistTime != nextFistTime)
                {
                    fistTime = nextFistTime;
                    UpdateMonumentPlaque(fistPlaque, fistMonument.sprite.pivot, monumentData.plaqueRect, fistTime);
                }
                if (jumpMapTime != nextJumpTime)
                {
                    jumpMapTime = nextJumpTime;
                    UpdateMonumentPlaque(jumpMapPlaque, jumpMapMonument.sprite.pivot, monumentData.jumpMap.plaqueRect, jumpMapTime);
                }
            }
            TickMonumentReveal(service);
            bool show = MonumentClearingOpen && !StationsSuppressed;
            fistMonument.enabled = show && fistTime != null;
            jumpMapMonument.enabled = show && jumpMapTime != null;
            fistPlaque.enabled = fistMonument.enabled;
            jumpMapPlaque.enabled = jumpMapMonument.enabled;
            foreach (var platform in monumentPlatforms) platform.gameObject.SetActive(show);
            bool colour = Preview == false || !Preview.HasValue && Chapter1ProfileHost.Service.PalettePieces >= 5;
            foreach (var material in monumentMaterials) material.SetFloat("_Saturation", colour ? 1 : 0);
        }
        // Seven rows of four bits per digit, drawn on the same grid as the plinth.
        static readonly int[] PlaqueDigits =
        {
            0b0110_1001_1001_1001_1001_1001_0110, 0b0100_1100_0100_0100_0100_0100_1110,
            0b0110_1001_0001_0010_0100_1000_1111, 0b1110_0001_0001_0110_0001_0001_1110,
            0b0010_0110_1010_1010_1111_0010_0010, 0b1111_1000_1000_1110_0001_1001_0110,
            0b0110_1000_1000_1110_1001_1001_0110, 0b1111_0001_0010_0010_0100_0100_0100,
            0b0110_1001_1001_0110_1001_1001_0110, 0b0110_1001_1001_0111_0001_0001_0110
        };
        const int PlaqueGlyphHeight = 7;
        const int PlaqueDigitWidth = 4;

        static int GlyphWidth(char c) => c >= '0' && c <= '9' ? PlaqueDigitWidth : 1;
        static bool GlyphPixel(char c, int row, int column)
        {
            if (c == ':') return row == 2 || row == 4;
            if (c == '.') return row == PlaqueGlyphHeight - 1;
            int shift = (PlaqueGlyphHeight - 1 - row) * PlaqueDigitWidth + PlaqueDigitWidth - 1 - column;
            return (PlaqueDigits[c - '0'] & (1 << shift)) != 0;
        }
        void ClearMonumentPlaque(SpriteRenderer label)
        {
            if (!label || !label.sprite) return;
            var sprite = label.sprite;
            label.sprite = null;
            Destroy(sprite.texture);
            Destroy(sprite);
        }
        void UpdateMonumentPlaque(SpriteRenderer label, Vector2 pivot, int[] rect, string time)
        {
            ClearMonumentPlaque(label);
            if (time == null) return;

            int width = time.Length - 1;
            foreach (char c in time) width += GlyphWidth(c);
            var pixels = new Color32[width * PlaqueGlyphHeight];
            var ink = new Color32(254, 232, 185, 255);
            int offset = 0;
            foreach (char c in time)
            {
                int columns = GlyphWidth(c);
                for (int row = 0; row < PlaqueGlyphHeight; row++)
                for (int col = 0; col < columns; col++)
                {
                    if (GlyphPixel(c, row, col))
                        pixels[(PlaqueGlyphHeight - 1 - row) * width + offset + col] = ink;
                }
                offset += columns + 1;
            }
            var texture = new Texture2D(width, PlaqueGlyphHeight, TextureFormat.RGBA32, false)
            { name = label.name + " pixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            const float pixelsPerUnit = 32;
            label.sprite = Sprite.Create(texture, new Rect(0, 0, width, PlaqueGlyphHeight), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            float scale = Mathf.Min(1, (rect[2] - 2f) / width);
            label.transform.localScale = Vector3.one * scale;
            label.transform.localPosition = new Vector3(
                (rect[0] + Mathf.Floor((rect[2] - width * scale) / 2) - pivot.x) / pixelsPerUnit,
                (rect[1] + Mathf.Floor((rect[3] - PlaqueGlyphHeight * scale) / 2) - pivot.y) / pixelsPerUnit, 0);
        }
        void DisposeFistMonument()
        {
            DisposeMonumentReveal();
            ClearMonumentPlaque(fistPlaque);
            ClearMonumentPlaque(jumpMapPlaque);
            if (monumentMarkerTexture) Destroy(monumentMarkerTexture);
            foreach (var material in monumentMaterials) if (material) Destroy(material);
            monumentMaterials.Clear();
        }
    }
}
