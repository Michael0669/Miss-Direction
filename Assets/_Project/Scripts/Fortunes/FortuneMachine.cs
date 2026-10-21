using System.Collections;
using System.Collections.Generic;
using MissDirection.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// Madame Direction's cabinet, run by the host. One press at a time: flash red or gold,
    /// read the fortune, print a card for whoever pressed. Pressing while she's busy shocks you.
    /// Every player sees the same reading and the same card; only the presser can take it.
    /// The floor's elevator should wait for <see cref="AllCardsClaimed"/>.
    /// </summary>
    public class FortuneMachine : NetworkBehaviour
    {
        public enum MachineState { Ready, Reading, Revealed, CardWaiting, Done }

        [SerializeField] FortuneDeck deck;
        [SerializeField] FortuneCard cardPrefab;
        [SerializeField] Transform cardSlot;

        [Header("Timing (target: about 3 seconds per player)")]
        [SerializeField, Min(0f)] float revealSeconds = 0.4f;
        [SerializeField, Min(0f)] float readingSeconds = 2.2f;

        [Header("Hooks for lights, sounds and animation (run on every player's game)")]
        public UnityEvent onReadingStarted;
        public UnityEvent onBadReveal;
        public UnityEvent onGoodReveal;
        public UnityEvent onCardPrinted;
        public UnityEvent onOutOfTurnShock;
        public UnityEvent onAllCardsClaimed;

        // Synced to everyone (written by the host)
        NetworkVariable<bool> busy = new();
        NetworkVariable<bool> revealed = new();
        NetworkVariable<bool> lastGood = new();
        NetworkVariable<int> teamSize = new(1);
        NetworkList<ulong> pressedIds;
        NetworkList<ulong> claimedIds;

        // Host only
        readonly Dictionary<FortuneDefinition, int> dealt = new();
        readonly Dictionary<int, (ulong owner, int fortuneIndex)> openCards = new();
        System.Random rng = new();
        int nextCardId = 1;

        // Every player's game
        readonly Dictionary<int, FortuneCard> localCards = new();

        public bool IsBusy => busy.Value;
        public bool HasRevealed => busy.Value && revealed.Value;
        public bool LastRevealGood => lastGood.Value;
        public bool HasDeck => deck != null && deck.fortunes.Count > 0;
        public int TeamSize => Mathf.Max(1, teamSize.Value);
        public int ClaimedCount => claimedIds?.Count ?? 0;
        public bool AllCardsClaimed => ClaimedCount >= TeamSize;

        public MachineState State
        {
            get
            {
                if (busy.Value) return revealed.Value ? MachineState.Revealed : MachineState.Reading;
                if (pressedIds != null && pressedIds.Count > claimedIds.Count) return MachineState.CardWaiting;
                if (AllCardsClaimed) return MachineState.Done;
                return MachineState.Ready;
            }
        }

        public bool HasPressed(ulong clientId) => pressedIds != null && pressedIds.Contains(clientId);
        public bool HasClaimed(ulong clientId) => claimedIds != null && claimedIds.Contains(clientId);

        void Awake()
        {
            pressedIds = new NetworkList<ulong>();
            claimedIds = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            BeginFloor(Random.Range(int.MinValue, int.MaxValue));
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            foreach (var card in localCards.Values)
                if (card != null) Destroy(card.gameObject);
            localCards.Clear();
        }

        // ---------- called by players on their own game ----------

        /// <summary>The local player pressed the button.</summary>
        public void RequestPress()
        {
            if (!IsSpawned) return;
            if (!HasDeck)
            {
                Debug.LogError("Fortune Machine has no deck assigned. Run Miss Direction > Build Test Playground Scene again, " +
                               "or drag StarterDeck onto the FortuneMachine's Deck field.", this);
                return;
            }
            PressRpc();
        }

        /// <summary>The local player grabbed their card.</summary>
        public void RequestClaim(int cardId)
        {
            if (IsSpawned) ClaimRpc(cardId);
        }

        /// <summary>Start the floor over: everyone's Fortunes are cleared and the machine is ready again.</summary>
        public void RequestReset()
        {
            if (IsSpawned) ResetRpc();
        }

        // ---------- host ----------

        /// <summary>Reset for a new floor. Host only.</summary>
        public void BeginFloor(int seed)
        {
            if (!IsServer) return;
            StopAllCoroutines();
            rng = new System.Random(seed);
            dealt.Clear();
            openCards.Clear();
            pressedIds.Clear();
            claimedIds.Clear();
            busy.Value = false;
            revealed.Value = false;
            teamSize.Value = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
        }

        [Rpc(SendTo.Server)]
        void PressRpc(RpcParams rpcParams = default)
        {
            ulong presser = rpcParams.Receive.SenderClientId;

            if (busy.Value)
            {
                ShockRpc(presser);
                return;
            }
            if (pressedIds.Contains(presser)) return; // one card per player per floor

            StartCoroutine(ReadFortune(presser));
        }

        IEnumerator ReadFortune(ulong presser)
        {
            busy.Value = true;
            revealed.Value = false;
            pressedIds.Add(presser);

            var fortune = deck.Draw(rng, TeamSize, dealt);
            int fortuneIndex = fortune != null ? deck.fortunes.IndexOf(fortune) : -1;
            ReadingStartedRpc();

            yield return new WaitForSeconds(revealSeconds);
            lastGood.Value = fortune != null && fortune.kind == FortuneKind.Good;
            revealed.Value = true;
            RevealRpc(lastGood.Value);

            yield return new WaitForSeconds(readingSeconds);

            bool stillHere = IsConnected(presser);
            if (fortuneIndex >= 0 && stillHere)
            {
                dealt.TryGetValue(fortune, out int count);
                dealt[fortune] = count + 1;

                int cardId = nextCardId++;
                openCards[cardId] = (presser, fortuneIndex);
                PrintCardRpc(cardId, fortuneIndex, presser);
            }
            else
            {
                pressedIds.Remove(presser);
            }

            revealed.Value = false;
            busy.Value = false;
        }

        [Rpc(SendTo.Server)]
        void ClaimRpc(int cardId, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!openCards.TryGetValue(cardId, out var card) || card.owner != sender) return;

            openCards.Remove(cardId);
            if (!claimedIds.Contains(sender)) claimedIds.Add(sender);
            CardClaimedRpc(cardId, sender, card.fortuneIndex);

            if (AllCardsClaimed) AllCardsClaimedRpc();
        }

        [Rpc(SendTo.Server)]
        void ResetRpc()
        {
            BeginFloor(Random.Range(int.MinValue, int.MaxValue));
            FloorResetRpc();
        }

        void OnClientConnected(ulong clientId) =>
            teamSize.Value = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);

        void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            var leftover = new List<int>();
            foreach (var pair in openCards)
                if (pair.Value.owner == clientId) leftover.Add(pair.Key);
            foreach (int cardId in leftover)
            {
                openCards.Remove(cardId);
                RemoveCardRpc(cardId);
            }

            pressedIds.Remove(clientId);
            claimedIds.Remove(clientId);

            int remaining = 0;
            foreach (var id in NetworkManager.ConnectedClientsIds)
                if (id != clientId) remaining++;
            teamSize.Value = Mathf.Max(1, remaining);
        }

        bool IsConnected(ulong clientId)
        {
            foreach (var id in NetworkManager.ConnectedClientsIds)
                if (id == clientId) return true;
            return false;
        }

        // ---------- every player's game ----------

        [Rpc(SendTo.ClientsAndHost)]
        void ShockRpc(ulong clientId)
        {
            onOutOfTurnShock?.Invoke();
            if (NetworkManager.LocalClientId == clientId && PlayerNetwork.Local != null && PlayerNetwork.Local.Grab != null)
                PlayerNetwork.Local.Grab.ReleaseAll();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ReadingStartedRpc() => onReadingStarted?.Invoke();

        [Rpc(SendTo.ClientsAndHost)]
        void RevealRpc(bool good)
        {
            if (good) onGoodReveal?.Invoke();
            else onBadReveal?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PrintCardRpc(int cardId, int fortuneIndex, ulong owner)
        {
            var fortune = GetFortune(fortuneIndex);
            if (fortune == null || cardPrefab == null) return;

            var slot = cardSlot != null ? cardSlot : transform;
            var card = Instantiate(cardPrefab, slot.position, slot.rotation);
            card.Init(fortune, this, owner, cardId);
            localCards[cardId] = card;
            onCardPrinted?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void CardClaimedRpc(int cardId, ulong owner, int fortuneIndex)
        {
            DestroyLocalCard(cardId);
            var fortune = GetFortune(fortuneIndex);
            if (fortune != null && PlayerNetwork.TryGet(owner, out var player) && player.Fortunes != null)
                player.Fortunes.Grant(fortune);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void RemoveCardRpc(int cardId) => DestroyLocalCard(cardId);

        [Rpc(SendTo.ClientsAndHost)]
        void AllCardsClaimedRpc() => onAllCardsClaimed?.Invoke();

        [Rpc(SendTo.ClientsAndHost)]
        void FloorResetRpc()
        {
            foreach (var card in localCards.Values)
                if (card != null) Destroy(card.gameObject);
            localCards.Clear();

            foreach (var player in PlayerNetwork.All.Values)
                if (player != null && player.Fortunes != null) player.Fortunes.ClearAll();
        }

        void DestroyLocalCard(int cardId)
        {
            if (localCards.TryGetValue(cardId, out var card) && card != null) Destroy(card.gameObject);
            localCards.Remove(cardId);
        }

        FortuneDefinition GetFortune(int index) =>
            deck != null && index >= 0 && index < deck.fortunes.Count ? deck.fortunes[index] : null;
    }
}
