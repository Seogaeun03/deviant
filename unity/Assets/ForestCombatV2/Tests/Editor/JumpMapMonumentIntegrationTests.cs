using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class JumpMapMonumentIntegrationTests
    {
        static StartingVillageWorld village;
        static MemoryProfileStore store;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); Chapter1ProfileHost.UseStore(null); village = null; store = null; }
        static void OpenVillage()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
        }
        static IEnumerator Boot(bool opened = true)
        {
            var data = ProfileData.CreateNew(Guid.NewGuid().ToString());
            data.seenStories = new[] { "opening", "first-return" }; data.runsStarted = 1;
            if (opened)
            {
                data.seenStories = data.seenStories.Append(StartingVillageWorld.MonumentClearingStory).ToArray();
                data.fistKing = new FistKingRecord { runId = Guid.NewGuid().ToString(), milliseconds = 137384 };
            }
            store = new MemoryProfileStore(data); Chapter1ProfileHost.UseStore(store);
            for (int i = 0; i < 900; i++)
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); if (village && village.Ready) break; }
            Assert.That(village && village.Ready, Is.True);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "TemporaryGameGui");
            if (gui) gui.GetType().GetMethod("SkipOpening", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gui, null);
            for (int i = 0; i < 30; i++) yield return null;
            village = Object.FindAnyObjectByType<StartingVillageWorld>(); village.RemoveOpeningCourse(); village.StationsSuppressed = false;
        }
        static void Stand(Vector2 at)
        {
            var player = village.World.arena.Player; player.Position = at; player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
        }
        [UnityTest] public IEnumerator SavedBestControlsUnlockTimeReloadAndTheInsetPixelPlaque()
        {
            OpenVillage();
            var gameView = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Deviant.Hub.Editor.ApprovedVillageEditor")).FirstOrDefault(t => t != null);
            gameView?.GetMethod("UseFullHdGameView")?.Invoke(null, null);
            yield return new EnterPlayMode();
            yield return Boot();
            var service = Chapter1ProfileHost.Service;
            Assert.That(village.JumpMapMonument.enabled, Is.False);
            Assert.That(village.JumpMapPlaque.enabled, Is.False);
            Assert.That(village.MonumentPlatforms.Count(Marked), Is.EqualTo(3));
            service.RecordSkyParkour(new SkyParkourRecord { seconds = 10, assisted = true });
            yield return null; yield return null;
            Assert.That(village.JumpMapMonument.enabled, Is.False, "practice clears do not unlock it");
            service.RecordSkyParkour(new SkyParkourRecord { seconds = 208.417f });
            yield return null; yield return null;
            Assert.That(village.JumpMapMonument.enabled, Is.True);
            Assert.That(village.JumpMapMonumentTime, Is.EqualTo("03:28.417"));
            Assert.That(village.JumpMapMonument.sprite.texture, Is.SameAs(Resources.Load<Texture2D>("Hub/monuments/jump-map-c-f")));
            Assert.That(village.JumpMapMonument.sprite.texture, Is.Not.SameAs(village.FistMonument.sprite.texture));
            Assert.That(village.JumpMapMonument.bounds.size.y, Is.EqualTo(village.FistMonument.bounds.size.y).Within(1f / 32));
            Assert.That(village.JumpMapMonument.sprite.rect.size, Is.EqualTo(new Vector2(77, 80)));
            Assert.That(village.FistMonument.sprite.rect.size, Is.EqualTo(new Vector2(77, 80)));
            Assert.That(village.JumpMapMonument.transform.localScale, Is.EqualTo(Vector3.one));
            var label = village.JumpMapPlaque;
            AssertPlaqueFits(village.FistPlaque, village.FistMonument, FistKingMonumentData.Load().plaqueRect);
            AssertPlaqueFits(label, village.JumpMapMonument, FistKingMonumentData.Load().jumpMap.plaqueRect);
            Assert.That(label.enabled && label.sprite, Is.True);
            Assert.That(label.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            var firstLabel = label.sprite;
            var platforms = village.MonumentPlatforms;
            Assert.That(platforms.Count, Is.EqualTo(3));
            var tops = platforms.Select(Top).ToArray();
            Assert.That(tops.Select(top => top.x), Is.EqualTo(new[] { -2.5f, -5.75f, -9f }));
            Assert.That(tops.Select(top => top.y), Is.EqualTo(new[] { -9.75f, -9.75f, -9.75f }));
            string[] wood = { "left", "middle-a", "right" };
            foreach (var platform in platforms)
            {
                Assert.That(platform.size.x, Is.EqualTo(3));
                var surface = platform.GetComponent<ArenaSurface>();
                Assert.That(surface.oneWay, Is.True);
                Assert.That(surface.footstepMaterial, Is.EqualTo(FootstepMaterial.Wood));
                Assert.That(Planks(platform).Select(plank => plank.sprite.texture), Is.EqualTo(
                    wood.Select(piece => Resources.Load<Texture2D>("Hub/pixel-village/ground/sketch/wood/" + piece))));
            }
            Assert.That((Vector2)village.FistMonument.transform.position, Is.EqualTo(new Vector2(tops[0].x, -14)));
            Assert.That((Vector2)village.JumpMapMonument.transform.position, Is.EqualTo(new Vector2(tops[2].x, -14)));
            Assert.That(platforms.All(Marked), Is.True);

            Stand(new Vector2(tops[1].x, -14));
            var player = village.World.arena.Player;
            const float step = 1f / 120;
            using (var driver = village.World.arena.AcquireManualSimulation())
            {
                driver.Step(step, new CombatInput(0, CombatButtons.Jump));
                bool doubled = false;
                for (int i = 0; i < 360; i++)
                {
                    var pressed = CombatButtons.None;
                    if (!doubled && i > 4 && player.velocity.y < 2)
                    {
                        pressed = CombatButtons.Jump;
                        doubled = true;
                    }
                    driver.Step(step, new CombatInput(0, pressed));
                    if (doubled && player.grounded) break;
                }
            }
            Assert.That(player.grounded, Is.True);
            Assert.That(player.Position.y, Is.EqualTo(tops[1].y).Within(.1f), "the player can jump from the village ground onto the display");

            Stand(new Vector2(-5.75f, -14));
            for (int i = 0; i < 3; i++) yield return null;
            village.World.RefreshCamera(0, true);
            foreach (var trophy in new[] { village.FistMonument, village.JumpMapMonument })
            {
                Assert.That(village.World.View.WorldToViewportPoint(trophy.bounds.min).x, Is.GreaterThan(0));
                Assert.That(village.World.View.WorldToViewportPoint(trophy.bounds.max).x, Is.LessThan(1));
            }
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "TemporaryGameGui");
            StartingVillageCaptureTests.Capture(gui, village, "trophy-reveal-display-sketch");
            village.SetSketch(false);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(Planks(platforms[0]).Select(plank => plank.sprite.texture), Is.EqualTo(
                wood.Select(piece => Resources.Load<Texture2D>("Hub/pixel-village/ground/color/wood/" + piece))));
            StartingVillageCaptureTests.Capture(gui, village, "trophy-reveal-display-colour");
            service.RecordSkyParkour(new SkyParkourRecord { seconds = 400 });
            yield return null;
            Assert.That(village.JumpMapMonumentTime, Is.EqualTo("03:28.417"));
            Assert.That(label.sprite, Is.SameAs(firstLabel));
            service.RecordSkyParkour(new SkyParkourRecord { seconds = 200.123f });
            yield return null;
            Assert.That(village.JumpMapMonumentTime, Is.EqualTo("03:20.123"));
            Assert.That(label.sprite, Is.Not.SameAs(firstLabel));
            store.FailNextSave = true;
            Assert.That(service.RecordSkyParkour(new SkyParkourRecord { seconds = 1 }), Is.False);
            yield return null;
            Assert.That(village.JumpMapMonumentTime, Is.EqualTo("03:20.123"));
            Chapter1ProfileHost.UseStore(store);
            yield return null; yield return null;
            Assert.That(village.JumpMapMonument.enabled, Is.True);
            Assert.That(village.JumpMapMonumentTime, Is.EqualTo("03:20.123"));
            Chapter1ProfileHost.Service.ResetProfile();
            yield return null; yield return null;
            Assert.That(village.JumpMapMonument.enabled || village.FistMonument.enabled, Is.False);
            Assert.That(label.enabled, Is.False);
            Assert.That(village.FistPlaque.enabled, Is.False);
        }
        static Vector2 Top(BoxCollider2D platform) => new Vector2(platform.bounds.center.x, platform.bounds.max.y);
        static System.Collections.Generic.IEnumerable<SpriteRenderer> Planks(BoxCollider2D platform) =>
            platform.GetComponentsInChildren<SpriteRenderer>(true).Where(part => part.name == "Monument platform wood");
        static bool Marked(BoxCollider2D platform) =>
            platform.transform.Find("Empty monument slot").GetComponent<SpriteRenderer>().enabled;

        static void AssertPlaqueFits(SpriteRenderer label, SpriteRenderer art, int[] face)
        {
            Assert.That(label.enabled && label.sprite, Is.True);
            Assert.That(label.sprite.rect.size, Is.EqualTo(new Vector2(38, 7)));
            Assert.That(label.sortingOrder, Is.GreaterThan(art.sortingOrder));
            var lowerLeft = (Vector2)art.transform.InverseTransformPoint(label.bounds.min) * 32 + art.sprite.pivot;
            var upperRight = (Vector2)art.transform.InverseTransformPoint(label.bounds.max) * 32 + art.sprite.pivot;
            Assert.That(lowerLeft.x, Is.GreaterThan(face[0]));
            Assert.That(lowerLeft.y, Is.GreaterThan(face[1]));
            Assert.That(upperRight.x, Is.LessThan(face[0] + face[2]));
            Assert.That(upperRight.y, Is.LessThan(face[1] + face[3]));
        }
        [UnityTest] public IEnumerator FallingLeftFromTheSummitOpensTheClearingOnLanding() => SummitReturn(-1);
        [UnityTest] public IEnumerator FallingRightFromTheSummitOpensTheClearingOnLanding() => SummitReturn(1);

        static IEnumerator SummitReturn(int direction)
        {
            OpenVillage();
            yield return new EnterPlayMode();
            // Match the editor review: a fresh memory profile and a warp that counts in that profile.
            var data = ProfileData.CreateNew(Guid.NewGuid().ToString());
            data.seenStories = new[] { "opening", "first-return", "artisan-first-run", "record-journal-written-0" };
            data.runsStarted = 1;
            store = new MemoryProfileStore(data);
            Chapter1ProfileHost.UseStore(store);
            for (float until = Time.unscaledTime + 25; Time.unscaledTime < until;)
            {
                village = Object.FindAnyObjectByType<StartingVillageWorld>();
                if (village && village.Ready && village.InteractionAllowed) break;
                yield return null;
            }
            Assert.That(village && village.Ready && village.InteractionAllowed, Is.True);
            var arena = village.World.arena;
            Assert.That(village.MonumentClearingOpen, Is.False);
            Assert.That(village.World.CameraMinPixels, Is.Zero);
            Assert.That(village.World.transform.Find("Monument clearing wall"), Is.Null);
            Stand(new Vector2(4, -14));
            using (var driver = arena.AcquireManualSimulation())
                for (int i = 0; i < 160; i++) driver.Step(1f / 120, new CombatInput(-1));
            Assert.That(arena.Player.Position.x, Is.GreaterThan(0), "the normal village boundary holds before the first trophy");
            int thuds = village.ThudCount;
            Assert.That(village.WarpToSkySummit(), Is.True);
            village.SkyAssisted = false;
            using (var driver = arena.AcquireManualSimulation())
            {
                for (int i = 0; i < 120 && !village.SkyCleared; i++) driver.Step(1f / 120);
                Assert.That(village.SkyCleared, Is.True);
                Assert.That(village.SkyNewBest, Is.True);
            }
            var best = Chapter1ProfileHost.Service.Snapshot.BestSkyParkour;
            Assert.That(best, Is.Not.Null);
            Assert.That(best.assisted, Is.False);
            Assert.That(best.seconds, Is.EqualTo(Mathf.Round(best.seconds * 1000) / 1000));
            yield return null;
            Assert.That(village.ThudCount, Is.EqualTo(thuds), "the summit clear itself stays quiet");
            // Leave the summit using movement input and keep the engine's fall and ground collision.
            using (var driver = arena.AcquireManualSimulation())
            {
                for (int frame = 0; frame < 400 && village.SkyCleared && village.SkyHeight > .05f; frame++)
                {
                    for (int step = 0; step < 8 && village.SkyCleared; step++)
                    {
                        float x = arena.Player.Position.x;
                        int move = direction < 0 || x < 66 ? direction : 0;
                        driver.Step(1f / 120, new CombatInput(move));
                        Assert.That(Mathf.Abs(arena.Player.Position.x - x), Is.LessThan(.75f),
                            "each fall step preserves horizontal position without warping");
                    }
                    yield return null;
                }
                for (int i = 0; i < 120 && village.SkyCleared; i++) driver.Step(1f / 120);
            }
            Assert.That(village.SkyCleared, Is.False,
                $"the player has landed on village ground: height={village.SkyHeight:F3}, grounded={arena.Player.grounded}");
            float landedX = arena.Player.Position.x;
            if (direction > 0)
            {
                Assert.That(landedX, Is.GreaterThan(village.GroundWidth + 1), "the hero lands past the village without being moved to the well");
                Assert.That(village.World.CameraRightPixels, Is.EqualTo(village.Sky.worldWidth * 32), "the far right ground stays in view after landing");
            }
            for (float until = Time.unscaledTime + 2; !village.MonumentRevealPlaying && Time.unscaledTime < until;) yield return null;
            Assert.That(village.MonumentRevealPlaying, Is.True, "landing starts the cutscene without any further movement input");
            Assert.That(arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Navigation), Is.True);
            RectTransform[] bars;
            float barsDeadline = Time.unscaledTime + 1;
            do
            {
                yield return null;
                bars = village.GetComponentsInChildren<RectTransform>(true)
                    .Where(bar => bar.name == "Upper letterbox" || bar.name == "Lower letterbox").ToArray();
            } while (bars.Count(bar => bar.gameObject.activeInHierarchy) != 2 && Time.unscaledTime < barsDeadline);
            Assert.That(bars.Count(bar => bar.gameObject.activeInHierarchy), Is.EqualTo(2));
            Assert.That(bars.All(bar => bar.sizeDelta.y > 0 && bar.sizeDelta.y < 32), Is.True, "both bars slide in at the start");
            var mark = village.transform.Find("Monument question mark").GetComponent<SpriteRenderer>();
            for (float until = Time.unscaledTime + 2; !mark.enabled && Time.unscaledTime < until;) yield return null;
            Assert.That(mark.enabled, Is.True);
            Assert.That(village.ThudCount, Is.EqualTo(thuds + 2));
            Assert.That(village.MonumentClearingOpen, Is.False, "the question mark precedes the reveal");
            Assert.That(mark.sprite.rect.size, Is.EqualTo(new Vector2(22, 28)), "the reveal uses the well cutscene reaction bubble");
            var hero = arena.Player;
            Assert.That(mark.transform.position.x, Is.EqualTo(hero.Position.x).Within(.01f));
            Assert.That(mark.transform.position.y - hero.Position.y - hero.size.y, Is.InRange(.44f, .65f),
                "the bubble sits close to the hero's head, including its entrance hop");
            Assert.That(arena.Player.Position.x, Is.EqualTo(landedX).Within(.4f), "the hero stops to react before walking");

            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "TemporaryGameGui");
            StartingVillageCaptureTests.Capture(gui, village, "trophy-reveal-cue");
            Assert.That(arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.True);
            SpriteRenderer[] smoke;
            float smokeDeadline = Time.unscaledTime + 24;
            do
            {
                yield return null;
                smoke = village.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Monument edge smoke").ToArray();
            } while (smoke.Length == 0 && Time.unscaledTime < smokeDeadline);
            Assert.That(smoke, Has.Length.EqualTo(32));
            float stopX = FistKingMonumentData.Load().revealStopX;
            Assert.That(arena.Player.Position.x, Is.EqualTo(Mathf.Min(landedX, stopX)).Within(.12f),
                "the hero stops by the record desk or stays where they landed to its left");
            Assert.That(village.World.transform.Find("Right world limit").position.x, Is.EqualTo(village.GroundWidth + .5f));
            Assert.That(mark.enabled, Is.False);
            for (float until = Time.unscaledTime + .9f; smoke.Count(r => r.enabled) <= 8 && Time.unscaledTime < until;) yield return null;
            Assert.That(smoke.Count(r => r.enabled), Is.GreaterThan(8));
            Assert.That(village.World.View.WorldToViewportPoint(smoke[0].transform.position).x, Is.InRange(-.04f, .18f));
            StartingVillageCaptureTests.Capture(gui, village, "trophy-reveal-edge-smoke");
            float heroCameraX = village.World.CameraX;
            float trophyCameraX = village.GroundLeft * 32;
            for (float until = Time.unscaledTime + 3.5f; Mathf.Abs(village.World.CameraX - trophyCameraX) > .05f && Time.unscaledTime < until;)
            {
                if (smoke.Any(r => r.enabled))
                    Assert.That(village.World.CameraX, Is.EqualTo(heroCameraX).Within(1), "smoke clears before the camera pans");
                yield return null;
            }
            Assert.That(village.World.CameraX, Is.EqualTo(trophyCameraX).Within(.05f));
            Assert.That(smoke.Any(r => r.enabled), Is.False);
            Assert.That(village.MonumentClearingOpen, Is.True);
            Assert.That(village.JumpMapMonument.enabled, Is.True);
            Assert.That(village.JumpMapMonumentTime, Is.Not.Null);
            StartingVillageCaptureTests.Capture(gui, village, "trophy-reveal-trophy-shot");
            for (float until = Time.unscaledTime + 2.3f; Time.unscaledTime < until;) yield return null;
            Assert.That(village.World.CameraX, Is.EqualTo(trophyCameraX).Within(.1f), "the trophy shot holds about 2.5 seconds");
            Assert.That(bars.Count(bar => bar.gameObject.activeInHierarchy), Is.EqualTo(2));

            bool sawBarsSlideOut = false;
            for (float until = Time.unscaledTime + 3; village.MonumentRevealPlaying && Time.unscaledTime < until;)
            {
                if (bars.All(bar => bar.sizeDelta.y > 0 && bar.sizeDelta.y < 32))
                {
                    sawBarsSlideOut = true;
                    Assert.That(village.World.CameraX, Is.EqualTo(heroCameraX).Within(1));
                    Assert.That(arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.True);
                }
                yield return null;
            }
            Assert.That(sawBarsSlideOut, Is.True, "the bars slide out after the camera returns");
            Assert.That(village.MonumentRevealPlaying, Is.False);
            Assert.That(village.World.CameraX, Is.EqualTo(heroCameraX).Within(1));
            for (float until = Time.unscaledTime + 1; bars.Any(bar => bar.gameObject.activeInHierarchy) && Time.unscaledTime < until;) yield return null;
            Assert.That(bars.Any(bar => bar.gameObject.activeInHierarchy), Is.False);
            Assert.That(arena.Controls.Current.Blocks(Deviant.Control.Contracts.InputChannels.Combat), Is.False);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen(StartingVillageWorld.MonumentClearingStory), Is.True);
            int finished = village.ThudCount;
            Chapter1ProfileHost.UseStore(store);
            yield return null; yield return null;
            Assert.That(village.MonumentClearingOpen, Is.True);
            Assert.That(village.ThudCount, Is.EqualTo(finished), "the saved reveal does not replay");
        }
    }
}
