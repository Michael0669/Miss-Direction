using System;
using UnityEngine;

namespace MissDirection.Player
{
    /// <summary>Every gameplay change a Fortune can make to a player.</summary>
    public enum ModifierType
    {
        InvertMovement,
        InvertLook,
        FlipScreenVertical,
        FlipScreenHorizontal,
        Blind,
        InputDelay,      // seconds
        ExtraJumps,      // count
        GravityScale,    // multiplier
        MoveSpeedScale,  // multiplier
        AutoRun,
        TurnRightOnly,
        HiccupJumps
    }

    /// <summary>
    /// Holds the current stack of modifiers on a player. Fortunes add and remove values here;
    /// the controller, camera and grab systems read them. Flags and amounts add up, scales multiply,
    /// so several Fortunes can stack and be removed cleanly in any order.
    /// </summary>
    public class PlayerModifiers : MonoBehaviour
    {
        static readonly int Count = Enum.GetValues(typeof(ModifierType)).Length;

        float[] values;

        public event Action Changed;

        void Awake() => EnsureInit();

        public static bool IsScale(ModifierType type) =>
            type == ModifierType.GravityScale || type == ModifierType.MoveSpeedScale;

        public void Apply(ModifierType type, float value)
        {
            EnsureInit();
            if (IsScale(type)) values[(int)type] *= value;
            else values[(int)type] += value;
            Changed?.Invoke();
        }

        public void Remove(ModifierType type, float value)
        {
            EnsureInit();
            if (IsScale(type))
            {
                if (Mathf.Abs(value) > 0.0001f) values[(int)type] /= value;
            }
            else
            {
                values[(int)type] = Mathf.Max(0f, values[(int)type] - value);
            }
            Changed?.Invoke();
        }

        public float Get(ModifierType type)
        {
            EnsureInit();
            return values[(int)type];
        }

        public bool Has(ModifierType type) => !IsScale(type) && Get(type) > 0.001f;

        public void ResetAll()
        {
            values = null;
            EnsureInit();
            Changed?.Invoke();
        }

        void EnsureInit()
        {
            if (values != null) return;
            values = new float[Count];
            for (int i = 0; i < Count; i++)
                values[i] = IsScale((ModifierType)i) ? 1f : 0f;
        }
    }
}
