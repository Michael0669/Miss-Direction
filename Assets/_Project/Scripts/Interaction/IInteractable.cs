using UnityEngine;

namespace MissDirection.Interaction
{
    /// <summary>
    /// Anything a player "grabs" that reacts instead of being picked up:
    /// the Fortune Machine button, a printed card, levers, elevator buttons.
    /// </summary>
    public interface IInteractable
    {
        /// <returns>True if the interaction happened.</returns>
        bool Interact(GameObject interactor);
    }
}
