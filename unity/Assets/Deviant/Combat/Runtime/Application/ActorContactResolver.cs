using System;
using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    public static class ActorContactResolver
    {
        // Covers the native contact offset a grounded body can rest above its floor.
        const float CeilingSeam = .02f;
        public static ActorAxisMotion ResolveAxis(ActorBodyFrame body, float distance, bool horizontal, IActorSurfaceQuery world) => Resolve(body, distance, horizontal, world, false);
        public static ActorAxisMotion ResolveSolidAxis(ActorBodyFrame body, float distance, bool horizontal, IActorSurfaceQuery world) => Resolve(body, distance, horizontal, world, true);
        static ActorAxisMotion Resolve(ActorBodyFrame body, float distance, bool horizontal, IActorSurfaceQuery world, bool solidOnly)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Check(distance);
            if (Math.Abs(distance) < .000001f) return new ActorAxisMotion(0, 0, false, true);
            float sign = distance >= 0 ? 1 : -1, x = (horizontal ? 1f : 0f) * sign, y = (horizontal ? 0f : 1f) * sign;
            float length = Math.Abs(distance), allowed = length;
            var hits = world.BoxCast(new ActorBoxCast(body.CenterX, body.CenterY, body.Width - .005f, body.Height - .005f, x, y, length + .003f))
                ?? throw new InvalidOperationException("Actor surface query returned no contact collection.");
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.Ignored && !solidOnly) continue;
                if (hit.OneWay && (solidOnly || horizontal || distance >= 0 || body.Y < hit.Top - .04f)) continue;
                // A flush floor seam must not become a side wall through the cast's
                // contact tolerance (notably when leaving a one-way platform).
                if (horizontal && hit.Top <= body.CenterY - body.Height / 2 + .003f) continue;
                // Likewise a ceiling flush with the head: a 2u body fits a 2u gap (the map
                // rules assume it), so walking off a ledge under a 2u overhang is not a wall,
                // and a ceiling resting on the head never stops a fall.
                if ((horizontal || distance < 0) && hit.Bottom >= body.CenterY + body.Height / 2 - CeilingSeam) continue;
                float normalSquared = hit.NormalX * hit.NormalX + hit.NormalY * hit.NormalY;
                float dot = hit.NormalX * x + hit.NormalY * y;
                if (normalSquared > .1f && dot > -.1f) continue;
                allowed = Math.Min(allowed, Math.Max(0, hit.Distance - .002f));
            }
            return new ActorAxisMotion(x * allowed, y * allowed, allowed + .0001f < length, false);
        }
        public static bool ProbeGround(ActorBodyFrame body, float velocityY, IActorSurfaceQuery world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world)); Check(velocityY);
            if (velocityY > .05f) return false;
            var hits = world.BoxCast(new ActorBoxCast(body.X + body.OffsetX * body.Facing, body.Y + .025f, body.Width - .08f, .04f, 0, -1, .055f))
                ?? throw new InvalidOperationException("Actor surface query returned no contact collection.");
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (!hit.Ignored && hit.NormalY > .8f && body.Y >= hit.Top - .06f) return true;
            }
            return false;
        }
        static void Check(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
