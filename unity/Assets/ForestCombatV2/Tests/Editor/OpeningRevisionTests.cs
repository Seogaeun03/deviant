using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class OpeningRevisionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test] public void InteractionKeyIsWhiteAtNativeSize()
        {
            var key = KeycapMarker.SpriteFor('F');
            Assert.That(key.rect.size, Is.EqualTo(new Vector2(17, 18)));
            Assert.That(key.texture.filterMode, Is.EqualTo(FilterMode.Point));
            // The face corners are white rather than the former beige artwork.
            // The generated still sprite is non-readable after upload, so inspect it through a render readback.
            Assert.That(key.name, Is.EqualTo("White interaction key"));
        }

        [UnityTest] public IEnumerator FirstDepartureGatesAndDodgePractice()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            yield return ExercisePractice();
            yield return new ExitPlayMode();
        }

        // Closures are created after EnterPlayMode's domain reload, not serialized across it.
        static IEnumerator ExercisePractice()
        {
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int f = 0; f < 600; f++)
            {
                yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>();
                if (village != null && village.Ready) break;
            }
            Assert.That(village && village.Ready, Is.True);
            village.SendMessage("Update");
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ServiceHub)), Is.False, "no upgrade interaction before the first run");
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReturnWell)), Is.False, "well waits for the first boss artisan explanation");
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.RecordDesk)), Is.True, "optional records remain available");
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            var guiType = gui.GetType();
            gui.enabled = false;
            guiType.GetMethod("CloseNotebook", Private).Invoke(gui, null);
            guiType.GetMethod("HideBubble", Private).Invoke(gui, null);
            guiType.GetMethod("LockOpening", Private).Invoke(gui, new object[] { false });
            guiType.GetMethod("HideControls", Private).Invoke(gui, null);
            village.SetSketch(false); village.SendMessage("TickSkyArt");
            var sky = village.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Sky stone");
            var colour = sky.sprite; var position = sky.transform.position;
            village.SetSketch(true); village.SendMessage("TickSkyArt");
            Assert.That(sky.sprite, Is.Not.SameAs(colour), "sky path drains after the opening");
            Assert.That(sky.transform.position, Is.EqualTo(position));
            Assert.That(sky.sprite.rect.size, Is.EqualTo(colour.rect.size));
            village.ClearDemos();
            village.BuildOpeningCourse(-170, new[]
            {
                new StartingVillageWorld.CoursePiece { kind = "dodgeGate", x = -143, width = 3, top = 32 },
                new StartingVillageWorld.CoursePiece { kind = "dodgeGate", x = -123, width = 4, top = 32 }
            });
            var demo = village.PlayDemo("dodge", new[] { "Shift" }, "대시 무적");
            Assert.That(demo, Is.Not.Null); Assert.That(demo.ShotShown, Is.False, "a virtual shot never overlaps practice");
            Assert.That(village.DodgeGateOpen, Is.False);
            var arena = village.World.arena; var player = arena.Player;
            player.Position = new Vector2(-151, village.WalkingLine); player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            village.SetSketch(false); village.World.FixedCameraCenter = new Vector2(-150, village.WalkingLine + 4);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            guiType.GetField("openingActive", Private).SetValue(gui, true);
            guiType.GetMethod("TickControls", Private).Invoke(gui, null);
            yield return null; village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            StartingVillageCaptureTests.Capture(gui, village, "revision-dodge-closed");
            // Walking into a shot does not release the gate.
            var tick = village.GetType().GetMethod("TickPracticeShots", Private);
            for (int i = 0; i < 60; i++) tick.Invoke(village, new object[] { .05f });
            Assert.That(village.PracticeHits, Is.GreaterThan(0)); Assert.That(village.DodgeGateOpen, Is.False);
            player.Position = new Vector2(-151, village.WalkingLine); player.velocity = Vector2.zero;
            village.GetType().GetMethod("ClearPracticeShots", Private).Invoke(village, null);
            // Let a live shot pass right through without dashing.
            player.EncounterProtection = () => true;
            for (int i = 0; i < 90; i++) tick.Invoke(village, new object[] { .05f });
            Assert.That(village.PracticeDodges, Is.Zero, "ordinary invulnerability is not a successful dash");
            player.EncounterProtection = null;
            player.SetMovementAbilities(MovementAbilities.All); // The live road grants these before the practice zone.
            var shots = (System.Collections.IList)village.GetType().GetField("practiceShots", Private).GetValue(village);
            const float step = .02f;
            bool firstGateTested = false;
            using (var driver = arena.AcquireManualSimulation())
            {
                // Dash at each incoming projectile using regular movement input and let the shots travel naturally.
                for (int i = 0; i < 800 && !village.DodgeGateOpen; i++)
                {
                    bool dash = false;
                    foreach (var shot in shots)
                    {
                        float distance = (float)shot.GetType().GetField("x").GetValue(shot) - player.Center.x;
                        if (distance > 0 && distance < 1.8f) { dash = true; break; }
                    }
                    bool crossFirstGate = firstGateTested && player.Position.x < -131;
                    driver.Step(step, dash ? new CombatInput(1, CombatButtons.Dash) :
                        crossFirstGate ? new CombatInput(1, 0) : default);
                    tick.Invoke(village, new object[] { step });
                    if (!firstGateTested && village.PracticeDodges == 1)
                    {
                        var gates = village.World.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name == "Opening course wall");
                        Assert.That(gates.Any(c => c.bounds.Contains(new Vector3(-142, village.WalkingLine + 1, 0))), Is.False);
                        Assert.That(gates.Any(c => c.bounds.Contains(new Vector3(-121, village.WalkingLine + 1, 0))), Is.True);
                        Assert.That(village.PracticeShotsFlying, Is.Zero);
                        for (int wait = 0; wait < 90; wait++) tick.Invoke(village, new object[] { step });
                        Assert.That(village.PracticeShotsFlying, Is.Zero, "the next shot waits until the hero passes the first gate");
                        firstGateTested = true;
                    }
                }
            }
            Assert.That(firstGateTested, Is.True);
            Assert.That(village.PracticeDodges, Is.EqualTo(2));
            Assert.That(village.DodgeGateOpen, Is.True);
            Assert.That(village.PracticeShotsFlying, Is.Zero, "no live shot after the final dodge");
            var walls = village.World.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name == "Opening course wall").ToArray();
            Assert.That(walls.Any(c => c.bounds.Contains(new Vector3(-121, village.WalkingLine + 1, 0))), Is.False, "the bottom passage opens");
            Assert.That(walls.Any(c => c.bounds.Contains(new Vector3(-121, village.WalkingLine + 6, 0))), Is.True, "the upper wall remains");
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            guiType.GetMethod("TickControls", Private).Invoke(gui, null);
            StartingVillageCaptureTests.Capture(gui, village, "revision-dodge-open");
            guiType.GetMethod("OpenControlsWindow", Private).Invoke(gui, null);
            guiType.GetMethod("TickControlsWindowPicture", Private).Invoke(gui, new object[] { .8f });
            StartingVillageCaptureTests.Capture(gui, village, "revision-controls-window");
            guiType.GetMethod("CloseControlsWindow", Private).Invoke(gui, null);
            var stage = (RectTransform)guiType.GetField("stage", Private).GetValue(gui);
            var loadingRoot = new GameObject("Loading review", typeof(RectTransform)).GetComponent<RectTransform>();
            loadingRoot.SetParent(stage, false); loadingRoot.anchorMin = loadingRoot.anchorMax = new Vector2(0, 1); loadingRoot.pivot = new Vector2(0, 1); loadingRoot.sizeDelta = new Vector2(960, 540);
            var backdrop = loadingRoot.gameObject.AddComponent<UnityEngine.UI.Image>(); backdrop.color = new Color32(15, 16, 17, 255);
            var view = ChapterLoadingView.For(LoadingPlace.Chapter1);
            view.Build(loadingRoot, new Vector2(480, 240), LoadingHeroRun.Capture()); view.Tick(1);
            StartingVillageCaptureTests.Capture(gui, village, "revision-loading"); view.Dispose();
            Object.Destroy(loadingRoot.gameObject);
            guiType.GetField("openingActive", Private).SetValue(gui, false);
            var service = Chapter1ProfileHost.Service;
            Assert.That(service.RecordChapterOneClear(service.PlanUnarmedRun().RunId, false), Is.True);
            village.SendMessage("Update");
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReturnWell)), Is.False, "boss clear alone does not open the well");
            service.MarkStorySeen("artisan-visit-1"); village.SendMessage("Update");
            Assert.That(village.StationOpen(village.Find(StartingVillageWorld.ReturnWell)), Is.True, "the artisan explanation opens the well");
            village.RemoveOpeningCourse(); village.ClearPreview();
            var well = village.Find(StartingVillageWorld.ReturnWell); player.Position = well.Position + Vector2.right;
            village.World.FixedCameraCenter = new Vector2(well.Position.x, village.WalkingLine + 4);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            yield return null;
            // Without an ink to pour the well has no F at all (playtest 2026-11-13): no keycap, no bubble.
            Assert.That(village.WellHasInk, Is.False);
            Assert.That(village.NearestStation, Is.Null, "the well offers nothing without a carried ink");
            StartingVillageCaptureTests.Capture(gui, village, "revision-well-idle");
        }
    }
}
