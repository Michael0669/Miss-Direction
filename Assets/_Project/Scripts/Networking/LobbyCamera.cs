using UnityEngine;

namespace MissDirection.Networking
{
    /// <summary>
    /// The camera you see on the connect menu, before your player exists.
    /// Turned off when your player spawns and back on when you leave.
    /// </summary>
    public class LobbyCamera : MonoBehaviour
    {
        static LobbyCamera instance;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void SetActive(bool active)
        {
            if (instance != null) instance.gameObject.SetActive(active);
        }
    }
}
