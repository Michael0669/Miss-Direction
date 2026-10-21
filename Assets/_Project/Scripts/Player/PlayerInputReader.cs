using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MissDirection.Player
{
    /// <summary>One frame of raw player input, before any Fortune changes it.</summary>
    public struct InputFrame
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool JumpPressed;
        public bool Sprint;
        public bool GrabLeft;
        public bool GrabRight;
        public bool Ping;
    }

    /// <summary>
    /// Reads keyboard and mouse. Works with either the new Input System or the old Input Manager,
    /// so it compiles whatever "Active Input Handling" the project uses.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Degrees per pixel of mouse movement (new Input System).")]
        [SerializeField] float mouseSensitivity = 0.1f;

        [Tooltip("Multiplier for mouse axes (old Input Manager).")]
        [SerializeField] float legacyLookScale = 2f;

        public InputFrame Read()
        {
            var frame = new InputFrame();
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                frame.Move = new Vector2(
                    (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                    (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
                frame.JumpPressed = kb.spaceKey.wasPressedThisFrame;
                frame.Sprint = kb.leftShiftKey.isPressed;
            }
            if (mouse != null)
            {
                frame.Look = mouse.delta.ReadValue() * mouseSensitivity;
                frame.GrabLeft = mouse.leftButton.isPressed;
                frame.GrabRight = mouse.rightButton.isPressed;
                frame.Ping = mouse.middleButton.wasPressedThisFrame;
            }
#else
            frame.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            frame.Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * legacyLookScale;
            frame.JumpPressed = Input.GetKeyDown(KeyCode.Space);
            frame.Sprint = Input.GetKey(KeyCode.LeftShift);
            frame.GrabLeft = Input.GetMouseButton(0);
            frame.GrabRight = Input.GetMouseButton(1);
            frame.Ping = Input.GetMouseButtonDown(2);
#endif
            return frame;
        }
    }
}
