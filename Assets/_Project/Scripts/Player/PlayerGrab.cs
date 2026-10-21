using MissDirection.Interaction;
using UnityEngine;

namespace MissDirection.Player
{
    /// <summary>
    /// Two hands. Hold left or right mouse to grab what you're looking at:
    /// objects are carried in front of you, teammates are held by the hand (a spring joint),
    /// and interactables (buttons, cards) are used instead of picked up.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrab : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float reach = 2.5f;
        [SerializeField] float holdDistance = 1.6f;
        [SerializeField] float holdStrength = 12f;
        [SerializeField] float maxCarryMass = 40f;
        [SerializeField] LayerMask grabMask = ~0;

        [Header("Holding hands")]
        [SerializeField] float handSpring = 300f;
        [SerializeField] float handDamper = 15f;
        [SerializeField] float handSlack = 0.6f;

        class Hand
        {
            public Rigidbody body;
            public Vector3 localGrabPoint;
            public SpringJoint joint;
            public bool wasDown;
            public float side;
        }

        readonly Hand left = new() { side = -1f };
        readonly Hand right = new() { side = 1f };
        PlayerController controller;
        Rigidbody selfBody;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            selfBody = GetComponent<Rigidbody>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
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
            if (!down && hand.wasDown) Release(hand);
            hand.wasDown = down;
        }

        void TryGrab(Hand hand)
        {
            if (viewCamera == null) return;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, grabMask, QueryTriggerInteraction.Collide)) return;

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(gameObject);
                return;
            }

            var target = hit.rigidbody;
            if (target == null || target == selfBody || target.isKinematic) return;

            if (target.GetComponent<PlayerController>() != null)
            {
                HoldHands(hand, target);
                return;
            }

            if (target.mass > maxCarryMass) return;
            hand.body = target;
            hand.localGrabPoint = target.transform.InverseTransformPoint(hit.point);
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
            if (hand.body == null || hand.joint != null || viewCamera == null) return;

            var cam = viewCamera.transform;
            var target = cam.position + cam.forward * holdDistance + cam.right * (0.35f * hand.side);
            var grabPoint = hand.body.transform.TransformPoint(hand.localGrabPoint);

            if (Vector3.Distance(target, grabPoint) > reach * 2f)
            {
                Release(hand); // snagged on something
                return;
            }

            hand.body.linearVelocity = Vector3.ClampMagnitude((target - grabPoint) * holdStrength, 15f);
            hand.body.angularVelocity *= 0.9f;
        }

        void Release(Hand hand)
        {
            if (hand.joint != null) Destroy(hand.joint);
            hand.joint = null;
            hand.body = null;
        }

        public void ReleaseAll()
        {
            Release(left);
            Release(right);
        }

        /// <summary>Sent by the Fortune Machine when you press out of turn.</summary>
        void OnFortuneShock() => ReleaseAll();
    }
}
