using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Run.Tests
{
    public sealed class SkyParkourMonumentRecordTests
    {
        static ProgressionCatalog Catalog() => new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));
        static ProgressionService Service(MemoryProfileStore store = null) => new ProgressionService(Catalog(), store ?? new MemoryProfileStore());
        static SkyParkourRecord Record(float seconds, bool assisted = false, long at = 1) => new SkyParkourRecord { seconds = seconds, assisted = assisted, at = at };

        [Test] public void PracticeClearsStayInHistoryWithoutUnlockingTheMonument()
        {
            var service = Service();
            Assert.That(service.RecordSkyParkour(Record(12, true)), Is.True);
            Assert.That(service.Snapshot.SkyParkourRecords.Count, Is.EqualTo(1));
            Assert.That(service.Snapshot.BestSkyParkour, Is.Null);
            Assert.That(service.RecordSkyParkour(Record(208.417f)), Is.True);
            Assert.That(service.Snapshot.BestSkyParkour.seconds, Is.EqualTo(208.417f));
        }
        [Test] public void EqualSlowerAndPracticeAttemptsNeverReplaceTheBest()
        {
            var service = Service(); service.RecordSkyParkour(Record(208.417f, at: 11));
            service.RecordSkyParkour(Record(208.417f, at: 12)); service.RecordSkyParkour(Record(300, at: 13));
            service.RecordSkyParkour(Record(1, true, 14));
            Assert.That(service.Snapshot.BestSkyParkour.at, Is.EqualTo(11));
            service.RecordSkyParkour(Record(208.416f, at: 15));
            Assert.That(service.Snapshot.BestSkyParkour.at, Is.EqualTo(15));
        }
        [Test] public void AllTimeBestSurvivesHistoryRolloverAndJsonReload()
        {
            var store = new MemoryProfileStore(); var service = Service(store);
            service.RecordSkyParkour(Record(88.123f));
            for (int i = 0; i < 60; i++) service.RecordSkyParkour(Record(200 + i, i % 2 == 0));
            Assert.That(service.Snapshot.SkyParkourRecords.Count, Is.EqualTo(50));
            var data = store.Load(_ => null, out _);
            var reloaded = Service(new MemoryProfileStore(JsonUtility.FromJson<ProfileData>(JsonUtility.ToJson(data))));
            Assert.That(reloaded.Snapshot.BestSkyParkour.seconds, Is.EqualTo(88.123f));
            reloaded.Snapshot.BestSkyParkour.seconds = 1;
            Assert.That(reloaded.Snapshot.BestSkyParkour.seconds, Is.EqualTo(88.123f));
        }
        [Test] public void LegacyHistorySeedsThePermanentBestBeforeItRollsOver()
        {
            var data = ProfileData.CreateNew(Guid.NewGuid().ToString()); data.skyParkourRecords = new[] { Record(42.12f) };
            var service = Service(new MemoryProfileStore(data));
            Assert.That(service.Snapshot.BestSkyParkour.seconds, Is.EqualTo(42.12f));
            for (int i = 0; i < 51; i++) service.RecordSkyParkour(Record(100));
            Assert.That(service.Snapshot.BestSkyParkour.seconds, Is.EqualTo(42.12f));
        }
        [Test] public void FailedSaveKeepsThePreviousBestAndResetClearsIt()
        {
            var store = new MemoryProfileStore(); var service = Service(store); service.RecordSkyParkour(Record(50));
            var before = service.Snapshot; store.FailNextSave = true;
            Assert.That(service.RecordSkyParkour(Record(20)), Is.False);
            Assert.That(service.Snapshot, Is.SameAs(before));
            Assert.That(service.Snapshot.BestSkyParkour.seconds, Is.EqualTo(50));
            Assert.That(service.ResetProfile(), Is.True);
            Assert.That(Service(store).Snapshot.BestSkyParkour, Is.Null);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidTimesCannotCreateAPermanentMonument(float seconds)
        { Assert.That(Service().RecordSkyParkour(Record(seconds)), Is.False); }
    }
}
