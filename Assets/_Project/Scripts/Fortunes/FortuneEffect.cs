using MissDirection.Player;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// Base for anything a Fortune does to a player. Apply when the card is grabbed,
    /// Remove when it burns away. Subclass this for behaviors that aren't simple modifiers
    /// (voice filters, the Navigator's map, Giant scaling).
    /// </summary>
    public abstract class FortuneEffect : ScriptableObject
    {
        public abstract void Apply(PlayerModifiers target);
        public abstract void Remove(PlayerModifiers target);
    }
}
