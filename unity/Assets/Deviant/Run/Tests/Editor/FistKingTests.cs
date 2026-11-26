using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class FistKingTests
    {
        static ProgressionCatalog Catalog() => new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));
        static ProgressionService Service(MemoryProfileStore store = null) => new ProgressionService(Catalog(), store ?? new MemoryProfileStore());
        static RunSession Start(ProgressionService service)
        {
            var run = new RunFlow(() => 42).StartNewRun(0, plan: service.PlanUnarmedRun());
            run.StartClock(0); return run;
        }
        static FistKingClear Clear(RunSession run, double seconds) => run.FistKing.Complete(seconds);
        static SketchArsenal Arsenal(RunSession run)
        {
            var arsenal = new SketchArsenal(_ => "red");
            arsenal.Changed += () => run.FistKing.ObserveEquipment(arsenal.HasEquippedWeapon);
            return arsenal;
        }

        [Test] public void FirstClearHasNoTimeLimitAndReloadsThePersistentRecord()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            Assert.That(service.RecordFistKing(Clear(Start(service), 812.345), out var first), Is.True);
            Assert.That(first, Is.True);
            Assert.That(Service(store).Snapshot.FistKing.milliseconds, Is.EqualTo(812345));
            Assert.That(FistKingRecord.Format(service.Snapshot.FistKing.milliseconds), Is.EqualTo("13:32.345"));
        }
        [Test] public void OnlyStrictlyFasterMillisecondsReplaceTheRecord()
        {
            var service = Service();
            service.RecordFistKing(Clear(Start(service), 200), out _);
            var slower = Clear(Start(service), 220);
            var equal = Clear(Start(service), 200.0009);
            long revision = service.Snapshot.Revision;
            Assert.That(service.RecordFistKing(slower, out _), Is.False);
            Assert.That(service.RecordFistKing(equal, out _), Is.False);
            Assert.That(service.Snapshot.Revision, Is.EqualTo(revision));
            Assert.That(service.RecordFistKing(Clear(Start(service), 199.999), out var first), Is.True);
            Assert.That(first, Is.False);
            Assert.That(service.Snapshot.FistKing.milliseconds, Is.EqualTo(199999));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(75)] public void OrdinaryEnemyKillsDoNotDisqualifyTheAttempt(int kills)
        {
            var run = Start(Service());
            for (int i = 0; i < kills; i++) run.RecordKill();
            Assert.That(Clear(run, 123), Is.Not.Null);
        }
        [TestCase("stored")] [TestCase("discarded")] [TestCase("destroyed")]
        public void AcquiringAndConfirmingWithoutEquippingIsAllowed(string after)
        {
            var run = Start(Service()); var arsenal = Arsenal(run);
            var weapon = arsenal.Acquire("base_sword", "test", new string[] { null });
            arsenal.Confirm(weapon.Id);
            if (after == "discarded") arsenal.Discard(weapon.Id);
            if (after == "destroyed") arsenal.Destroy(weapon.Id);
            Assert.That(run.FistKing.HasEquippedWeapon, Is.False);
            Assert.That(Clear(run, 123), Is.Not.Null);
        }
        [TestCase(0, "unequip")] [TestCase(1, "unequip")]
        [TestCase(0, "destroy")] [TestCase(1, "destroy")]
        [TestCase(0, "discard")] [TestCase(1, "discard")]
        public void EitherSlotEquippedEvenWithoutAttackingStaysDisqualifiedAfterLoss(int slot, string after)
        {
            var run = Start(Service()); var arsenal = Arsenal(run);
            var weapon = arsenal.Acquire("base_sword", "test", new string[] { null });
            arsenal.Confirm(weapon.Id); arsenal.Equip(weapon.Id, slot);
            if (after == "destroy") arsenal.Destroy(weapon.Id);
            else { arsenal.Unequip(slot); if (after == "discard") arsenal.Discard(weapon.Id); }
            Assert.That(arsenal.Equipped(0), Is.Null); Assert.That(arsenal.Equipped(1), Is.Null);
            run.FistKing.ObserveEquipment(false);
            Assert.That(run.FistKing.HasEquippedWeapon, Is.True);
            Assert.That(Clear(run, 123), Is.Null);
            Assert.That(Clear(Start(Service()), 123), Is.Not.Null, "a new run starts with a fresh flag");
        }
        [Test] public void ObservingAfterEquipAndUnequipStillReadsTheLifetimeFlag()
        {
            var arsenal = new SketchArsenal(_ => "red");
            var weapon = arsenal.Acquire("base_sword", "test", new string[] { null });
            arsenal.Confirm(weapon.Id); arsenal.Equip(weapon.Id, 1); arsenal.Unequip(1);
            var run = Start(Service()); run.FistKing.ObserveEquipment(arsenal.HasEquippedWeapon);
            Assert.That(Clear(run, 123), Is.Null);
        }
        [Test] public void FailedEquipDoesNotCountAsHavingEquippedAWeapon()
        {
            var run = Start(Service()); var arsenal = Arsenal(run);
            var weapon = arsenal.Acquire("base_sword", "test", new string[] { null });
            Assert.Throws<InvalidOperationException>(() => arsenal.Equip(weapon.Id, 0));
            Assert.That(Clear(run, 123), Is.Not.Null);
        }
        [Test] public void CombatResetAndTrainingRemainExcluded()
        {
            var run = Start(Service()); run.FistKing.Invalidate();
            Assert.That(Clear(run, 123), Is.Null);
            run = new RunFlow(() => 42).StartNewRun(0, plan: Service().PlanUnarmedRun(training: true));
            run.StartClock(0);
            Assert.That(Clear(run, 123), Is.Null);
        }
        [Test] public void PreselectedStartingWeaponsAlreadyCountAsEquipped()
        {
            var service = Service(); var blueprints = service.Catalog.Data.startLoadout.availableBlueprints;
            var run = new RunFlow(() => 42).StartNewRun(0, plan: service.PlanRun(new[] { blueprints[0], blueprints[1] }));
            run.StartClock(0);
            Assert.That(run.FistKing.HasEquippedWeapon, Is.True);
            Assert.That(Clear(run, 123), Is.Null);
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(0d)] [TestCase(-1d)]
        public void InvalidClearTimesCannotSaveARecord(double seconds)
        {
            Assert.That(Clear(Start(Service()), seconds), Is.Null);
        }
        [Test] public void DirectEditorEntryAndChapterTwoNeverUnlockTheMonument()
        {
            var flow = new RunFlow(() => 42); var run = flow.StartNewRun(0); run.StartClock(0);
            Assert.That(Clear(run, 123), Is.Null);
            run = flow.StartDebugChapter(0, 2); run.StartClock(0);
            Assert.That(Clear(run, 123), Is.Null);
        }
        [Test] public void SaveFailureLeavesNoUnlockOrNewBestAndTheSameProofCanRetry()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            var clear = Clear(Start(service), 123.456); store.FailNextSave = true;
            Assert.That(service.RecordFistKing(clear, out _), Is.False);
            Assert.That(service.Snapshot.FistKing, Is.Null);
            Assert.That(service.RecordFistKing(clear, out var first), Is.True);
            Assert.That(first, Is.True);
            Assert.That(service.RecordFistKing(clear, out _), Is.False);
            Assert.That(service.RecordFistKing(Clear(Start(Service()), 1), out _), Is.False, "a different profile's proof");
        }
        [Test] public void SnapshotCannotBeMutatedAndProfileResetRemovesTheMonument()
        {
            var service = Service(); var run = Start(service); var proof = Clear(run, 137.384);
            service.RecordFistKing(proof, out _);
            service.Snapshot.FistKing.milliseconds = 1;
            Assert.That(service.Snapshot.FistKing.milliseconds, Is.EqualTo(137384));
            Assert.That(run.FistKing.Complete(1), Is.Null);
            Assert.That(service.ResetProfile(), Is.True);
            Assert.That(service.Snapshot.FistKing, Is.Null);
        }
        [Test] public void OldProfilesDefaultToLockedAndInvalidRecordsAreRejected()
        {
            var data = ProfileData.CreateNew(Guid.NewGuid().ToString());
            Assert.That(Service(new MemoryProfileStore(data)).Snapshot.FistKing, Is.Null);
            var roundTrip = JsonUtility.FromJson<ProfileData>(JsonUtility.ToJson(data));
            Assert.That(ProfileData.Validate(roundTrip, Catalog()), Is.Empty);
            var loaded = Service(new MemoryProfileStore(roundTrip));
            Assert.That(loaded.Snapshot.FistKing, Is.Null);
            Assert.That(loaded.RecordFistKing(Clear(Start(loaded), 123), out var first), Is.True);
            Assert.That(first, Is.True);
            data.fistKing = new FistKingRecord { runId = "bad", milliseconds = -1 };
            Assert.That(ProfileData.Validate(data, Catalog()), Is.Not.Empty);
        }
    }
}
