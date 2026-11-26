using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ClockBossTimePresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }

        // The old timed enrage was a 60-segment #B66C4D ring drawn by the assembly preview.
        static bool IsTimedRing(SpriteRenderer r) => r.enabled && r.gameObject.name == "Preview art"
            && Mathf.Abs(r.color.r - 182 / 255f) < .001f && Mathf.Abs(r.color.g - 108 / 255f) < .001f
            && Mathf.Abs(r.color.b - 77 / 255f) < .001f;

        [UnityTest] public IEnumerator TwoMinutesDoNotSpawnOldEnrageArtAndMixerStillHasItsWarning()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            yield return new EnterPlayMode();
            var arena = new GameObject("Clock time arena").AddComponent<CombatArena>();
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Camera = new GameObject("Clock time camera").AddComponent<Camera>();
            content.SpriteMaterial = material; content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSpawn = new Vector2(-12, 0); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = Vector2.up;
            content.Enemies = System.Array.Empty<CombatEnemySpawn>();
            Assert.That(arena.Initialize(content), Is.True);
            using (var simulation = arena.AcquireManualSimulation())
            {
                var boss = new GameObject("Timed clock boss").AddComponent<ClockExecutionerBoss>();
                boss.Initialize(arena, arena.TechnicalSprite, -15, 0); arena.enemies.Add(boss); boss.BeginFight();
                Assert.That(boss.PreviewArtLoaded, Is.True);
                foreach (float elapsed in new[] { 119.9f, 120, 120.5f, 121.99f, 122, 180 })
                {
                    typeof(ClockExecutionerBoss).GetProperty(nameof(ClockExecutionerBoss.FightStarted)).SetValue(boss, arena.Now - elapsed);
                    boss.Present();
                    Assert.That(boss.VisiblePreviewParts, Is.GreaterThan(10));
                    Assert.That(boss.GetComponentsInChildren<SpriteRenderer>().Count(IsTimedRing), Is.Zero, "elapsed " + elapsed);
                    Assert.That(boss.FinalPhase, Is.False, "elapsed time does not enable enrage");
                }
                boss.BeginAttack(ClockBossAttack.Mixer);
                typeof(ClockExecutionerBoss).GetField("began", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(boss, arena.Now - (ClockExecutionerBoss.MixerPrepare + .1f) / boss.PatternSpeed);
                boss.Present();
                Assert.That(boss.InkTelegraphsLoaded, Is.True);
                Assert.That(boss.VisibleInkCount, Is.GreaterThan(0), "the actual mixer attack keeps its ink warning");
            }
            Object.Destroy(material);
        }
    }
}
