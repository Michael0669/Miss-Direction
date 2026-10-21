using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MissDirection.Networking
{
    /// <summary>
    /// Prototype connect menu (IMGUI): Host, or Join by IP. Caps the game at 10 players and
    /// places each new player at the spawn area. F8 leaves. Steam lobbies replace this later.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkLauncher : MonoBehaviour
    {
        public const int MaxPlayers = 10;

        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;

        static string lastMessage;

        NetworkManager manager;
        UnityTransport transport;
        string portText;
        bool wasListening;
        bool everConnected;
        bool leaving;
        string localIp;
        GUIStyle title, label, button, field, small;

        void Awake()
        {
            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            portText = port.ToString();
            localIp = FindLocalIp();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (manager == null) return;

            if (manager.IsConnectedClient) everConnected = true;

            if (wasListening && !manager.IsListening && !manager.ShutdownInProgress && !leaving)
            {
                var reason = manager.DisconnectReason;
                if (!string.IsNullOrEmpty(reason)) ReturnToMenu($"Disconnected: {reason}");
                else if (!everConnected) ReturnToMenu($"Couldn't connect to {address}:{port}. Is the host running?");
                else ReturnToMenu("Disconnected. The host may have left.");
                return;
            }
            wasListening = manager.IsListening;

            if (manager.IsListening && LeavePressed()) ReturnToMenu("You left the game.");
        }

        public void Host()
        {
            if (!Configure()) return;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!manager.StartHost()) lastMessage = $"Couldn't host on port {port}. Is another game already hosting?";
            else lastMessage = null;
        }

        public void Join()
        {
            if (!Configure()) return;
            transport.SetConnectionData(address.Trim(), port);
            if (!manager.StartClient()) lastMessage = "Couldn't start connecting. Check the address.";
            else lastMessage = null;
        }

        bool Configure()
        {
            if (transport == null)
            {
                lastMessage = "No UnityTransport on the NetworkManager.";
                return false;
            }
            if (!ushort.TryParse(portText, out port))
            {
                lastMessage = "The port must be a number between 1 and 65535.";
                return false;
            }

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = Approve;
            return true;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int playerCount = manager.ConnectedClientsIds.Count;
            if (playerCount >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = $"The game is full ({MaxPlayers} players).";
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;

            var spawn = FindAnyObjectByType<PlayerSpawnArea>();
            if (spawn != null)
            {
                response.Position = spawn.GetPosition(playerCount);
                response.Rotation = spawn.Rotation;
            }
        }

        void ReturnToMenu(string message)
        {
            if (leaving) return;
            leaving = true;
            lastMessage = message;

            if (manager.IsListening) manager.Shutdown();
            // Reload the scene for a clean slate. The NetworkManager survives scene loads,
            // so destroy it; the reloaded scene brings a fresh one.
            Destroy(manager.gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnGUI()
        {
            if (manager == null) return;
            EnsureStyles();

            if (manager.IsListening)
            {
                DrawStatus();
                return;
            }

            float w = 460f, h = 360f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            DrawBox(panel, new Color(0.12f, 0.05f, 0.18f, 0.92f));

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 18f, w - 48f, h - 36f));
            GUILayout.Label("MISS DIRECTION", title);
            GUILayout.Label("Playground (1 to 10 players)", small);
            GUILayout.Space(14f);

            if (GUILayout.Button("HOST A GAME", button, GUILayout.Height(44f))) Host();
            GUILayout.Space(14f);

            GUILayout.Label("Or join a host:", label);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", label, GUILayout.Width(80f));
            address = GUILayout.TextField(address, field, GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", label, GUILayout.Width(80f));
            portText = GUILayout.TextField(portText, field, GUILayout.Width(100f), GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            if (GUILayout.Button("JOIN", button, GUILayout.Height(40f))) Join();

            GUILayout.Space(10f);
            if (!string.IsNullOrEmpty(lastMessage))
            {
                small.normal.textColor = new Color(1f, 0.75f, 0.4f);
                GUILayout.Label(lastMessage, small);
                small.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            }
            GUILayout.Label("Same computer: join 127.0.0.1. Same Wi-Fi: join the host's local IP.", small);
            GUILayout.EndArea();
        }

        void DrawStatus()
        {
            string role = manager.IsHost ? "HOSTING" : manager.IsConnectedClient ? "CONNECTED" : "CONNECTING...";
            int players = manager.IsServer ? manager.ConnectedClientsIds.Count : PlayerNetwork.All.Count;
            string text = manager.IsHost
                ? $"{role}  |  {players}/{MaxPlayers} players  |  Your IP: {localIp}:{port}  |  F8 to leave"
                : $"{role}  |  {players} players  |  F8 to leave";

            float width = Mathf.Min(560f, Screen.width - 32f);
            var rect = new Rect(Screen.width - width - 16f, Screen.height - 42f, width, 26f);
            DrawBox(rect, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, rect.height), text, small);
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.82f, 0.25f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = Color.white;
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        }

        static void DrawBox(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static string FindLocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch
            {
                // No network info available; fall through.
            }
            return "127.0.0.1";
        }

        static bool LeavePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F8);
#endif
        }
    }
}
