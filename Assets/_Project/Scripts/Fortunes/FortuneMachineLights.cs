using UnityEngine;

namespace MissDirection.Fortunes
{
    /// <summary>
    /// Colors the button and Madame Direction's eyes to show what the machine is doing:
    /// green pulse = ready, white flicker = reading, red or gold = bad or good card,
    /// blue flash = someone pressed out of turn, dim = everyone has their card.
    /// </summary>
    public class FortuneMachineLights : MonoBehaviour
    {
        [SerializeField] FortuneMachine machine;
        [SerializeField] Renderer[] lights;
        [SerializeField] float emission = 2.5f;

        [Header("Colors")]
        [SerializeField] Color ready = new(0.2f, 1f, 0.35f);
        [SerializeField] Color reading = new(1f, 1f, 0.9f);
        [SerializeField] Color bad = new(1f, 0.15f, 0.15f);
        [SerializeField] Color good = new(1f, 0.78f, 0.15f);
        [SerializeField] Color shock = new(0.3f, 0.6f, 1f);
        [SerializeField] Color done = new(0.25f, 0.25f, 0.3f);

        MaterialPropertyBlock block;
        float shockUntil;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (machine == null) machine = GetComponentInParent<FortuneMachine>();
        }

        void OnEnable()
        {
            if (machine != null) machine.onOutOfTurnShock.AddListener(Flash);
        }

        void OnDisable()
        {
            if (machine != null) machine.onOutOfTurnShock.RemoveListener(Flash);
        }

        void Flash() => shockUntil = Time.time + 0.4f;

        void Update()
        {
            if (machine == null || lights == null) return;

            Color color;
            float intensity = emission;

            if (Time.time < shockUntil)
            {
                color = shock;
                intensity *= 1.5f;
            }
            else
            {
                switch (machine.State)
                {
                    case FortuneMachine.MachineState.Ready:
                        color = ready;
                        intensity *= 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
                        break;
                    case FortuneMachine.MachineState.Reading:
                        color = reading;
                        intensity *= Random.Range(0.3f, 1.2f);
                        break;
                    case FortuneMachine.MachineState.Revealed:
                    case FortuneMachine.MachineState.CardWaiting:
                        color = machine.LastRevealGood ? good : bad;
                        break;
                    default:
                        color = done;
                        intensity = 0f;
                        break;
                }
            }

            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            foreach (var r in lights)
                if (r != null) r.SetPropertyBlock(block);
        }
    }
}
