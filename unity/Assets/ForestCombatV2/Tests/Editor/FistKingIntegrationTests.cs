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
    public sealed class FistKingIntegrationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); Chapter1ProfileHost.UseStore(null); }
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "TemporaryGameGui");
        static void SeedProfile(bool monument, bool clearingOpened = true)
        {
            var profile = ProfileData.CreateNew(Guid.NewGuid().ToString());
            profile.seenStories = new[] { "opening", "first-return", Chapter1FirstRunInkGuide.GuideBagStory, SketchRunBinding.AttackTutorialRecord, SketchRunBinding.RewardTutorialRecord };
            if (monument && clearingOpened) profile.seenStories = profile.seenStories.Append(StartingVillageWorld.MonumentClearingStory).ToArray();
            if (monument) profile.fistKing = new FistKingRecord { runId = Guid.NewGuid().ToString(), milliseconds = 137384 };
            Chapter1ProfileHost.UseStore(new MemoryProfileStore(profile));
        }
        static void Open(string path)
        { EditorSettings.enterPlayModeOptionsEnabled = false; ForestCombatV2.Editor.DummyTrainingTools.SelectNormalMode(); EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene(path); }

        [UnityTest] public IEnumerator CompactClearingIsWalkableAndRestoresItsLeftEdgeAfterTheOpening()
        {
            Open(StartingVillageTests.ScenePath); yield return new EnterPlayMode(); SeedProfile(true);
            StartingVillageWorld village = null;
            for (int i = 0; i < 900 && !(village && village.Ready); i++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            var gui = Gui();
            if (gui) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            for (int i = 0; i < 30; i++) yield return null;
            village = Object.FindAnyObjectByType<StartingVillageWorld>();
            Assert.That(village.MonumentPlatforms.Count, Is.EqualTo(3));
            Assert.That(village.GroundLeft, Is.EqualTo(-12));
            village.RemoveOpeningCourse(); village.StationsSuppressed = false;
            var world = village.World; var player = world.arena.Player;
            Assert.That(world.CameraMinPixels, Is.Zero, "the village side retains its camera edge");
            Assert.That(village.FistMonumentTime, Is.EqualTo("02:17.384"));
            Assert.That(village.FistMonument.bounds.size.y, Is.EqualTo(2.5f).Within(.01));
            player.Position = new Vector2(1, -14); player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            using (var driver = world.arena.AcquireManualSimulation())
                for (int i = 0; i < 780; i++) driver.Step(.01f, new CombatInput(-1));
            yield return null;
            Assert.That(world.CameraMinPixels, Is.EqualTo(-12 * 32), "walking into the clearing extends the camera edge");
            Assert.That(player.Position.x, Is.LessThan(-9), "walk past the leftmost stand");
            Assert.That(player.Position.x, Is.GreaterThanOrEqualTo(-12), "the compact boundary holds");
            Assert.That(player.Position.y, Is.EqualTo(-14).Within(.1), "the extension is solid indexed terrain");
            player.Position = new Vector2(-11, -14); player.velocity = Vector2.zero; world.RefreshCamera(0, true);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(village.FistMonument.enabled, Is.True);
            string output = System.IO.Path.GetFullPath("../outputs/ralph/monument-village.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            ScreenCapture.CaptureScreenshot(output); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Chapter1ProfileHost.Service.ResetProfile(), Is.True);
            yield return null; yield return null;
            Assert.That(village.FistMonument.enabled, Is.False, "resetting a profile removes its monument");
            Assert.That(village.MonumentClearingOpen, Is.False, "resetting closes the clearing");
            Assert.That(village.MonumentPlatforms.All(platform => !platform.gameObject.activeInHierarchy), Is.True);
        }

        [UnityTest] public IEnumerator ReturningWithTheFirstFistTrophyShowsTheCueBeforeTheClearingOpens()
        {
            Open(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            SeedProfile(true, false);
            StartingVillageWorld village = null;
            for (int i = 0; i < 600 && !(village && village.Ready); i++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            var gui = Gui();
            if (gui) gui.GetType().GetMethod("SkipOpening", Private).Invoke(gui, null);
            var mark = village.transform.Find("Monument question mark").GetComponent<SpriteRenderer>();
            for (float until = Time.unscaledTime + 4; !mark.enabled && Time.unscaledTime < until;) yield return null;
            Assert.That(mark.enabled, Is.True);
            Assert.That(mark.sprite.rect.size, Is.EqualTo(new Vector2(22, 28)));
            Assert.That(village.FistMonument.enabled, Is.False, "the first record alone does not open the clearing");
            Assert.That(village.MonumentRevealPlaying, Is.False, "the reveal waits for the player near the left edge");

            var player = village.World.arena.Player;
            player.Position = new Vector2(FistKingMonumentData.Load().entranceX + 1, player.Position.y);
            player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            player.grounded = player.ProbeGround();
            for (float until = Time.unscaledTime + 3; !village.MonumentRevealPlaying && Time.unscaledTime < until;) yield return null;
            Assert.That(village.MonumentRevealPlaying, Is.True);
            for (float until = Time.unscaledTime + 9; village.MonumentRevealPlaying && Time.unscaledTime < until;) yield return null;
            Assert.That(village.MonumentRevealPlaying, Is.False);
            Assert.That(village.FistMonument.enabled, Is.True);
            var profile = Chapter1ProfileHost.Service;
            Assert.That(profile.Snapshot.HasSeen(StartingVillageWorld.MonumentClearingStory), Is.True);

            int thuds = village.ThudCount;
            Assert.That(profile.RecordSkyParkour(new SkyParkourRecord { seconds = 208.417f }), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(village.JumpMapMonument.enabled, Is.True);
            Assert.That(village.MonumentRevealPlaying, Is.False, "the second trophy shares the first reveal's completion flag");
            Assert.That(village.ThudCount, Is.EqualTo(thuds));
            Assert.That(mark.enabled, Is.False);
        }

        [UnityTest] public IEnumerator OrdinaryKillsAndStoredBlueprintStillUnlockTheFirstReactionBeforeRewards() => BossReaction(false);
        [UnityTest] public IEnumerator FasterClearShowsTheRecordReactionBeforeRewards() => BossReaction(true);
        [UnityTest] public IEnumerator EquippingThenRemovingFromSlotAWithoutAttackingSuppressesTheEasterEgg() => BossReaction(false, 0);
        [UnityTest] public IEnumerator EquippingThenRemovingFromSlotBWithoutAttackingSuppressesTheEasterEgg() => BossReaction(false, 1);
        [UnityTest] public IEnumerator CheatEquipmentRemovedBeforeAttackingStillSuppressesTheEasterEgg() => BossReaction(false, 0, true);
        static IEnumerator BossReaction(bool previousRecord, int equippedSlot = -1, bool cheatEquipment = false)
        {
            Open(Chapter1RunHost.TitleScene); yield return new EnterPlayMode(); SeedProfile(previousRecord);
            yield return null;
            var host = Chapter1RunHost.Instance;
            host.StartNewRun(Chapter1ProfileHost.Service.PlanUnarmedRun());
            ForestCombatWorld world = null; Chapter1RunBinding binding = null;
            for (int i = 0; i < 1200; i++)
            {
                yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
                if (world && world.Ready && binding && binding.Sketch && world.GetComponent<ClockExecutionerEncounter>()?.Boss) break;
            }
            Assert.That(world && world.Ready && binding && binding.Sketch, Is.True);
            Assert.That(world.GetComponent<ClockExecutionerEncounter>(), Is.Not.Null,
                $"normal boss scene ready: {world.gameObject.scene.path}, procedural={world.proceduralRooms}, village={world.villageHub}, training={world.dummyTrainingOnly}");
            var gui = Gui(); if (gui) gui.GetType().GetMethod("DismissDepartureCard")?.Invoke(gui, null);
            var guide = world.GetComponent<Chapter1FirstRunInkGuide>(); if (guide) { guide.FinishNotebook(); guide.enabled = false; }
            yield return null; yield return null;
            var arena = world.arena; var player = arena.Player; var encounter = world.GetComponent<ClockExecutionerEncounter>(); var boss = encounter.Boss;
            var arsenal = binding.Run.Sketch.Arsenal;
            var weapon = arsenal.Acquire(binding.Run.Sketch.Planner.RollWeapon(1, 3, new System.Random(7)));
            arsenal.Confirm(weapon.Id);
            Assert.That(binding.Run.FistKing.Eligible, Is.True, "acquiring and confirming a blueprint is allowed");
            if (equippedSlot >= 0)
            {
                // No attack or intervening frame: the real equipment command must retain this history.
                if (cheatEquipment)
                {
                    arena.Sketch.DebugConfigureWeapon(equippedSlot, weapon.Form, 0);
                    arena.Sketch.DebugConfigureWeapon(equippedSlot, null, 0);
                }
                else { arsenal.Equip(weapon.Id, equippedSlot); arsenal.Unequip(equippedSlot); }
                Assert.That(binding.Run.FistKing.HasEquippedWeapon, Is.True);
            }
            // Avoid capturing iterator locals in a lambda: EnterPlayMode reloads this test's iterator state.
            TestEnemy ordinary = null;
            foreach (var enemy in arena.enemies)
                if (enemy && Chapter1EnemyRole.Of(enemy) == Chapter1EnemyRole.Normal) { ordinary = enemy; break; }
            Assert.That(ordinary, Is.Not.Null);
            foreach (var enemy in arena.enemies) if (enemy) { enemy.aggressive = false; enemy.CancelAttack(); if (enemy != boss) enemy.Position += Vector2.right * 1000; }
            // A real started fist action supplies the provenance; this fixture amplifies its accepted damage to
            // finish the real boss quickly. It checks integration, not full-fight balance or the player's record.
            using (var driver = arena.AcquireManualSimulation())
            {
                for (int i = 0; i < 60 && !player.grounded; i++) driver.Step(.01f);
                driver.Step(.01f, new CombatInput(0, CombatButtons.Weapon1Basic));
                var cast = player.currentCast; Assert.That(cast, Is.Not.Null);
                var origin = (CombatHitOrigin)typeof(CombatCast).GetMethod("HitOrigin", Private).Invoke(cast, new object[] { 0, CombatHitKind.Melee });
                Assert.That(origin.Stamp.FormId, Is.EqualTo("unarmed"));
                Assert.That(ordinary.Receive(ordinary.maxHealth, player, 1, true, null, true, false, origin), Is.True);
                boss.BeginFight();
                Assert.That(boss.Receive(boss.maxHealth, player, 1, true, null, true, false, origin), Is.True);
            }
            if (equippedSlot >= 0)
            {
                for (int i = 0; i < 30; i++) yield return null;
                Assert.That(binding.Run.Kills, Is.EqualTo(2));
                Assert.That(binding.FistKingChallenge.RecordSaved, Is.False);
                Assert.That(binding.FistKingChallenge.ReactionPending, Is.False);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.FistKing, Is.Null);
                yield break;
            }
            for (int i = 0; i < 180 && !binding.FistKingChallenge.RecordSaved; i++) yield return null;
            Assert.That(binding.Run.Kills, Is.EqualTo(2));
            Assert.That(binding.FistKingChallenge.RecordSaved, Is.True);
            Assert.That(Chapter1ProfileHost.Service.Snapshot.FistKing, Is.Not.Null);
            Assert.That(binding.FistKingChallenge.ReactionLine, Is.EqualTo(previousRecord ? "주먹왕, 강림" : "나... 주먹왕일지도?"));
            Assert.That(binding.Sketch.RewardModalShown, Is.False, "the reaction precedes the reward modal");
            for (int i = 0; i < 600 && !binding.FistKingChallenge.ReactionVisible; i++) yield return null;
            Assert.That(binding.FistKingChallenge.ReactionVisible, Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(previousRecord ? "../outputs/ralph/fist-king-record-reaction.png" : "../outputs/ralph/fist-king-reaction.png"));
            yield return new WaitForSecondsRealtime(.3f);
            for (int i = 0; i < 600 && binding.FistKingChallenge.ReactionPending; i++) yield return null;
            Assert.That(binding.FistKingChallenge.ReactionPending, Is.False);
            Assert.That(binding.Sketch.RewardModalShown, Is.True, "existing boss rewards resume after the reaction");
        }
    }
}
