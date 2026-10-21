using MissDirection.Interaction;
using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>The big brass button. Left-click it to press.</summary>
    public class FortuneMachineButton : MonoBehaviour, IInteractable
    {
        [SerializeField] FortuneMachine machine;

        public FortuneMachine Machine => machine;

        public bool Interact(GameObject interactor)
        {
            if (machine == null) return false;
            machine.RequestPress();
            return true;
        }
    }
}
