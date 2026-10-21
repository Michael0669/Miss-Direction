using System.Collections.Generic;
using UnityEngine;

namespace MissDirection.Player
{
    /// <summary>
    /// First-person physics movement: walk, sprint, jump, look. Fortunes change how input is
    /// interpreted (inverted, delayed, right-turn only, auto-run, hiccups) and how the body
    /// behaves (gravity, speed, extra jumps). Local only for now; networking wraps this later.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(PlayerModifiers), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform cameraPivot;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float sprintSpeed = 7.5f;
        [SerializeField] float acceleration = 12f;
        [SerializeField, Range(0f, 1f)] float airControl = 0.35f;
        [Tooltip("How high a normal jump goes, in meters.")]
        [SerializeField] float jumpHeight = 1.4f;

        [Header("Gravity (snappier than real life so jumps don't feel floaty)")]
        [Tooltip("Gravity multiplier for players. 1 = Unity's default.")]
        [SerializeField] float gravityMultiplier = 2.2f;
        [Tooltip("Extra multiplier while falling, so you come down faster than you go up.")]
        [SerializeField] float fallMultiplier = 1.4f;
        [SerializeField] float groundCheckDistance = 0.15f;
        [SerializeField] LayerMask groundMask = ~0;

        [Header("Look")]
        [SerializeField] float maxPitch = 85f;

        [Header("Hiccups Fortune")]
        [SerializeField] Vector2 hiccupInterval = new(2f, 5f);

        Rigidbody body;
        CapsuleCollider capsule;
        PlayerModifiers modifiers;
        PlayerInputReader reader;

        readonly Queue<(float time, InputFrame frame)> inputBuffer = new();
        InputFrame delayedInput;   // raw input after the Lagger delay
        InputFrame effectiveInput; // after every Fortune has been applied

        float yaw;
        float pitch;
        bool jumpQueued;
        int jumpsUsed;
        float lastJumpTime = -1f;
        float nextHiccupTime;

        /// <summary>Raw input after the input delay (used by grabbing, so Lagger delays hands too).</summary>
        public InputFrame CurrentInput => delayedInput;
        public bool IsGrounded { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;
        /// <summary>Where the player is looking, independent of render interpolation.</summary>
        public Quaternion LookRotation => Quaternion.Euler(pitch, yaw, 0f);

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            modifiers = GetComponent<PlayerModifiers>();
            reader = GetComponent<PlayerInputReader>();

            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            yaw = transform.eulerAngles.y;
            ScheduleHiccup();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            ReadDelayedInput();

            var input = delayedInput;
            if (modifiers.Has(ModifierType.InvertMovement)) input.Move = -input.Move;
            if (modifiers.Has(ModifierType.InvertLook)) input.Look.y = -input.Look.y;
            if (modifiers.Has(ModifierType.TurnRightOnly)) input.Look.x = Mathf.Max(0f, input.Look.x);
            if (modifiers.Has(ModifierType.AutoRun)) input.Move.y = 1f;
            effectiveInput = input;

            yaw += input.Look.x;
            pitch = Mathf.Clamp(pitch - input.Look.y, -maxPitch, maxPitch);

            if (input.JumpPressed) jumpQueued = true;

            if (modifiers.Has(ModifierType.HiccupJumps) && Time.time >= nextHiccupTime)
            {
                jumpQueued = true;
                ScheduleHiccup();
            }
        }

        void LateUpdate()
        {
            // The camera turns every frame for smooth look; the body catches up in FixedUpdate.
            if (cameraPivot != null)
                cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void FixedUpdate()
        {
            IsGrounded = CheckGround();
            if (IsGrounded && Time.time - lastJumpTime > 0.1f) jumpsUsed = 0;

            var facing = Quaternion.Euler(0f, yaw, 0f);
            body.MoveRotation(facing);

            float speed = (effectiveInput.Sprint ? sprintSpeed : walkSpeed) * modifiers.Get(ModifierType.MoveSpeedScale);
            var wish = facing * new Vector3(effectiveInput.Move.x, 0f, effectiveInput.Move.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            var velocity = body.linearVelocity;
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float blend = 1f - Mathf.Exp(-acceleration * (IsGrounded ? 1f : airControl) * Time.fixedDeltaTime);
            horizontal = Vector3.Lerp(horizontal, wish * speed, blend);
            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

            if (jumpQueued)
            {
                int allowedJumps = 1 + Mathf.RoundToInt(modifiers.Get(ModifierType.ExtraJumps));
                bool canAirJump = !IsGrounded && jumpsUsed >= 1 && jumpsUsed < allowedJumps;
                if (IsGrounded || canAirJump)
                {
                    // Based on normal gravity, so low-gravity Fortunes (Moonwalker) still leap higher.
                    float rise = -Physics.gravity.y * gravityMultiplier;
                    velocity.y = Mathf.Sqrt(2f * Mathf.Max(0.01f, rise) * Mathf.Max(0f, jumpHeight));
                    jumpsUsed = IsGrounded ? 1 : jumpsUsed + 1;
                    lastJumpTime = Time.time;
                }
                jumpQueued = false;
            }

            body.linearVelocity = velocity;

            // Unity already applies 1x gravity; add the rest.
            bool falling = !IsGrounded && velocity.y < 0f;
            float gravityScale = gravityMultiplier * (falling ? fallMultiplier : 1f)
                                 * modifiers.Get(ModifierType.GravityScale);
            if (!Mathf.Approximately(gravityScale, 1f))
                body.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
        }

        void ReadDelayedInput()
        {
            float delay = modifiers.Get(ModifierType.InputDelay);
            inputBuffer.Enqueue((Time.time, reader.Read()));

            var merged = new InputFrame();
            bool consumed = false;
            while (inputBuffer.Count > 0 && Time.time - inputBuffer.Peek().time >= delay - 0.0001f)
            {
                var frame = inputBuffer.Dequeue().frame;
                merged.Look += frame.Look;
                merged.JumpPressed |= frame.JumpPressed;
                merged.Ping |= frame.Ping;
                merged.Move = frame.Move;
                merged.Sprint = frame.Sprint;
                merged.GrabLeft = frame.GrabLeft;
                merged.GrabRight = frame.GrabRight;
                consumed = true;
            }

            if (consumed)
            {
                delayedInput = merged;
            }
            else
            {
                // Nothing old enough yet: keep held buttons, drop one-frame events.
                delayedInput.Look = Vector2.zero;
                delayedInput.JumpPressed = false;
                delayedInput.Ping = false;
            }
        }

        bool CheckGround()
        {
            float radius = capsule != null ? capsule.radius * 0.95f : 0.35f;
            var origin = transform.position + Vector3.up * (radius + 0.05f);
            return Physics.SphereCast(origin, radius, Vector3.down, out _, groundCheckDistance + 0.05f,
                groundMask, QueryTriggerInteraction.Ignore);
        }

        void ScheduleHiccup() =>
            nextHiccupTime = Time.time + Random.Range(hiccupInterval.x, hiccupInterval.y);
    }
}
