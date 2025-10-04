namespace LSP.Gameplay.Interactions
{
    /// <summary>
    /// Describes an object that can be interacted with by the player.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Returns true if the interactable can currently be activated by the specified caller.
        /// </summary>
        /// <param name="caller">Controller attempting to interact.</param>
        bool CanInteract(LSP.Gameplay.PlayerInteractionController caller);

        /// <summary>
        /// Performs the interaction behaviour for the given caller.
        /// </summary>
        /// <param name="caller">Controller invoking the interaction.</param>
        void Interact(LSP.Gameplay.PlayerInteractionController caller);
    }
}
