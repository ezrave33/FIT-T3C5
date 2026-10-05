using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Standalone kraken vibration tester.
/// Fires haptic pulses to both XR controllers on a timer, with optional
/// proximity scaling to a kraken transform.
///
/// NOT part of the final deliverable — merge into NavigationalStrategy_ADAPTME
/// before hand-in. Use this to tune amplitude / duration / cadence in the headset.
/// </summary>
public class KrakenVibration : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The boat transform. Used to measure distance to the kraken.")]
    public Transform boat;

    [Tooltip("The kraken transform. Leave empty for fixed-amplitude pulses.")]
    public Transform kraken;

    [Header("Timing")]
    [Tooltip("Seconds after game start before the first kraken encounter.")]
    public float firstEncounterDelay = 30f;

    [Tooltip("Cooldown between encounters, in seconds.")]
    public float cooldown = 30f;

    [Tooltip("How long each encounter lasts, in seconds.")]
    public float encounterDuration = 12f;

    [Tooltip("Enable to run automatically. Disable to trigger manually via public methods.")]
    public bool autoRun = true;

    [Header("Pulse")]
    [Tooltip("Haptic pulse amplitude (0–1).")]
    [Range(0f, 1f)] public float baseAmplitude = 0.5f;

    [Tooltip("Extra amplitude scaled by proximity (0 = none, 1 = full proximity scaling).")]
    [Range(0f, 1f)] public float proximityScale = 0.5f;

    [Tooltip("Distance at which the kraken is right next to you (max amplitude).")]
    public float nearDistance = 5f;

    [Tooltip("Distance beyond which the kraken's pulse fades to zero.")]
    public float farDistance = 50f;

    [Tooltip("Pulse duration in seconds. Short durations feel like a heartbeat; long ones feel like a rumble.")]
    [Range(0.02f, 0.5f)] public float pulseDuration = 0.1f;

    [Tooltip("Pulses per second. 5–10 feels like a heavy rumble, 1–2 like a heartbeat.")]
    [Range(0.5f, 20f)] public float pulseHz = 6f;

    [Header("Lose Condition")]
    [Tooltip("How many encounters before the lose condition triggers.")]
    public int loseAfter = 4;

    // --- Runtime state ---
    private float nextPossibleTime = 0f;
    private float encounterEndTime = 0f;
    private float nextPulseTime = 0f;
    private bool encounterActive = false;
    public int EncounterCount { get; private set; } = 0;
    public bool EncounterActive => encounterActive;

    void Start()
    {
        nextPossibleTime = Time.time + firstEncounterDelay;
    }

    void Update()
    {
        if (!autoRun) return;

        // Start a new encounter
        if (!encounterActive && Time.time >= nextPossibleTime && EncounterCount < loseAfter)
        {
            StartEncounter();
        }

        // End the encounter
        if (encounterActive && Time.time >= encounterEndTime)
        {
            EndEncounter();
        }

        // Pulse while active
        if (encounterActive && Time.time >= nextPulseTime)
        {
            FirePulse();
            nextPulseTime = Time.time + 1f / pulseHz;
        }
    }

    public void StartEncounter()
    {
        encounterActive = true;
        encounterEndTime = Time.time + encounterDuration;
        EncounterCount++;

        Debug.Log($"<color=red>KRAKEN encounter {EncounterCount}/{loseAfter} started.</color>");

        if (EncounterCount >= loseAfter)
        {
            Debug.Log("<color=red><b>LOSE CONDITION: kraken reached you too many times.</b></color>");
        }
    }

    public void EndEncounter()
    {
        encounterActive = false;
        nextPossibleTime = Time.time + cooldown;
        Debug.Log($"<color=cyan>Kraken lost interest. Encounters: {EncounterCount}/{loseAfter}</color>");
    }

    void FirePulse()
    {
        float amp = ComputeAmplitude();
        if (amp <= 0.01f) return;

        HapticsUtility.SendHapticImpulse(amp, pulseDuration, HapticsUtility.Controller.Both);
    }

    float ComputeAmplitude()
    {
        float proximity = 1f;

        if (kraken != null && boat != null)
        {
            float d = Vector3.Distance(kraken.position, boat.position);
            proximity = Mathf.Clamp01(1f - (d - nearDistance) / (farDistance - nearDistance));
        }

        return Mathf.Clamp01(baseAmplitude + proximityScale * proximity);
    }

    // --- Public helpers if you want to trigger manually from ADAPTME ---

    /// <summary>Fire a single pulse right now (for a "bump" cue).</summary>
    public void SinglePulse(float amplitude = 0.6f, float duration = 0.2f)
    {
        HapticsUtility.SendHapticImpulse(amplitude, duration, HapticsUtility.Controller.Both);
    }

    /// <summary>Force-start a kraken encounter immediately.</summary>
    public void TriggerNow()
    {
        if (!encounterActive) StartEncounter();
    }
}
