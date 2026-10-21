using MissDirection.Fortunes;
using MissDirection.Interaction;
using MissDirection.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MissDirection.UI
{
    /// <summary>
    /// Prototype HUD (IMGUI, no setup needed): what to do next, a crosshair with a
    /// "what am I looking at" prompt, the Fortunes you hold, and controls.
    /// R resets the machine so you can draw again; H hides the controls.
    /// </summary>
    public class PlaygroundHud : MonoBehaviour
    {
        [SerializeField] PlayerFortunes player;
        [SerializeField] Camera viewCamera;
        [SerializeField] FortuneMachine machine;
        [SerializeField] float reach = 2.5f;

        static readonly Color Gold = new(1f, 0.82f, 0.25f);
        static readonly Color Red = new(1f, 0.4f, 0.4f);
        static readonly Color Green = new(0.45f, 1f, 0.55f);

        bool showControls = true;
        string prompt;
        bool promptActionable;
        GUIStyle big, medium, small;
        Texture2D white;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerFortunes>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        void Start()
        {
            if (machine == null) machine = FindAnyObjectByType<FortuneMachine>();
        }

        void Update()
        {
            if (ResetPressed() && machine != null && player != null)
            {
                player.ClearAll();
                machine.BeginFloor(machine.TeamSize, Random.Range(int.MinValue, int.MaxValue));
            }
            if (HelpPressed()) showControls = !showControls;

            UpdatePrompt();
        }

        void UpdatePrompt()
        {
            prompt = null;
            promptActionable = false;
            if (viewCamera == null) return;

            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Collide)) return;

            var card = hit.collider.GetComponentInParent<FortuneCard>();
            if (card != null)
            {
                bool mine = card.ReservedFor == null || card.ReservedFor == player;
                prompt = mine ? $"LEFT CLICK  Take your card: {card.Fortune.displayName}" : "This card belongs to someone else";
                promptActionable = mine;
                return;
            }

            var button = hit.collider.GetComponentInParent<FortuneMachineButton>();
            if (button != null && button.Machine != null)
            {
                var m = button.Machine;
                if (m.IsBusy) prompt = "Wait your turn! Madame Direction is reading...";
                else if (m.HasPressed(player)) prompt = m.HasClaimed(player) ? "You already have your Fortune (R to reset)" : "Take your card first!";
                else { prompt = "LEFT CLICK  Press the button"; promptActionable = true; }
                return;
            }

            if (hit.collider.GetComponentInParent<IInteractable>() != null)
            {
                prompt = "LEFT CLICK  Use";
                promptActionable = true;
                return;
            }

            var rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic && (player == null || rb.gameObject != player.gameObject))
            {
                prompt = rb.GetComponent<PlayerController>() != null
                    ? "HOLD CLICK  Hold hands"
                    : "HOLD LEFT or RIGHT CLICK  Carry";
                promptActionable = true;
            }
        }

        string Objective()
        {
            if (machine == null) return "No Fortune Machine in this scene.";
            if (!machine.HasDeck) return "The Fortune Machine has no deck! Re-run Miss Direction > Build Test Playground Scene.";
            switch (machine.State)
            {
                case FortuneMachine.MachineState.Reading:
                    return "Madame Direction is reading your fortune...";
                case FortuneMachine.MachineState.Revealed:
                    return machine.LastRevealGood ? "Her eyes glow GOLD... a good fortune!" : "Her eyes glow RED... uh oh.";
                case FortuneMachine.MachineState.CardWaiting:
                    return "Your card printed! Look at the floating card and LEFT CLICK it.";
                case FortuneMachine.MachineState.Done:
                    return "Fortune received! Try moving around with it.   Press R to draw again.";
                default:
                    return "Walk to the Fortune Machine (the striped cabinet) and LEFT CLICK the glowing green button.";
            }
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            // Objective banner
            var objective = Objective();
            var banner = new Rect(w * 0.5f - 420f, 16f, 840f, 52f);
            DrawPanel(banner, new Color(0.1f, 0.05f, 0.15f, 0.8f));
            big.normal.textColor = machine != null && machine.State == FortuneMachine.MachineState.Revealed
                ? (machine.LastRevealGood ? Gold : Red)
                : Color.white;
            GUI.Label(banner, objective, big);

            // Crosshair
            var crossColor = promptActionable ? Green : new Color(1f, 1f, 1f, 0.8f);
            float size = promptActionable ? 10f : 6f;
            DrawPanel(new Rect(w * 0.5f - size * 0.5f, h * 0.5f - size * 0.5f, size, size), crossColor);

            // Prompt under crosshair
            if (!string.IsNullOrEmpty(prompt))
            {
                medium.normal.textColor = promptActionable ? Green : Gold;
                var rect = new Rect(w * 0.5f - 300f, h * 0.5f + 24f, 600f, 34f);
                DrawPanel(rect, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(rect, prompt, medium);
            }

            // Active Fortunes
            if (player != null && player.Active.Count > 0)
            {
                float y = 84f;
                foreach (var fortune in player.Active)
                {
                    bool goodCard = fortune.kind == FortuneKind.Good;
                    var rect = new Rect(w - 356f, y, 340f, 72f);
                    DrawPanel(rect, goodCard ? new Color(0.35f, 0.27f, 0.02f, 0.9f) : new Color(0.35f, 0.04f, 0.06f, 0.9f));
                    medium.normal.textColor = goodCard ? Gold : Red;
                    GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 28f), $"YOUR FORTUNE: {fortune.displayName}", medium);
                    small.normal.textColor = Color.white;
                    GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 34f), fortune.cardRule, small);
                    y += 80f;
                }
            }

            // Controls
            if (showControls)
            {
                var rect = new Rect(16f, h - 190f, 300f, 174f);
                DrawPanel(rect, new Color(0f, 0f, 0f, 0.55f));
                small.normal.textColor = Color.white;
                GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 16f),
                    "WASD  Move\nMouse  Look\nSpace  Jump\nShift  Sprint\nLeft / Right click  Grab or press\nR  Reset the machine (draw again)\nH  Hide this help\nEsc  Free the mouse", small);
            }
        }

        void DrawPanel(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (big != null) return;
            white = Texture2D.whiteTexture;
            big = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            medium = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            small.normal.textColor = Color.white;
        }

        static bool ResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        static bool HelpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.H);
#endif
        }
    }
}
