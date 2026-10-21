using System.Collections;
using System.Collections.Generic;
using MissDirection.Player;
using UnityEngine;
using UnityEngine.Events;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// Madame Direction's cabinet. One press at a time: flash red or gold, read the fortune,
    /// print a card reserved for whoever pressed. Pressing while she's busy gets you shocked.
    /// The floor's elevator should wait for <see cref="AllCardsClaimed"/>.
    /// </summary>
    public class FortuneMachine : MonoBehaviour
    {
        [SerializeField] FortuneDeck deck;
        [SerializeField] FortuneCard cardPrefab;
        [SerializeField] Transform cardSlot;
        [SerializeField] int teamSize = 4;

        [Header("Timing (target: about 3 seconds per player)")]
        [SerializeField, Min(0f)] float revealSeconds = 0.4f;
        [SerializeField, Min(0f)] float readingSeconds = 2.2f;
        [SerializeField] float cardEjectSpeed = 1.5f;

        [Header("Hooks for lights, sounds and animation")]
        public UnityEvent onReadingStarted;
        public UnityEvent onBadReveal;
        public UnityEvent onGoodReveal;
        public UnityEvent onCardPrinted;
        public UnityEvent onOutOfTurnShock;
        public UnityEvent onAllCardsClaimed;

        readonly Dictionary<FortuneDefinition, int> dealt = new();
        readonly HashSet<PlayerFortunes> pressed = new();
        readonly HashSet<PlayerFortunes> claimed = new();
        System.Random rng = new();

        public bool IsBusy { get; private set; }
        public int TeamSize => teamSize;
        public bool AllCardsClaimed => claimed.Count >= teamSize;

        /// <summary>Reset for a new floor. Use a shared seed so every client deals the same cards.</summary>
        public void BeginFloor(int playerCount, int seed)
        {
            StopAllCoroutines();
            teamSize = Mathf.Max(1, playerCount);
            rng = new System.Random(seed);
            dealt.Clear();
            pressed.Clear();
            claimed.Clear();
            IsBusy = false;
        }

        public bool TryPress(PlayerFortunes presser)
        {
            if (presser == null || deck == null) return false;

            if (IsBusy)
            {
                onOutOfTurnShock?.Invoke();
                presser.SendMessage("OnFortuneShock", SendMessageOptions.DontRequireReceiver);
                return false;
            }

            if (pressed.Contains(presser)) return false; // one card per player per floor

            StartCoroutine(ReadFortune(presser));
            return true;
        }

        IEnumerator ReadFortune(PlayerFortunes presser)
        {
            IsBusy = true;
            pressed.Add(presser);

            var fortune = deck.Draw(rng, teamSize, dealt);
            onReadingStarted?.Invoke();

            yield return new WaitForSeconds(revealSeconds);
            if (fortune != null && fortune.kind == FortuneKind.Good) onGoodReveal?.Invoke();
            else onBadReveal?.Invoke();

            yield return new WaitForSeconds(readingSeconds);
            if (fortune != null)
            {
                dealt.TryGetValue(fortune, out int count);
                dealt[fortune] = count + 1;
                PrintCard(fortune, presser);
            }

            IsBusy = false;
        }

        void PrintCard(FortuneDefinition fortune, PlayerFortunes owner)
        {
            if (cardPrefab == null)
            {
                // No card prefab assigned: grant straight away so the loop still works.
                owner.Grant(fortune);
                NotifyClaimed(owner);
                return;
            }

            var slot = cardSlot != null ? cardSlot : transform;
            var card = Instantiate(cardPrefab, slot.position, slot.rotation);
            card.Init(fortune, this, owner);
            if (card.TryGetComponent(out Rigidbody body))
                body.linearVelocity = slot.forward * cardEjectSpeed;

            onCardPrinted?.Invoke();
        }

        internal void NotifyClaimed(PlayerFortunes player)
        {
            claimed.Add(player);
            if (AllCardsClaimed) onAllCardsClaimed?.Invoke();
        }
    }
}
