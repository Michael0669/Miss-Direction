using System.Collections.Generic;
using UnityEngine;

namespace MissDirection.Fortunes
{
    public enum FortuneKind { Bad, Good, Mixed, Role }
    public enum FortuneCategory { Movement, Vision, Voice, Hud, Role, Special }
    public enum FortuneRarity { Common, Rare, Cursed, Golden }

    /// <summary>
    /// One Fortune card. Designers make new Fortunes as assets (Create > Miss Direction > Fortune)
    /// and stack effects on them; no code needed unless the Fortune needs a brand-new behavior.
    /// </summary>
    [CreateAssetMenu(menuName = "Miss Direction/Fortune", fileName = "Fortune_")]
    public class FortuneDefinition : ScriptableObject
    {
        [Header("Card")]
        public string id = "fortune.new";
        public string displayName = "New Fortune";
        [TextArea] public string cardRule = "One plain-language rule for the back of the card.";
        public Sprite cardArt;
        public AudioClip printChime;

        [Header("Dealing")]
        public FortuneKind kind = FortuneKind.Bad;
        public FortuneCategory category = FortuneCategory.Movement;
        public FortuneRarity rarity = FortuneRarity.Common;
        [Min(0f)] public float weight = 1f;

        [Tooltip("How many players may hold this per floor, per 5 players (1 = max 2 in a 10-player game). 0 = no cap.")]
        [Min(0)] public int capPerFivePlayers;

        [Header("Effects")]
        public List<FortuneEffect> effects = new();
    }
}
