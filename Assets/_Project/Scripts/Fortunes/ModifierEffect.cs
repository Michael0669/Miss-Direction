using MissDirection.Player;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>The common effect: add one value to one player modifier (invert, flip, delay, gravity...).</summary>
    [CreateAssetMenu(menuName = "Miss Direction/Effects/Modifier Effect", fileName = "Effect_")]
    public class ModifierEffect : FortuneEffect
    {
        public ModifierType type;

        [Tooltip("1 for on/off modifiers. Seconds for InputDelay, a count for ExtraJumps, a multiplier for scales.")]
        public float value = 1f;

        public override void Apply(PlayerModifiers target) => target.Apply(type, value);
        public override void Remove(PlayerModifiers target) => target.Remove(type, value);
    }
}
