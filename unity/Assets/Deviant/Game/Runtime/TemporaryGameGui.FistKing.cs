using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        MaintenanceBubbleView fistKingBubble;
        void RefreshFistKingBubble(bool hudShown)
        {
            var challenge = run ? run.FistKingChallenge : null;
            if (!hudShown || !challenge || !challenge.ReactionVisible || !world || !world.View)
            { if (fistKingBubble) fistKingBubble.Hide(); return; }
            if (!fistKingBubble) { fistKingBubble = MaintenanceBubbleView.Create(world.View); fistKingBubble.name = "Fist king reaction"; }
            string line = challenge.ReactionLine;
            int count = Mathf.Clamp(Mathf.FloorToInt(challenge.ReactionAge * 24) + 1, 1, line.Length);
            var player = host.Arena.Player;
            fistKingBubble.Show(line, count, null, true, player.Position + Vector2.up * (player.size.y + .4f), Time.unscaledTime, false);
        }
        void DisposeFistKingBubble() { if (fistKingBubble) Destroy(fistKingBubble.gameObject); }
    }
}
