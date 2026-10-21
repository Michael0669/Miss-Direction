using System.Collections.Generic;
using MissDirection.Interaction;
using UnityEngine;

namespace MissDirection.Player
{
    /// <summary>
    /// Two hands. Hold left or right mouse to grab what you're looking at:
    /// objects are carried in front of you, teammates are held by the hand (a spring joint).
    /// Interactables (the machine button, cards, levers) respond to the LEFT hand only.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrab : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float reach = 2.5f;
        [SerializeField] float holdDistance = 1.6f;
        [SerializeField] float maxCarryMass = 40f;
        [SerializeField] LayerMask grabMask = ~0;

        [Header("Carrying feel")]
        [Tooltip("How quickly a carried object closes the gap to its hold point (1/seconds).")]
        [SerializeField] float followRate = 18f;
        [SerializeField] float maxCarrySpeed = 20f;
        [Tooltip("How quickly a carried object turns to stay facing the same way relative to you.")]
        [SerializeField] float turnRate = 12f;
        [SerializeField] float throwSpeedCap = 8f;

        [Header("Holding hands")]
        [SerializeField] float handSpring = 300f;
        [SerializeField] float handDamper = 15f;
        [SerializeField] float handSlack = 0.6f;

        class Hand
        {
            public bool isLeft;
            public float side;
            public bool wasDown;

            public Rigidbody body;
            public SpringJoint joint;
            public Vector3 localGrabPoint;
            public Quaternion rotationOffset; // held object's rotation relative to the player's yaw

            // Restored on release
            public bool hadGravity;
            public RigidbodyInterpolation interpolation;
            public CollisionDetectionMode collisionMode;
            public readonly List<Collider> ignoredColliders = new();
        }

        readonly Hand left = new() { isLeft = true, side = -1f };
        readonly Hand right = new() { isLeft = false, side = 1f };
        PlayerController controller;
        Rigidbody selfBody;
        Collider[] selfColliders;
        Vector3 eyeOffset = new(0f, 1.65f, 0f);

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            selfBody = GetComponent<Rigidbody>();
            selfColliders = GetComponentsInChildren<Collider>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        void Start()
        {
            if (viewCamera != null)
                eyeOffset = transform.InverseTransformPoint(viewCamera.transform.position);
        }

        void Update()
        {
            var input = controller.CurrentInput;
            HandleHand(left, input.GrabLeft);
            HandleHand(right, input.GrabRight);
        }

        void FixedUpdate()
        {
            CarryHeld(left);
            CarryHeld(right);
        }

        void HandleHand(Hand hand, bool down)
        {
            if (down && !hand.wasDown) TryGrab(hand);
            if (!down && hand.wasDown) Release(hand, true);
            hand.wasDown = down;
        }

        void TryGrab(Hand hand)
        {
            if (viewCamera == null || hand.body != null) return;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, grabMask, QueryTriggerInteraction.Collide)) return;

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                if (hand.isLeft) interactable.Interact(gameObject);
                return; // right hand never presses buttons or takes cards
            }

            var target = hit.rigidbody;
            if (target == null || target == selfBody || target.isKinematic) return;
            if (left.body == target || right.body == target) return; // already in the other hand

            if (target.GetComponent<PlayerController>() != null)
            {
                HoldHands(hand, target);
                return;
            }

            if (target.mass > maxCarryMass) return;
            PickUp(hand, target, hit.point);
        }

        void PickUp(Hand hand, Rigidbody target, Vector3 grabPoint)
        {
            hand.body = target;
            hand.localGrabPoint = target.transform.InverseTransformPoint(grabPoint);
            hand.rotationOffset = Quaternion.Inverse(Quaternion.Euler(0f, controller.Yaw, 0f)) * target.rotation;

            // Smooth rendering and steady motion while carried
            hand.hadGravity = target.useGravity;
            hand.interpolation = target.interpolation;
            hand.collisionMode = target.collisionDetectionMode;
            target.useGravity = false;
            target.interpolation = RigidbodyInterpolation.Interpolate;
            target.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Don't let the carried object shove against your own body
            hand.ignoredColliders.Clear();
            foreach (var other in target.GetComponentsInChildren<Collider>())
            {
                foreach (var mine in selfColliders)
                    if (mine != null) Physics.IgnoreCollision(other, mine, true);
                hand.ignoredColliders.Add(other);
            }
        }

        void HoldHands(Hand hand, Rigidbody teammate)
        {
            var joint = gameObject.AddComponent<SpringJoint>();
            joint.connectedBody = teammate;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = new Vector3(0.4f * hand.side, 1.2f, 0.2f);
            joint.connectedAnchor = new Vector3(-0.4f * hand.side, 1.2f, 0.2f);
            joint.spring = handSpring;
            joint.damper = handDamper;
            joint.minDistance = 0f;
            joint.maxDistance = handSlack;
            joint.enableCollision = true;
            hand.joint = joint;
            hand.body = teammate;
        }

        void CarryHeld(Hand hand)
        {
            if (hand.body == null || hand.joint != null) return;

            // Use the physics pose (not the interpolated render pose) so the target doesn't wobble.
            var look = controller.LookRotation;
            var yawRotation = Quaternion.Euler(0f, controller.Yaw, 0f);
            var eye = selfBody.position + yawRotation * eyeOffset;
            var target = eye + look * new Vector3(0.35f * hand.side, 0f, holdDistance);

            var body = hand.body;
            var grabPoint = body.position + body.rotation * Vector3.Scale(hand.localGrabPoint, body.transform.lossyScale);
            var error = target - grabPoint;

            if (error.magnitude > reach * 2f)
            {
                Release(hand, false); // snagged on something
                return;
            }

            // Move with the player, plus a correction that closes the gap smoothly.
            var correction = Vector3.ClampMagnitude(error * followRate, maxCarrySpeed);
            body.linearVelocity = selfBody.linearVelocity + correction;

            // Keep the object facing the same way relative to you as you turn.
            var desired = yawRotation * hand.rotationOffset;
            var delta = desired * Quaternion.Inverse(body.rotation);
            delta.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            body.angularVelocity = float.IsNaN(axis.x) || Mathf.Abs(angle) < 0.01f
                ? Vector3.zero
                : axis * (angle * Mathf.Deg2Rad * turnRate);
        }

        void Release(Hand hand, bool keepMomentum)
        {
            if (hand.joint != null)
            {
                Destroy(hand.joint);
            }
            else if (hand.body != null)
            {
                var body = hand.body;
                body.useGravity = hand.hadGravity;
                body.interpolation = hand.interpolation;
                body.collisionDetectionMode = hand.collisionMode;
                body.angularVelocity = Vector3.zero;
                body.linearVelocity = keepMomentum
                    ? Vector3.ClampMagnitude(body.linearVelocity, throwSpeedCap)
                    : Vector3.zero;

                foreach (var other in hand.ignoredColliders)
                {
                    if (other == null) continue;
                    foreach (var mine in selfColliders)
                        if (mine != null) Physics.IgnoreCollision(other, mine, false);
                }
                hand.ignoredColliders.Clear();
            }

            hand.joint = null;
            hand.body = null;
        }

        public void ReleaseAll()
        {
            Release(left, false);
            Release(right, false);
        }

        /// <summary>Sent by the Fortune Machine when you press out of turn.</summary>
        void OnFortuneShock() => ReleaseAll();
    }
}
