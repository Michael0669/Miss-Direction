using MissDirection.Interaction;
using MissDirection.Player;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>The big brass button. Grab it to press.</summary>
    public class FortuneMachineButton : MonoBehaviour, IInteractable
    {
        [SerializeField] FortuneMachine machine;

        public FortuneMachine Machine => machine;

        public bool Interact(GameObject interactor)
        {
            if (machine == null || interactor == null) return false;
            return machine.TryPress(interactor.GetComponentInParent<PlayerFortunes>());
        }
    }
}
