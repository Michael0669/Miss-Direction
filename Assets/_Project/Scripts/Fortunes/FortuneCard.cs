using MissDirection.Interaction;
using MissDirection.Player;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// The printed card. Pops out of the slot, hovers and bobs so it's easy to spot,
    /// and shows the Fortune's name. Reserved for the player who pressed the button;
    /// grabbing it activates the Fortune. (Wrist slot and "show card" come later.)
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

        public FortuneDefinition Fortune { get; private set; }
        public PlayerFortunes ReservedFor { get; private set; }
        public bool Claimed { get; private set; }

        void Awake() => body = GetComponent<Rigidbody>();

        public void Init(FortuneDefinition fortune, FortuneMachine source, PlayerFortunes reservedFor)
        {
            Fortune = fortune;
            machine = source;
            ReservedFor = reservedFor;
            name = $"Card - {fortune.displayName}";

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

        public bool Interact(GameObject interactor) =>
            Claim(interactor != null ? interactor.GetComponentInParent<PlayerFortunes>() : null);

        public bool Claim(PlayerFortunes player)
        {
            if (Claimed || player == null || Fortune == null) return false;
            if (ReservedFor != null && ReservedFor != player) return false;

            Claimed = true;
            player.Grant(Fortune);
            if (machine != null) machine.NotifyClaimed(player);
            Destroy(gameObject);
            return true;
        }
    }
}
