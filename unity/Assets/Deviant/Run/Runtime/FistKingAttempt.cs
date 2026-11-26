using System;
using System.Globalization;

namespace Deviant.Run
{
    [Serializable] public sealed class FistKingRecord
    {
        public string runId;
        public long milliseconds;
        // JsonUtility can materialize a null inline serializable class as a zero-valued object on reload.
        public bool IsEmpty => milliseconds == 0 && string.IsNullOrEmpty(runId);
        public FistKingRecord Clone() => new FistKingRecord { runId = runId, milliseconds = milliseconds };
        public static string Format(long milliseconds)
        {
            long ms = Math.Max(0, milliseconds);
            return (ms / 60000).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (ms / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + "." +
                (ms % 1000).ToString("000", CultureInfo.InvariantCulture);
        }
    }

    // Produced once by a chapter-one attempt, never reconstructed from the currently equipped slots.
    public sealed class FistKingClear
    {
        public Guid RunId { get; }
        public string ProfileId { get; }
        public long Milliseconds { get; }
        internal FistKingClear(Guid run, string profile, long milliseconds)
        { RunId = run; ProfileId = profile; Milliseconds = milliseconds; }
    }

    // Uses the simulation clock (pause time is absent). Only equipment history decides the combat restriction;
    // kills and bag acquisitions are allowed. Invalid runs/resets remain excluded separately.
    public sealed class FistKingAttempt
    {
        readonly Guid runId;
        readonly string profileId;
        bool eligible, started, finished;
        double startedAt;
        public bool HasEquippedWeapon { get; private set; }
        public bool Eligible => eligible && !HasEquippedWeapon && !finished;
        public FistKingClear Clear { get; private set; }
        internal FistKingAttempt(Guid run, RunStartPlan plan, int chapter)
        {
            runId = run; profileId = plan?.ProfileId;
            eligible = chapter == 1 && plan != null && !plan.Training;
            HasEquippedWeapon = plan != null && !plan.Unarmed;
        }
        public void Start(double now) { if (!started && !finished) { started = true; startedAt = now; } }
        public void Invalidate() { eligible = false; }
        public void ObserveEquipment(bool hasEquippedWeapon) { HasEquippedWeapon |= hasEquippedWeapon; }
        public FistKingClear Complete(double now)
        {
            if (finished) return null;
            finished = true;
            double seconds = now - startedAt;
            if (!eligible || HasEquippedWeapon || !started || !(seconds > 0) || double.IsInfinity(seconds) ||
                seconds >= long.MaxValue / 1000d) return null;
            long milliseconds = (long)Math.Floor(seconds * 1000 + .00001);
            if (milliseconds <= 0) return null;
            return Clear = new FistKingClear(runId, profileId, milliseconds);
        }
    }
}
