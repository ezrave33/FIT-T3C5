using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class NavigationalStrategy_ADAPTME : MonoBehaviour
{
    /* ============================================================
     *  THE LANTERN KEEPER'S ROUTE
     *  Multimodal navigation through the Veiled Sound.
     *  Three guardians, three senses, one lighthouse.
     * ============================================================ */

    [Header("Group")]
    public string GroupName = "Group";

    [Header("Scene References")]
    [SerializeField] private GameObject RowBoatPrefab;
    [SerializeField] private GameObject lighthousePrefab; // assign in inspector, or auto-found

    // ============================================================
    //  STEP 2 — CHECKPOINTS (the three guardians)
    // ============================================================
    [Header("Guardian Checkpoints (order matters)")]
    [Tooltip("Positions of the three guardians. Slot 0 = Ember, 1 = Bell, 2 = Kite. " +
             "Keep all of these >50 units from the lighthouse so TargetLocator doesn't mute cues.")]
    public Vector3[] checkpointPositions = new Vector3[3];

    [Tooltip("Which modality each checkpoint unlocks. 0 = Heat, 1 = Airflow, 2 = Audio.")]
    public int[] checkpointModalityIndex = new int[] { 0, 1, 2 };

    [Tooltip("Reach radius for each checkpoint.")]
    public float checkpointReachRadius = 8f;

    // ============================================================
    //  STEP 7 — PUZZLE: angle window for each checkpoint
    //  You must approach the guardian within this angular window,
    //  otherwise the guardian stays "unsolved" and won't join.
    // ============================================================
    [Header("Guardian Trial — Approach Angle Window")]
    [Tooltip("Half-width in degrees. You must be facing the guardian within ±this to solve.")]
    public float trialAngleWindow = 25f;

    [Tooltip("Only require the angle window once you are this close (so you don't fail from far away).")]
    public float trialAngleCheckDistance = 20f;

    // ============================================================
    //  RUNTIME STATE
    // ============================================================
    private int activeCheckpointIndex = 0;
    private bool heatUnlocked = false;
    private bool airflowUnlocked = false;
    private bool audioUnlocked = false;

    // Step 4 — "guardian joins" sting
    private float guardianStingUntilTime = 0f;
    private const float GUARDIAN_STING_DURATION = 1.5f;

    // ============================================================
    //  HARDWARE INTERFACE (do not rename — CuesActuator + SoundController read these)
    // ============================================================
    public bool TurnOnHeatTOWERLEFT = false;
    public bool TurnOnHeatTOWERRIGHT = false;
    public bool TurnOnHeatTOWERBACK = false;
    public bool TurnOnHeatTOWERFRONT = false;

    public bool TurnOnHeaterLEFT = false;
    public bool TurnOnHeaterLEFTMIDDLE = false;
    public bool TurnOnHeaterRIGHTMIDDLE = false;
    public bool TurnOnHeaterRIGHT = false;

    public bool TurnOnHeater1 = false;
    public bool TurnOnHeater2 = false;
    public bool TurnOnHeater3 = false;
    public bool TurnOnHeater4 = false;
    public bool TurnOnHeater5 = false;
    public bool TurnOnHeater6 = false;
    public bool TurnOnHeater7 = false;
    public bool TurnOnHeater8 = false;

    public float Hairdryer1Ventilator = 0f;
    public float Hairdryer2Ventilator = 0f;
    public bool ShutDownHairdryers = false;

    public bool Sound1On = false;
    public bool Sound2On = false;
    public bool Sound3On = false;
    public bool Sound4On = false;
    public bool Sound5On = false;
    public bool Sound6On = false;
    public bool Sound7On = false;
    public bool Sound8On = false;

    // ============================================================
    //  SAFETY CONSTANTS (slide 95)
    // ============================================================
    private const float HAIRDRYER_MIN_PWM = 0.2f;
    private const int MAX_HEAT_TOWERS_ON = 2;

    // ============================================================
    //  DATA + LOGGING
    // ============================================================
    GameObject eventManager;
    string filename = "";
    private string date;
    private string time;
    private int startingTimeMillisec;
    private float nextWriteTime = 0f;

    private float PrivateAngleOfCurrentTarget;

    private List<Vector3> DangerZoneLocations;
    private float distanceBoatToDangerZone;
    private float angleBoatToDangerZone;
    private float[] dangerZoneDistances;
    public int TriggerDistanceDangerZone = 20;

    // Step 8 — evaluation metrics
    private int[] checkpointsReachedAtMs;
    private int dangerZoneHits = 0;
    private bool wasInDangerLastFrame = false;

    void Awake()
    {
        Debug.Log($"Trial started for group: {GroupName} — The Lantern Keeper's Route");

        eventManager = GameObject.Find("EventManager");

        if (lighthousePrefab == null)
        {
            lighthousePrefab = GameObject.Find("Lighthouse");
        }

        date = DateTime.Now.ToString("dd-MM-yyyy");
        time = DateTime.Now.ToString("HH-mm-ss");
        startingTimeMillisec = (((DateTime.Now.Hour * 3600) + (DateTime.Now.Minute * 60) + DateTime.Now.Second) * 1000) + DateTime.Now.Millisecond;
        filename = Application.dataPath + "/SavedData/" + date + "--" + time + "--GroupName-" + GroupName + ".csv";

        DangerZoneLocations = new List<Vector3>();
        var tl = eventManager.GetComponent<TargetLocator>();
        DangerZoneLocations.Add(tl.DangerZone1.transform.position);
        DangerZoneLocations.Add(tl.DangerZone2.transform.position);
        DangerZoneLocations.Add(tl.DangerZone3.transform.position);
        DangerZoneLocations.Add(tl.DangerZone4.transform.position);
        DangerZoneLocations.Add(tl.DangerZone5.transform.position);
        DangerZoneLocations.Add(tl.DangerZone6.transform.position);
        DangerZoneLocations.Add(tl.DangerZone7.transform.position);
        DangerZoneLocations.Add(tl.DangerZone8.transform.position);
        DangerZoneLocations.Add(tl.DangerZone9.transform.position);
        DangerZoneLocations.Add(tl.DangerZone10.transform.position);
        DangerZoneLocations.Add(tl.DangerZone11.transform.position);

        dangerZoneDistances = new float[DangerZoneLocations.Count];
        checkpointsReachedAtMs = new int[checkpointPositions.Length];
        for (int i = 0; i < checkpointsReachedAtMs.Length; i++) checkpointsReachedAtMs[i] = -1;
    }

    // ============================================================
    //  STEP 1 — CURRENT TARGET ABSTRACTION
    // ============================================================
    private bool CurrentTargetIsCheckpoint()
    {
        return activeCheckpointIndex < checkpointPositions.Length;
    }

    private Vector3 GetCurrentTargetPosition()
    {
        if (CurrentTargetIsCheckpoint())
            return checkpointPositions[activeCheckpointIndex];

        if (lighthousePrefab != null)
            return lighthousePrefab.transform.position;

        return RowBoatPrefab.transform.position; // fallback, no movement
    }

    private float GetAngleToCurrentTarget()
    {
        Vector3 dir = GetCurrentTargetPosition() - RowBoatPrefab.transform.position;
        Vector3 horiz = new Vector3(dir.x, 0f, dir.z);
        return Vector3.SignedAngle(RowBoatPrefab.transform.forward, horiz, Vector3.up);
    }

    // ============================================================
    //  UPDATE
    // ============================================================
    void Update()
    {
        if (eventManager == null || RowBoatPrefab == null) return;

        var tl = eventManager.GetComponent<TargetLocator>();
        if (tl == null || tl.EndScene) return;

        PrivateAngleOfCurrentTarget = GetAngleToCurrentTarget();

        // --- Step 2/3/4: checkpoint progression ---
        UpdateCheckpointProgression();

        // --- Step 6: gather danger info first, because it can override ---
        bool dangerThreatActive = UpdateDangerZones();

        // --- Step 5: emit cues for the current target ---
        if (!dangerThreatActive)
        {
            EmitTargetCues();
        }

        // --- Step 8: logging ---
        if (Time.time >= nextWriteTime)
        {
            WriteCSV();
            nextWriteTime = Time.time + 1f;
        }
    }

    // ============================================================
    //  STEP 2 + 3 + 4 + 7 — CHECKPOINT PROGRESSION
    // ============================================================
    void UpdateCheckpointProgression()
    {
        if (!CurrentTargetIsCheckpoint()) return;

        Vector3 cp = checkpointPositions[activeCheckpointIndex];
        Vector3 boatPos = RowBoatPrefab.transform.position;
        float dist = Vector3.Distance(cp, boatPos);

        if (dist <= checkpointReachRadius)
        {
            // Step 7 — trial: must be facing the guardian within the angle window
            float angleToCP = GetAngleToCurrentTarget();

            bool angleOK = true;
            if (dist <= trialAngleCheckDistance)
            {
                angleOK = Mathf.Abs(angleToCP) <= trialAngleWindow;
            }

            if (!angleOK)
            {
                // Not solved yet — emit a "wrong approach" cue (short hairdryer pulse)
                // We stay put; player must reorient.
                return;
            }

            // --- SOLVED ---
            int modality = (activeCheckpointIndex < checkpointModalityIndex.Length)
                ? checkpointModalityIndex[activeCheckpointIndex]
                : -1;

            switch (modality)
            {
                case 0: heatUnlocked = true;    Debug.Log("Ember joins — HEAT unlocked.");    break;
                case 1: airflowUnlocked = true; Debug.Log("Bell joins — AIRFLOW unlocked."); break;
                case 2: audioUnlocked = true;   Debug.Log("Kite joins — AUDIO unlocked.");   break;
            }

            // Step 4 — guardian sting
            guardianStingUntilTime = Time.time + GUARDIAN_STING_DURATION;

            checkpointsReachedAtMs[activeCheckpointIndex] =
                (((DateTime.Now.Hour * 3600) + (DateTime.Now.Minute * 60) + DateTime.Now.Second) * 1000)
                + DateTime.Now.Millisecond - startingTimeMillisec;

            activeCheckpointIndex++;
        }
    }

    // ============================================================
    //  STEP 6 — DANGER ZONES (returns true if a repulsion override is active)
    // ============================================================
    bool UpdateDangerZones()
    {
        bool overrideActive = false;
        bool inAnyDanger = false;

        for (int i = 0; i < dangerZoneDistances.Length; i++)
            dangerZoneDistances[i] = float.MaxValue;

        int index = 0;
        foreach (Vector3 DZlocation in DangerZoneLocations)
        {
            distanceBoatToDangerZone = Vector3.Distance(DZlocation, RowBoatPrefab.transform.position);
            angleBoatToDangerZone = CalculateAngleToDangerZone(DZlocation);
            dangerZoneDistances[index++] = distanceBoatToDangerZone;

            if (distanceBoatToDangerZone < TriggerDistanceDangerZone)
            {
                inAnyDanger = true;

                // Repulsion via hairdryers (existing pattern), with safety clamp
                if (angleBoatToDangerZone <= 180f)
                {
                    Hairdryer1Ventilator = Mathf.Sin(angleBoatToDangerZone * Mathf.Deg2Rad);
                    Hairdryer2Ventilator = 0f;
                }
                else
                {
                    Hairdryer1Ventilator = 0f;
                    Hairdryer2Ventilator = Mathf.Sin((360f - angleBoatToDangerZone) * Mathf.Deg2Rad);
                }

                overrideActive = true;
            }
        }

        // Safety clamp
        if (Hairdryer1Ventilator > 0f && Hairdryer1Ventilator < HAIRDRYER_MIN_PWM) Hairdryer1Ventilator = HAIRDRYER_MIN_PWM;
        if (Hairdryer2Ventilator > 0f && Hairdryer2Ventilator < HAIRDRYER_MIN_PWM) Hairdryer2Ventilator = HAIRDRYER_MIN_PWM;

        // Turn off if all clear
        bool allClear = true;
        foreach (float d in dangerZoneDistances)
            if (d < 30f) { allClear = false; break; }

        if (allClear && (Hairdryer1Ventilator > 0f || Hairdryer2Ventilator > 0f))
        {
            Hairdryer1Ventilator = 0f;
            Hairdryer2Ventilator = 0f;
        }

        // Logging
        if (inAnyDanger && !wasInDangerLastFrame) dangerZoneHits++;
        wasInDangerLastFrame = inAnyDanger;

        return overrideActive;
    }

    // ============================================================
    //  STEP 5 — EMIT CUES FOR THE CURRENT TARGET
    //  Each guardian uses a distinct modality. Also respects unlocks.
    // ============================================================
    void EmitTargetCues()
    {
        float angle = PrivateAngleOfCurrentTarget;

        // --- If a guardian sting is playing, override everything for a moment ---
        if (Time.time < guardianStingUntilTime)
        {
            EmitGuardianSting();
            return;
        }

        // --- Determine which modality to use for THIS leg ---
        int modalityForThisLeg = GetModalityForCurrentLeg();

        // Reset all continuous outputs first
        TurnOnHeatTOWERLEFT = TurnOnHeatTOWERRIGHT = TurnOnHeatTOWERBACK = TurnOnHeatTOWERFRONT = false;

        switch (modalityForThisLeg)
        {
            case 0: // HEAT
                if (heatUnlocked) EmitHeatCue(angle);
                break;

            case 1: // AIRFLOW
                if (airflowUnlocked) EmitAirflowCue(angle);
                break;

            case 2: // AUDIO
                if (audioUnlocked) EmitAudioCue(angle);
                break;
        }
    }

    int GetModalityForCurrentLeg()
    {
        // While heading to a guardian: use the PREVIOUS guardian's modality
        // (i.e. the most recently unlocked one). The first leg falls back to audio
        // so the player always has something to follow.
        if (activeCheckpointIndex == 0) return 2;             // audio
        return checkpointModalityIndex[activeCheckpointIndex - 1];
    }

    // ---- HEAT ----
    void EmitHeatCue(float angle)
    {
        // Respect safety: max 2 towers on at a time.
        // We use ONE tower for a clean directional signal.
        if (angle >= -45f && angle <= 45f)            TurnOnHeatTOWERFRONT = true;
        else if (angle > 45f && angle <= 135f)         TurnOnHeatTOWERRIGHT = true;
        else if (angle > 135f || angle <= -135f)       TurnOnHeatTOWERBACK = true;
        else if (angle > -135f && angle < -45f)        TurnOnHeatTOWERLEFT = true;

        // Mirror to grouped bools for CuesActuator
        TurnOnHeaterLEFT        = TurnOnHeatTOWERLEFT;
        TurnOnHeaterLEFTMIDDLE  = TurnOnHeatTOWERRIGHT;
        TurnOnHeaterRIGHTMIDDLE = TurnOnHeatTOWERBACK;
        TurnOnHeaterRIGHT       = TurnOnHeatTOWERFRONT;
    }

    // ---- AIRFLOW ----
    void EmitAirflowCue(float angle)
    {
        // Hairdryer 1 = right, Hairdryer 2 = left (per the original example)
        // Intensity scales with |angle| so "more to the side" = stronger.
        float mag = Mathf.Clamp01(Mathf.Abs(angle) / 135f);

        if (angle > 0f)
        {
            Hairdryer1Ventilator = Mathf.Max(HAIRDRYER_MIN_PWM, mag);
            Hairdryer2Ventilator = 0f;
        }
        else
        {
            Hairdryer1Ventilator = 0f;
            Hairdryer2Ventilator = Mathf.Max(HAIRDRYER_MIN_PWM, mag);
        }
    }

    // ---- AUDIO ----
    void EmitAudioCue(float angle)
    {
        // Front / right / back / left = Sound1 / 2 / 3 / 4
        Sound1On = Sound2On = Sound3On = Sound4On = false;
        Sound5On = Sound6On = Sound7On = Sound8On = false;

        if (angle >= -45f && angle <= 45f)              Sound1On = true;
        else if (angle > 45f && angle <= 135f)          Sound2On = true;
        else if (angle > 135f || angle <= -135f)        Sound3On = true;
        else if (angle > -135f && angle < -45f)         Sound4On = true;
    }

    // ---- GUARDIAN STING (Step 4) ----
    void EmitGuardianSting()
    {
        // Warm + wind + sound, briefly, to signal "a guardian joined you"
        TurnOnHeatTOWERFRONT = true;
        TurnOnHeatTOWERBACK  = true;  // 2 towers max, OK
        TurnOnHeaterLEFT        = TurnOnHeatTOWERLEFT;
        TurnOnHeaterLEFTMIDDLE  = TurnOnHeatTOWERRIGHT;
        TurnOnHeaterRIGHTMIDDLE = TurnOnHeatTOWERBACK;
        TurnOnHeaterRIGHT       = TurnOnHeatTOWERFRONT;

        Hairdryer1Ventilator = HAIRDRYER_MIN_PWM;
        Hairdryer2Ventilator = HAIRDRYER_MIN_PWM;

        Sound1On = Sound5On = false;
        Sound8On = true; // distinct sting
    }

    // ============================================================
    //  HELPERS
    // ============================================================
    private float CalculateAngleToDangerZone(Vector3 LocationDZ)
    {
        Vector3 direction = LocationDZ - RowBoatPrefab.transform.position;
        Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
        float signedAngle = Vector3.SignedAngle(RowBoatPrefab.transform.forward, horizontalDir, Vector3.up);
        return Mathf.Repeat(signedAngle + 360f, 360f);
    }

    // ============================================================
    //  STEP 8 — CSV LOGGING
    // ============================================================
    public void WriteCSV()
    {
        if (!File.Exists(filename))
        {
            using (TextWriter tw = new StreamWriter(filename, false))
            {
                tw.WriteLine("Date;Time;TimestampMs;Group;ActiveCheckpoint;" +
                             "AngleToTarget;DistToTarget;" +
                             "HeatUnlocked;AirflowUnlocked;AudioUnlocked;" +
                             "DangerZoneHits;" +
                             "BoatX;BoatY;BoatZ");
            }
        }

        int TimeStampMilliseconds =
            (((DateTime.Now.Hour * 3600) + (DateTime.Now.Minute * 60) + DateTime.Now.Second) * 1000)
            + DateTime.Now.Millisecond - startingTimeMillisec;

        Vector3 boat = RowBoatPrefab.transform.position;
        Vector3 tgt  = GetCurrentTargetPosition();
        float distToTarget = Vector3.Distance(tgt, boat);

        using (TextWriter tw = new StreamWriter(filename, true))
        {
            tw.WriteLine(
                $"{DateTime.Now:dd-MM-yyyy};{DateTime.Now:HH:mm:ss};{TimeStampMilliseconds};" +
                $"{GroupName};{activeCheckpointIndex};" +
                $"{PrivateAngleOfCurrentTarget:F2};{distToTarget:F2};" +
                $"{(heatUnlocked ? 1 : 0)};{(airflowUnlocked ? 1 : 0)};{(audioUnlocked ? 1 : 0)};" +
                $"{dangerZoneHits};" +
                $"{boat.x:F2};{boat.y:F2};{boat.z:F2}");
        }
    }
}