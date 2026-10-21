using System;
using System.Collections.Generic;
using MissDirection.Fortunes;
using UnityEngine;

namespace MissDirection.Player
{
    /// <summary>The Fortunes a player currently holds. Granting applies their effects; clearing removes them.</summary>
    [RequireComponent(typeof(PlayerModifiers))]
    public class PlayerFortunes : MonoBehaviour
    {
        readonly List<FortuneDefinition> active = new();
        PlayerModifiers modifiers;

        public IReadOnlyList<FortuneDefinition> Active => active;

        public event Action<FortuneDefinition> Granted;
        public event Action Cleared;

        void Awake() => modifiers = GetComponent<PlayerModifiers>();

        public void Grant(FortuneDefinition fortune)
        {
            if (fortune == null) return;
            active.Add(fortune);
            foreach (var effect in fortune.effects)
                if (effect != null) effect.Apply(modifiers);

            Debug.Log($"{name} drew {fortune.displayName}: {fortune.cardRule}", this);
            Granted?.Invoke(fortune);
        }

        /// <summary>Called when a floor ends: the cards burn away.</summary>
        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                foreach (var effect in active[i].effects)
                    if (effect != null) effect.Remove(modifiers);

            active.Clear();
            Cleared?.Invoke();
        }
    }
}
