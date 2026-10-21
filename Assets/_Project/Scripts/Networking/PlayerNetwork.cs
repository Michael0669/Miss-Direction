using System.Collections.Generic;
using MissDirection.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace MissDirection.Networking
{
    /// <summary>
    /// The networked side of a player. The owner keeps their camera, input, grabbing and HUD;
    /// everyone else's copy of this player is just a body with a name tag.
    /// Movement is owner-authoritative (NetworkTransform set to Owner), so it feels instant.
    /// The host gives each player the lowest free number (1 to 10), which sets their name and
    /// color, so someone who leaves and rejoins gets their old number back.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetwork : NetworkBehaviour
    {
        static readonly Dictionary<ulong, PlayerNetwork> Players = new();

        static readonly Color[] Palette =
        {
            new(0.10f, 0.75f, 0.75f), // teal
            new(1.00f, 0.50f, 0.10f), // orange
            new(0.95f, 0.30f, 0.65f), // pink
            new(0.45f, 0.90f, 0.25f), // lime
            new(0.30f, 0.55f, 1.00f), // blue
            new(1.00f, 0.85f, 0.20f), // yellow
            new(0.65f, 0.40f, 1.00f), // purple
            new(0.90f, 0.20f, 0.20f), // red
            new(0.95f, 0.95f, 0.95f), // white
            new(0.55f, 0.35f, 0.20f), // brown
        };

        [Tooltip("Camera rig and anything else only the owner should have. Destroyed on other players' copies.")]
        [SerializeField] GameObject localOnlyRoot;
        [Tooltip("Scripts only the owner should run (controller, grab, input, HUD).")]
        [SerializeField] Behaviour[] localOnlyBehaviours;
        [Tooltip("The player's visible body. Shown to others; only its shadow is shown to the owner.")]
        [SerializeField] Renderer[] bodyRenderers;
        [Tooltip("Renderers tinted with this player's color.")]
        [SerializeField] Renderer[] tintedRenderers;
        [SerializeField] TextMesh nameLabel;

        // 1 to 10, chosen by the host
        NetworkVariable<int> playerNumber = new(0);

        public static PlayerNetwork Local { get; private set; }
        public static IReadOnlyDictionary<ulong, PlayerNetwork> All => Players;

        public PlayerFortunes Fortunes { get; private set; }
        public PlayerGrab Grab { get; private set; }
        public int PlayerNumber => playerNumber.Value > 0 ? playerNumber.Value : (int)OwnerClientId + 1;
        public Color PlayerColor => Palette[(PlayerNumber - 1) % Palette.Length];
        public string DisplayName => $"Player {PlayerNumber}";

        void Awake()
        {
            Fortunes = GetComponent<PlayerFortunes>();
            Grab = GetComponent<PlayerGrab>();
        }

        public static bool TryGet(ulong clientId, out PlayerNetwork player) => Players.TryGetValue(clientId, out player);

        /// <summary>The lowest player number nobody is using (host side).</summary>
        public static int LowestFreeNumber(PlayerNetwork ignore = null)
        {
            for (int n = 1; n <= 64; n++)
            {
                bool taken = false;
                foreach (var p in Players.Values)
                    if (p != null && p != ignore && p.playerNumber.Value == n) { taken = true; break; }
                if (!taken) return n;
            }
            return Players.Count + 1;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && playerNumber.Value == 0) playerNumber.Value = LowestFreeNumber(this);
            Players[OwnerClientId] = this;
            playerNumber.OnValueChanged += OnNumberChanged;
            RefreshIdentity();

            if (IsOwner)
            {
                Local = this;
                LobbyCamera.SetActive(false);
                if (nameLabel != null) nameLabel.gameObject.SetActive(false);
                foreach (var r in bodyRenderers)
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            else
            {
                if (localOnlyRoot != null) Destroy(localOnlyRoot);
                foreach (var b in localOnlyBehaviours)
                    if (b != null) b.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            playerNumber.OnValueChanged -= OnNumberChanged;

            if (Players.TryGetValue(OwnerClientId, out var current) && current == this)
                Players.Remove(OwnerClientId);

            if (Local == this)
            {
                Local = null;
                LobbyCamera.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void LateUpdate()
        {
            if (IsOwner || nameLabel == null) return;
            var cam = Camera.main;
            if (cam != null) nameLabel.transform.rotation = cam.transform.rotation;
        }

        void OnNumberChanged(int previous, int current) => RefreshIdentity();

        void RefreshIdentity()
        {
            name = $"{DisplayName}{(IsOwner ? " (you)" : "")}";
            ApplyColor();
            if (nameLabel != null)
            {
                nameLabel.text = DisplayName;
                nameLabel.color = PlayerColor;
            }
        }

        void ApplyColor()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", PlayerColor);
            block.SetColor("_Color", PlayerColor);
            foreach (var r in tintedRenderers)
                if (r != null) r.SetPropertyBlock(block);
        }
    }
}
