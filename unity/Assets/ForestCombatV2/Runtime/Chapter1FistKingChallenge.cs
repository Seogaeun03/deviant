using System;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class FistKingMonumentData
    {
        public int schemaVersion;
        public string firstLine, recordLine, sprite;
        public float reactionSeconds, height, platformHeight, entranceX, revealStopX;
        public float[] slotXs;
        public int slotIndex, platformWidth;
        public float[] spriteRect;
        public int[] plaqueRect;
        public JumpMapMonumentData jumpMap;
        public static FistKingMonumentData Load()
        {
            var source = Resources.Load<TextAsset>("FistKingMonument");
            if (!source) throw new InvalidOperationException("FistKingMonument catalog is missing.");
            return JsonUtility.FromJson<FistKingMonumentData>(source.text);
        }
    }

    [Serializable] public sealed class JumpMapMonumentData
    {
        public string sprite;
        public float height;
        public int slotIndex;
        public float[] spriteRect;
        public int[] plaqueRect;
    }

    // Observes run-long equipment history and the boss's killing-hit time. Rewards wait for the hero reaction.
    public sealed class Chapter1FistKingChallenge : MonoBehaviour
    {
        Chapter1RunBinding binding;
        CombatArena arena;
        SketchArsenal arsenal;
        TestEnemy defeatedBoss;
        FistKingClear pendingSave;
        FistKingMonumentData data;
        float deathTime, lastBossHitTime = -1, reactionAge, nextSave;
        bool resolved;
        public string ReactionLine { get; private set; }
        public bool ReactionPending => ReactionLine != null;
        public bool ResolutionPending => defeatedBoss && !resolved;
        public bool ReactionVisible { get; private set; }
        public float ReactionAge => reactionAge;
        public bool RecordSaved { get; private set; }
        public void Initialize(Chapter1RunBinding owner) { binding = owner; data = FistKingMonumentData.Load(); }
        public void Observe(CombatArena current)
        {
            if (arena != current)
            {
                if (arena) { arena.AcceptedHit -= OnHit; arena.ResetCompleted -= OnReset; binding.Run.FistKing.Invalidate(); }
                arena = current;
                arena.AcceptedHit += OnHit; arena.ResetCompleted += OnReset;
            }
            var next = binding.Run.Sketch?.Arsenal;
            if (arsenal != next)
            {
                if (arsenal != null) arsenal.Changed -= ObserveWeapons;
                arsenal = next;
                if (arsenal != null) arsenal.Changed += ObserveWeapons;
            }
            ObserveWeapons();
            var start = binding.World.GetComponent<Chapter1StartingBlueprints>();
            if (start && start.SelectedCount > 0) binding.Run.FistKing.ObserveEquipment(true);
            if (binding.World.dummyTrainingOnly) binding.Run.FistKing.Invalidate();
        }
        void ObserveWeapons() => binding.Run.FistKing.ObserveEquipment(arsenal?.HasEquippedWeapon ?? false);
        void OnReset() { binding.Run.FistKing.Invalidate(); ReactionLine = null; ReactionVisible = false; }
        void OnHit(CombatAcceptedHit hit)
        {
            if (!binding.Run.FistKing.Eligible || !hit.After.Dead || hit.Before.Health <= hit.After.Health) return;
            foreach (var enemy in arena.enemies)
            {
                if (!enemy || enemy.ActorId != hit.Target.ActorId || Chapter1EnemyRole.Of(enemy) != Chapter1EnemyRole.Boss) continue;
                lastBossHitTime = hit.Now;
                return;
            }
        }
        public void BossDefeated(TestEnemy boss) { defeatedBoss = boss; deathTime = lastBossHitTime >= 0 ? lastBossHitTime : arena.Now; }
        public void Tick()
        {
            if (!arena) return;
            if (!resolved && defeatedBoss)
            {
                resolved = true; ObserveWeapons();
                pendingSave = binding.Run.FistKing.Complete(deathTime);
            }
            if (pendingSave != null && Time.unscaledTime >= nextSave)
            {
                var service = Chapter1ProfileHost.Service;
                var best = service.Snapshot.FistKing;
                if (pendingSave.ProfileId != service.Snapshot.ProfileId || best != null && pendingSave.Milliseconds >= best.milliseconds) pendingSave = null;
                else if (service.RecordFistKing(pendingSave, out bool first))
                { RecordSaved = true; ReactionLine = first ? data.firstLine : data.recordLine; pendingSave = null; }
                else nextSave = Time.unscaledTime + 1;
            }
            ReactionVisible = false;
            if (arena.Player.Dead) { ReactionLine = null; return; }
            if (!ReactionPending || arena.Controls.Current.SimulationPaused) return;
            var boss = binding.World.GetComponent<ClockExecutionerEncounter>();
            if (boss && (boss.DeathCutscene || !boss.Defeated)) return;
            reactionAge += Time.unscaledDeltaTime;
            if (reactionAge >= data.reactionSeconds) { ReactionLine = null; return; }
            ReactionVisible = true;
        }
        void OnDestroy()
        {
            if (arsenal != null) arsenal.Changed -= ObserveWeapons;
            if (arena) { arena.AcceptedHit -= OnHit; arena.ResetCompleted -= OnReset; }
        }
    }
}
