using MissDirection.Interaction;
using MissDirection.Player;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// The printed card. Reserved for the player who pressed the button;
    /// grabbing it activates the Fortune. (Wrist slot and "show card" come later.)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FortuneCard : MonoBehaviour, IInteractable
    {
        FortuneMachine machine;

        public FortuneDefinition Fortune { get; private set; }
        public PlayerFortunes ReservedFor { get; private set; }
        public bool Claimed { get; private set; }

        public void Init(FortuneDefinition fortune, FortuneMachine source, PlayerFortunes reservedFor)
        {
            Fortune = fortune;
            machine = source;
            ReservedFor = reservedFor;
            name = $"Card - {fortune.displayName}";
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
