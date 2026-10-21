using UnityEngine;

namespace MissDirection.Networking
{
    /// <summary>Where players appear: the first in the middle, the rest in a ring around it.</summary>
    public class PlayerSpawnArea : MonoBehaviour
    {
        [SerializeField] float ringRadius = 1.4f;
        [SerializeField] int ringSlots = 9;

        public Vector3 GetPosition(int index)
        {
            if (index <= 0) return transform.position;
            float angle = (index - 1) * Mathf.PI * 2f / Mathf.Max(1, ringSlots);
            return transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ringRadius;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < ringSlots + 1; i++)
                Gizmos.DrawWireSphere(GetPosition(i) + Vector3.up * 0.1f, 0.3f);
        }
    }
}
