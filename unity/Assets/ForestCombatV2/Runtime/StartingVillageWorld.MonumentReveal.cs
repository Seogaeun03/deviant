using System.Collections.Generic;
using Deviant.Control.Contracts;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        public const string MonumentClearingStory = "monument-clearing-revealed";
        const float NoticeSecondThudAt = .22f, NoticeEndsAt = 2.35f;
        const float GateApproach = 2.75f;
        const float LandingThudAt = .12f, LandingQuestionAt = .38f, LandingWalkAt = 1.55f;
        const float SmokeSeconds = 1.65f, PanSeconds = 1.05f, TrophyHoldSeconds = 2.5f;
        const float BarTransitionSeconds = .32f;
        enum MonumentShot { Landing, Walk, Smoke, PanToTrophies, HoldTrophies, PanToHero, ReleaseBars }
        Transform monumentLeftLimit;
        SpriteRenderer monumentQuestion;
        static Sprite monumentQuestionSprite;
        Sprite monumentSmokeSprite;
        IControlScope monumentRevealScope;
        IControlLease monumentCombatLock;
        readonly List<(SpriteRenderer art, Vector2 start, Vector2 velocity, float delay)> monumentDust = new List<(SpriteRenderer, Vector2, Vector2, float)>();
        string monumentNoticeProfile, monumentOpenedProfile;
        float monumentNoticeAge = -1, monumentRevealAge, monumentBarsAge;
        float monumentHeroCameraX, monumentCameraY;
        int monumentRumbles;
        bool monumentRevealOnLanding;
        MonumentShot monumentShot;

        public bool MonumentPresentationAllowed { get; set; }
        public bool MonumentRevealPlaying => monumentRevealScope != null;
        public bool MonumentClearingOpen { get; private set; }
        public float MonumentBarsProgress => !MonumentRevealPlaying ? 0 : Mathf.SmoothStep(0, 1,
            monumentShot == MonumentShot.ReleaseBars ? 1 - monumentRevealAge / BarTransitionSeconds : monumentBarsAge / BarTransitionSeconds);

        void BuildMonumentNotice()
        {
            if (!monumentQuestionSprite) monumentQuestionSprite = BuildReactionBubble(tail: true, question: true);
            monumentQuestion = Renderer("Monument question mark", monumentQuestionSprite, 8);
            monumentQuestion.enabled = false;
        }

        Sprite MonumentSmokeSprite()
        {
            if (monumentSmokeSprite) return monumentSmokeSprite;
            const int size = 9, center = size / 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x - center, dy = y - center;
                int distance = dx * dx + dy * dy;
                if (distance < 20)
                    pixels[y * size + x] = new Color32(255, 255, 255, distance < 12 ? (byte)255 : (byte)140);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Monument smoke", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            monumentSmokeSprite = Make(texture, Vector2.one * .5f);
            return monumentSmokeSprite;
        }

        void TickMonumentReveal(ProgressionService service)
        {
            var snapshot = service.Snapshot;
            bool hasTrophy = snapshot.FistKing != null || snapshot.BestSkyParkour != null;
            bool opened = hasTrophy && (snapshot.HasSeen(MonumentClearingStory) || monumentOpenedProfile == snapshot.ProfileId);
            MonumentClearingOpen = opened || (MonumentRevealPlaying && monumentShot >= MonumentShot.PanToTrophies);
            MoveMonumentBoundary();
            if (MonumentRevealPlaying)
            {
                if (hasTrophy) { AdvanceMonumentReveal(service); return; }
                EndMonumentReveal();
            }
            RestoreMonumentCameraBounds();

            var player = World.arena.Player;
            if (!hasTrophy || opened || CourseBuilt || !InteractionAllowed || !MonumentPresentationAllowed ||
                SkyRunActive || SkyCleared || !player.grounded || Mathf.Abs(player.Position.y - GroundTop) > .12f)
            {
                monumentQuestion.enabled = false;
                if (!hasTrophy)
                {
                    monumentNoticeAge = -1;
                    monumentNoticeProfile = null;
                    monumentOpenedProfile = null;
                    monumentRevealOnLanding = false;
                }
                return;
            }

            if (monumentRevealOnLanding)
            {
                BeginMonumentReveal(MonumentShot.Landing);
                return;
            }

            if (monumentNoticeProfile != snapshot.ProfileId)
            {
                // The village return cue waits until the hero is in view.
                var at = World.View.WorldToViewportPoint(player.visual.bounds.center);
                if (at.x <= 0 || at.x >= 1 || at.y < .08f || at.y > .85f) return;
                monumentNoticeProfile = snapshot.ProfileId;
                monumentNoticeAge = 0;
                Thud(.8f);
                World.ShakeCamera(.12f, .18f);
            }
            float previous = monumentNoticeAge;
            monumentNoticeAge += Time.unscaledDeltaTime;
            if (previous < NoticeSecondThudAt && monumentNoticeAge >= NoticeSecondThudAt)
            {
                Thud(1f);
                World.ShakeCamera(.18f, .35f);
            }
            monumentQuestion.enabled = monumentNoticeAge >= NoticeSecondThudAt && monumentNoticeAge < NoticeEndsAt;
            if (monumentQuestion.enabled)
                PlaceReactionBubble(monumentQuestion, HeroReactionHead, monumentNoticeAge - NoticeSecondThudAt, NoticeEndsAt - NoticeSecondThudAt);
            if (monumentNoticeAge >= .75f && player.Position.x <= monumentData.entranceX + GateApproach)
                BeginMonumentReveal(MonumentShot.Smoke);
        }

        void BeginMonumentReveal(MonumentShot firstShot)
        {
            var player = World.arena.Player;
            player.velocity = Vector2.zero;
            player.facing = -1;
            monumentRevealOnLanding = false;
            monumentQuestion.enabled = false;
            monumentBarsAge = 0;
            monumentRevealScope = World.arena.Controls.OpenScope("monument clearing reveal");
            monumentRevealScope.BlockInput(InputChannels.Interaction | InputChannels.Navigation);
            monumentCombatLock = monumentRevealScope.BlockInput(InputChannels.Combat);
            StartMonumentShot(firstShot);
        }

        void StartMonumentShot(MonumentShot shot)
        {
            monumentRevealAge = 0;
            monumentShot = shot;
            if (shot == MonumentShot.Smoke) EmitMonumentSmoke();
        }

        void EmitMonumentSmoke()
        {
            monumentRumbles = 0;
            var random = new System.Random(733);
            float leftEdge = World.View.ViewportToWorldPoint(new Vector3(.015f, 0, 0)).x;
            for (int i = 0; i < 32; i++)
            {
                var art = Renderer("Monument edge smoke", MonumentSmokeSprite(), 4);
                art.transform.localScale = Vector3.one * random.Next(2, 6);
                var start = new Vector2(leftEdge + (float)random.NextDouble() * .9f,
                    GroundTop + .15f + (float)random.NextDouble() * .85f);
                art.transform.position = start;
                art.enabled = false;
                monumentDust.Add((art, start, new Vector2(.35f + (float)random.NextDouble() * 2,
                    1 + (float)random.NextDouble() * 3), (float)random.NextDouble() * .44f));
            }
        }

        void AdvanceMonumentReveal(ProgressionService service)
        {
            float previous = monumentRevealAge;
            monumentRevealAge += Time.unscaledDeltaTime;
            monumentBarsAge += Time.unscaledDeltaTime;
            switch (monumentShot)
            {
                case MonumentShot.Landing:
                    if (previous < LandingThudAt && monumentRevealAge >= LandingThudAt)
                    {
                        Thud(.8f);
                        World.ShakeCamera(.12f, .18f);
                    }
                    if (previous < LandingQuestionAt && monumentRevealAge >= LandingQuestionAt)
                    {
                        Thud(1f);
                        World.ShakeCamera(.18f, .35f);
                    }
                    if (monumentRevealAge < LandingWalkAt)
                    {
                        monumentQuestion.enabled = monumentRevealAge >= LandingQuestionAt;
                        if (monumentQuestion.enabled)
                            PlaceReactionBubble(monumentQuestion, HeroReactionHead, monumentRevealAge - LandingQuestionAt, LandingWalkAt - LandingQuestionAt);
                        return;
                    }
                    monumentQuestion.enabled = false;
                    float stopX = monumentData.revealStopX;
                    if (World.arena.Player.Position.x > stopX)
                    {
                        StartMonumentShot(MonumentShot.Walk);
                        // The scripted walk owns simulation input. The normal combat block would also discard its steps.
                        monumentCombatLock.Dispose();
                        monumentCombatLock = null;
                        StartScriptedWalk(float.NaN, stopX, 18f, FinishMonumentWalk);
                    }
                    else StartMonumentShot(MonumentShot.Smoke);
                    break;
                case MonumentShot.Walk:
                    break;
                case MonumentShot.Smoke:
                    TickMonumentSmoke();
                    if (monumentRevealAge < SmokeSeconds) return;
                    monumentHeroCameraX = World.CameraX;
                    monumentCameraY = -(World.CameraY + 270) / 32;
                    StartMonumentShot(MonumentShot.PanToTrophies);
                    break;
                case MonumentShot.PanToTrophies:
                    SetMonumentCamera(Mathf.SmoothStep(monumentHeroCameraX, GroundLeft * 32, monumentRevealAge / PanSeconds));
                    if (monumentRevealAge >= PanSeconds) StartMonumentShot(MonumentShot.HoldTrophies);
                    break;
                case MonumentShot.HoldTrophies:
                    if (monumentRevealAge >= TrophyHoldSeconds) StartMonumentShot(MonumentShot.PanToHero);
                    break;
                case MonumentShot.PanToHero:
                    SetMonumentCamera(Mathf.SmoothStep(GroundLeft * 32, monumentHeroCameraX, monumentRevealAge / PanSeconds));
                    if (monumentRevealAge >= PanSeconds) StartMonumentShot(MonumentShot.ReleaseBars);
                    break;
                case MonumentShot.ReleaseBars:
                    if (monumentRevealAge >= BarTransitionSeconds) FinishMonumentReveal(service);
                    break;
            }
        }

        void FinishMonumentWalk()
        {
            monumentCombatLock = monumentRevealScope.BlockInput(InputChannels.Combat);
            var player = World.arena.Player;
            if (player.Position.x < monumentData.revealStopX)
            {
                player.Position = new Vector2(monumentData.revealStopX, player.Position.y);
                Physics2D.SyncTransforms();
            }
            player.velocity = Vector2.zero;
            StartMonumentShot(MonumentShot.Smoke);
        }

        void SetMonumentCamera(float leftPixels)
        {
            World.CameraMinPixels = GroundLeft * 32;
            World.FixedCameraCenter = new Vector2((leftPixels + 480) / 32, monumentCameraY);
            World.RefreshCamera(0, true);
        }

        void TickMonumentSmoke()
        {
            if (monumentRumbles < 5 && monumentRevealAge >= monumentRumbles * .16f)
            {
                Thud(Mathf.Lerp(.58f, 1f, monumentRumbles / 4f));
                World.ShakeCamera(.2f + .04f * monumentRumbles, .21f);
                monumentRumbles++;
            }
            foreach (var mote in monumentDust)
            {
                float age = monumentRevealAge - mote.delay;
                mote.art.enabled = age >= 0 && age < 1f;
                if (!mote.art.enabled) continue;
                Vector2 position = mote.start + mote.velocity * age + Vector2.down * (1.1f * age * age);
                mote.art.transform.position = new Vector3(Mathf.Round(position.x * 32) / 32, Mathf.Round(position.y * 32) / 32, 0);
                mote.art.color = new Color(.61f, .56f, .48f, (1 - age) * .65f);
            }
        }

        void FinishMonumentReveal(ProgressionService service)
        {
            monumentOpenedProfile = service.Snapshot.ProfileId;
            if (!service.MarkStorySeen(MonumentClearingStory) && !service.Snapshot.HasSeen(MonumentClearingStory))
                Debug.LogWarning("The monument clearing reveal was not saved: " + service.LastSaveError);
            MonumentClearingOpen = true;
            EndMonumentReveal();
            RestoreMonumentCameraBounds();
        }

        void MoveMonumentBoundary()
        {
            if (!monumentLeftLimit || CourseBuilt) return;
            float edge = MonumentClearingOpen ? GroundLeft : monumentData.entranceX;
            var at = monumentLeftLimit.position;
            if (Mathf.Approximately(at.x, edge - .5f)) return;
            monumentLeftLimit.position = new Vector3(edge - .5f, at.y, at.z);
            Physics2D.SyncTransforms();
        }

        void RestoreMonumentCameraBounds()
        {
            if (CourseBuilt || monumentData == null || !World.arena || !World.arena.Player) return;
            float entrance = monumentData.entranceX;
            float visit = MonumentClearingOpen ? Mathf.InverseLerp(entrance, entrance - 2, World.arena.Player.Position.x) : 0;
            World.CameraMinPixels = Mathf.Lerp(entrance, GroundLeft, visit) * 32;
        }

        void EndMonumentReveal()
        {
            if (MonumentRevealPlaying)
            {
                if (monumentShot == MonumentShot.Walk) StopScriptedWalk();
                if (World && monumentShot >= MonumentShot.PanToTrophies) World.FixedCameraCenter = null;
            }
            monumentRevealScope?.Dispose();
            monumentRevealScope = null;
            monumentCombatLock = null;
            if (monumentQuestion) monumentQuestion.enabled = false;
            foreach (var mote in monumentDust) if (mote.art) Destroy(mote.art.gameObject);
            monumentDust.Clear();
        }

        void DisposeMonumentReveal()
        {
            EndMonumentReveal();
            if (monumentSmokeSprite)
            {
                Destroy(monumentSmokeSprite.texture);
                Destroy(monumentSmokeSprite);
            }
        }
    }
}
