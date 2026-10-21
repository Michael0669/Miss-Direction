using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// The pool the machine draws from. Rolls bad / good / mixed first (80 / 15 / 5 by default),
    /// then picks by weight inside that bucket, skipping Fortunes that hit their per-floor cap.
    /// </summary>
    [CreateAssetMenu(menuName = "Miss Direction/Fortune Deck", fileName = "FortuneDeck")]
    public class FortuneDeck : ScriptableObject
    {
        public List<FortuneDefinition> fortunes = new();

        [Range(0f, 1f)] public float badChance = 0.80f;
        [Range(0f, 1f)] public float goodChance = 0.15f;

        public FortuneDefinition Draw(System.Random rng, int teamSize, IReadOnlyDictionary<FortuneDefinition, int> dealtThisFloor)
        {
            float roll = (float)rng.NextDouble();
            var bucket = roll < badChance ? FortuneKind.Bad
                : roll < badChance + goodChance ? FortuneKind.Good
                : FortuneKind.Mixed;

            return DrawWhere(rng, teamSize, dealtThisFloor, f => InBucket(f, bucket))
                ?? DrawWhere(rng, teamSize, dealtThisFloor, _ => true);
        }

        public static bool IsCapped(FortuneDefinition fortune, int teamSize, IReadOnlyDictionary<FortuneDefinition, int> dealt)
        {
            if (fortune.capPerFivePlayers <= 0 || dealt == null) return false;
            int cap = fortune.capPerFivePlayers * Mathf.CeilToInt(Mathf.Max(1, teamSize) / 5f);
            return dealt.TryGetValue(fortune, out int count) && count >= cap;
        }

        static bool InBucket(FortuneDefinition fortune, FortuneKind bucket) =>
            bucket == FortuneKind.Bad
                ? fortune.kind == FortuneKind.Bad || fortune.kind == FortuneKind.Role
                : fortune.kind == bucket;

        FortuneDefinition DrawWhere(System.Random rng, int teamSize,
            IReadOnlyDictionary<FortuneDefinition, int> dealt, Func<FortuneDefinition, bool> filter)
        {
            float total = 0f;
            foreach (var f in fortunes)
                if (Eligible(f, teamSize, dealt, filter)) total += f.weight;

            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            FortuneDefinition last = null;
            foreach (var f in fortunes)
            {
                if (!Eligible(f, teamSize, dealt, filter)) continue;
                last = f;
                pick -= f.weight;
                if (pick <= 0f) return f;
            }
            return last;
        }

        static bool Eligible(FortuneDefinition f, int teamSize,
            IReadOnlyDictionary<FortuneDefinition, int> dealt, Func<FortuneDefinition, bool> filter) =>
            f != null && f.weight > 0f && filter(f) && !IsCapped(f, teamSize, dealt);
    }
}
