using MissDirection.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// The printed card. Pops out of the slot, hovers and bobs so it's easy to spot,
    /// and shows the Fortune's name. Every player sees it, but only the player who pressed
    /// the button can take it. Taking it asks the host, who grants the Fortune to everyone's
    /// copy of that player. (Wrist slot and "show card" come later.)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FortuneCard : MonoBehaviour, IInteractable
    {
        [SerializeField] TextMesh label;
        [SerializeField] Renderer face;
        [SerializeField] float popDistance = 0.45f;
        [SerializeField] float popSeconds = 0.35f;
        [SerializeField] Color goodColor = new(1f, 0.82f, 0.25f);
        [SerializeField] Color badColor = new(0.95f, 0.3f, 0.3f);

        FortuneMachine machine;
        Rigidbody body;
        Vector3 startPosition;
        Vector3 hoverPosition;
        float spawnTime;
        bool requested;

        public FortuneDefinition Fortune { get; private set; }
        public ulong OwnerClientId { get; private set; }
        public int CardId { get; private set; }

        /// <summary>True if this card belongs to the player on this computer.</summary>
        public bool IsMine =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == OwnerClientId;

        void Awake() => body = GetComponent<Rigidbody>();

        public void Init(FortuneDefinition fortune, FortuneMachine source, ulong ownerClientId, int cardId)
        {
            Fortune = fortune;
            machine = source;
            OwnerClientId = ownerClientId;
            CardId = cardId;
            name = $"Card - {fortune.displayName} (Player {ownerClientId + 1})";

            startPosition = transform.position;
            hoverPosition = startPosition + transform.forward * popDistance;
            spawnTime = Time.time;

            if (label != null) label.text = fortune.displayName.ToUpperInvariant().Replace(' ', '\n');

            if (face != null)
            {
                var block = new MaterialPropertyBlock();
                var color = fortune.kind == FortuneKind.Good ? goodColor : badColor;
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * 0.6f);
                face.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (body == null || !body.isKinematic) return;

            float t = Mathf.Clamp01((Time.time - spawnTime) / Mathf.Max(0.01f, popSeconds));
            var position = Vector3.Lerp(startPosition, hoverPosition, 1f - (1f - t) * (1f - t));
            position.y += Mathf.Sin(Time.time * 3f) * 0.03f;
            transform.position = position;
        }

        public bool Interact(GameObject interactor)
        {
            if (requested || !IsMine || machine == null) return false;
            requested = true; // the host confirms, then every game removes the card
            machine.RequestClaim(CardId);
            return true;
        }
    }
}
