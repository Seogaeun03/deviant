using System.Collections;
using System.IO;
using Deviant.Combat;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class PlayerHitEffectVisibilityTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static Texture2D Render(Camera camera)
        {
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var previous = camera.targetTexture; var rect = camera.rect;
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.Render();
            var active = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            RenderTexture.active = active; camera.targetTexture = previous; camera.rect = rect;
            RenderTexture.ReleaseTemporary(target); return image;
        }

        [UnityTest] public IEnumerator ContactBurstKeepsBothFacingEyesAndStillDrawsOutsideTheBody()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Visibility arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode();
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var camera = new GameObject("Visibility camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8.4375f; camera.aspect = 16f / 9;
            camera.transform.position = new Vector3(0, 3, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Camera = camera; content.SpriteMaterial = material;
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = Vector2.up;
            content.PlayerSortingOrder = 10; content.PlayerSprite = Resources.Load<Sprite>("UnarmedAttack/jab-combo/frame-11");
            content.Enemies = System.Array.Empty<CombatEnemySpawn>();
            Assert.That(arena.Initialize(content), Is.True);
            // Hold a real arena's clock while the asynchronous pixel burst is baked; use its native attack sprite.
            var player = arena.Player; player.visual.sprite = content.PlayerSprite;
            player.visual.transform.localPosition = Vector3.zero; player.visual.transform.localScale = Vector3.one;
            var fx = new GameObject("Visibility pixel effects").AddComponent<CombatFeelPixelFx>();
            fx.Bind(arena, CombatFeelSettings.Load());
            var view = fx.GetComponentInChildren<SpriteRenderer>();
            string output = Path.GetFullPath("../outputs/player-hit-visibility"); Directory.CreateDirectory(output);
            using (var simulation = arena.AcquireManualSimulation())
            foreach (int facing in new[] { 1, -1 })
            {
                fx.Clear(); player.SyncFacing(facing); player.visual.flipX = facing < 0;
                player.Position = Vector2.zero;
                view.enabled = false; var baseline = Render(camera);
                int topGold = -1;
                for (int y = 0; y < 540; y++) for (int x = 0; x < 960; x++) { var c = baseline.GetPixel(x,y); if (c.r > .45f && c.g > .25f && c.b < .35f && c.r > c.g * 1.1f) topGold = Mathf.Max(topGold,y); }
                Assert.That(topGold, Is.GreaterThan(0), "native gold eye pixels are present");
                float eyeX = 0; int goldCount = 0;
                for (int y = topGold - 4; y <= topGold; y++) for (int x = 0; x < 960; x++) { var c = baseline.GetPixel(x,y); if (c.r > .45f && c.g > .25f && c.b < .35f && c.r > c.g * 1.1f) { eyeX += x; goldCount++; } }
                var eyePoint = new Vector2(camera.transform.position.x + (eyeX / goldCount / 960f - .5f) * camera.orthographicSize * 2 * camera.aspect, camera.transform.position.y - camera.orthographicSize + (topGold - 1) / 540f * camera.orthographicSize * 2);
                // A point-blank enemy contact can overlap the face. The unmasked frame must reproduce that overlap.
                fx.Hit(player, eyePoint, false, false, facing, 28 * facing, false);
                for (int frame = 0; frame < 5; frame++) { yield return null; player.visual.flipX = facing < 0; player.visual.sprite = content.PlayerSprite; player.visual.transform.localPosition = Vector3.zero; player.visual.transform.localScale = Vector3.one; fx.Refresh(); }
                fx.Refresh(); Assert.That(fx.LastDrawnPixels, Is.GreaterThan(20));
                Object.Destroy(baseline); view.enabled = false; baseline = Render(camera);
                view.enabled = true; view.maskInteraction = SpriteMaskInteraction.None;
                yield return null; player.visual.flipX = facing < 0; player.visual.sprite = content.PlayerSprite; player.visual.transform.localScale = Vector3.one; fx.Refresh();
                var unmasked = Render(camera);
                view.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                yield return null; player.visual.flipX = facing < 0; player.visual.sprite = content.PlayerSprite; player.visual.transform.localScale = Vector3.one; fx.Refresh();
                var masked = Render(camera);
                int eyes = 0, coveredBefore = 0, coveredAfter = 0, effectPixels = 0;
                // RenderTexture dimensions, not the Editor Game view, define the sampled game pixels.
                int faceBottom = topGold - 4;
                for (int y = 0; y < 540; y++) for (int x = 0; x < 960; x++)
                {
                    var b = baseline.GetPixel(x, y); var before = unmasked.GetPixel(x, y); var after = masked.GetPixel(x, y);
                    if (Vector3.Distance(new Vector3(b.r,b.g,b.b), new Vector3(after.r,after.g,after.b)) > .1f) effectPixels++;
                    if (y < faceBottom || b.r < .45f || b.g < .25f || b.b > .35f || b.r < b.g * 1.1f) continue;
                    eyes++;
                    if (Vector3.Distance(new Vector3(b.r,b.g,b.b), new Vector3(before.r,before.g,before.b)) > .1f) coveredBefore++;
                    if (Vector3.Distance(new Vector3(b.r,b.g,b.b), new Vector3(after.r,after.g,after.b)) > .1f) coveredAfter++;
                }
                File.WriteAllBytes(Path.Combine(output, "baseline-" + facing + ".png"), baseline.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output, "before-" + facing + ".png"), unmasked.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output, "after-" + facing + ".png"), masked.EncodeToPNG());
                Object.Destroy(baseline); Object.Destroy(unmasked); Object.Destroy(masked);
                Assert.That(eyes, Is.GreaterThan(2), "the native jab drawing has visible gold eyes");
                Assert.That(coveredBefore, Is.GreaterThan(0), "the original foreground burst covers the eyes");
                Assert.That(coveredAfter, Is.Zero, "the player's eyes survive the same contact burst");
                Assert.That(effectPixels, Is.GreaterThan(20), "the effect outside the body is preserved");
            }
            Object.Destroy(material);
        }
    }
}
