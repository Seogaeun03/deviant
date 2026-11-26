using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ClockBossContactShadowTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator CompositeBodyGetsAProportionalShadowWithoutEnlargingItsMotor()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            yield return new EnterPlayMode();
            var arena = new GameObject("Shadow arena").AddComponent<CombatArena>();
            var camera = new GameObject("Shadow camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8.4375f;
            camera.transform.position = new Vector3(0, 6, -10);
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Camera = camera; content.SpriteMaterial = material;
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSpawn = new Vector2(-10, 0); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = Vector2.up;
            content.PlayerSprite = Resources.Load<Sprite>("UnarmedBody/idle/F01"); content.PlayerSortingOrder = 10;
            content.Enemies = System.Array.Empty<CombatEnemySpawn>();
            content.PrepareEnvironment = _ => {
                var floor = new GameObject("Shadow floor"); floor.transform.position = new Vector3(0, -.5f, 0);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            };
            Assert.That(arena.Initialize(content), Is.True);
            using (var simulation = arena.AcquireManualSimulation())
            {
                var boss = new GameObject("Composite clock boss").AddComponent<ClockExecutionerBoss>(); boss.Initialize(arena, arena.TechnicalSprite, -15, 0); arena.enemies.Add(boss); boss.Present();
                yield return null; // The arena creates its shared shadow presenter in LateUpdate.
                Assert.That(arena.ContactShadows, Is.Not.Null);
                Physics2D.SyncTransforms(); arena.ContactShadows.Tick();
                var shadows = arena.transform.Find("Contact shadows").GetComponentsInChildren<SpriteRenderer>();
                var hero = shadows.Single(r => Mathf.Abs(r.transform.position.x + 10) < .1f);
                var clock = shadows.Single(r => Mathf.Abs(r.transform.position.x) < .1f);
                Assert.That(clock.enabled, Is.True);
                Assert.That(hero.sprite.rect.size, Is.EqualTo(new Vector2(36, 10)), "the player's existing footprint is unchanged");
                Assert.That(clock.bounds.size.x, Is.EqualTo(ClockExecutionerBoss.HurtWidth * ContactShadowPresentation.WidthPerBody).Within(.125f));
                Assert.That(clock.sprite.rect.height, Is.EqualTo(60), "depth scales with the composite body, on the 2px grid");
                Assert.That(boss.bodyCollider.size, Is.EqualTo(Vector2.one));
                Assert.That(clock.color, Is.EqualTo(hero.color), "the same ground-darkening strength");
                Assert.That(clock.transform.position.y, Is.EqualTo(0).Within(.001f));
                Assert.That(boss.PreviewArtLoaded, Is.True);
                var rt = RenderTexture.GetTemporary(960,540,24); camera.targetTexture = rt; camera.rect = new Rect(0,0,1,1); camera.Render();
                RenderTexture.active = rt; var capture = new Texture2D(960,540,TextureFormat.RGB24,false); capture.ReadPixels(new Rect(0,0,960,540),0,0); capture.Apply();
                string output = System.IO.Path.GetFullPath("../outputs/clock-boss-shadow"); System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"scaled.png"),capture.EncodeToPNG());
                RenderTexture.active = null; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(capture);
                Assert.That(boss.size, Is.EqualTo(Vector2.one), "visual size must not change the motor");
                Assert.That(boss.hurtCollider.size, Is.EqualTo(new Vector2(ClockExecutionerBoss.HurtWidth, ClockExecutionerBoss.HurtHeight)));
                boss.Position += Vector2.up * 3; Physics2D.SyncTransforms(); arena.ContactShadows.Tick();
                Assert.That(clock.enabled, Is.True); Assert.That(clock.transform.position.y, Is.Zero.Within(.001f));
                Assert.That(clock.bounds.size.x, Is.LessThan(ClockExecutionerBoss.HurtWidth * ContactShadowPresentation.WidthPerBody));
                Assert.That(clock.color.a, Is.LessThan(hero.color.a));
                boss.Position += Vector2.up * 6; arena.ContactShadows.Tick(); Assert.That(clock.enabled, Is.False);
                boss.Position = Vector2.zero; boss.health = 0; arena.ContactShadows.Tick(); Assert.That(clock.enabled, Is.False);
            }
            Object.Destroy(material);
        }
    }
}
