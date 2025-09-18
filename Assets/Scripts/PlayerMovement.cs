using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Basic first-person style planar movement that honours the configured movement speed
    /// and the player's alive state.
    /// </summary>
    [RequireComponent(typeof(PlayerStateController))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private CharacterController controller;

        private PlayerStateController stateController;

        private void Awake()
        {
            stateController = GetComponent<PlayerStateController>();

            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }
        }

        private void Update()
        {
            if (!stateController.IsAlive || controller == null)
            {
                return;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 move = (transform.right * input.x) + (transform.forward * input.y);
            controller.SimpleMove(move * stateController.MovementSpeed);
        }
    }
}
