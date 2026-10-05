bool UpdateDangerZones()
{
    bool overrideActive = false;
    bool inAnyDanger = false;

    // Track which side is "hottest" this frame (0 = none, 1 = front, 2 = right, 3 = back, 4 = left)
    int heatSide = 0;

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
            overrideActive = true;

            // --- Hairdryers (existing behaviour) ---
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

            // --- Heat warning: which tower? ---
            // Danger angle is 0..360, where 0 = in front, 90 = right, 180 = behind, 270 = left.
            int thisHeatSide = 0;
            if (angleBoatToDangerZone >= 315f || angleBoatToDangerZone < 45f)   thisHeatSide = 1; // FRONT
            else if (angleBoatToDangerZone >= 45f  && angleBoatToDangerZone < 135f) thisHeatSide = 2; // RIGHT
            else if (angleBoatToDangerZone >= 135f && angleBoatToDangerZone < 225f) thisHeatSide = 3; // BACK
            else                                                                thisHeatSide = 4; // LEFT

            // Keep the closest danger zone's side
            if (heatSide == 0) heatSide = thisHeatSide;
        }
    }

    // --- Safety clamps (slide 95) ---
    if (Hairdryer1Ventilator > 0f && Hairdryer1Ventilator < HAIRDRYER_MIN_PWM) Hairdryer1Ventilator = HAIRDRYER_MIN_PWM;
    if (Hairdryer2Ventilator > 0f && Hairdryer2Ventilator < HAIRDRYER_MIN_PWM) Hairdryer2Ventilator = HAIRDRYER_MIN_PWM;

    // --- Apply heat warning (overrides any navigation heat for this frame) ---
    if (heatSide != 0 && heatUnlocked)
    {
        TurnOnHeatTOWERFRONT = (heatSide == 1);
        TurnOnHeatTOWERRIGHT = (heatSide == 2);
        TurnOnHeatTOWERBACK  = (heatSide == 3);
        TurnOnHeatTOWERLEFT  = (heatSide == 4);

        // Mirror to grouped bools so CuesActuator actually fires them
        TurnOnHeaterLEFT        = TurnOnHeatTOWERLEFT;
        TurnOnHeaterLEFTMIDDLE  = TurnOnHeatTOWERRIGHT;
        TurnOnHeaterRIGHTMIDDLE = TurnOnHeatTOWERBACK;
        TurnOnHeaterRIGHT       = TurnOnHeatTOWERFRONT;

        Debug.Log($"<color=red>DANGER: heat warning on side {heatSide} " +
                  $"(angle {angleBoatToDangerZone:F0}°)</color>");
    }

    // --- All clear? ---
    bool allClear = true;
    foreach (float d in dangerZoneDistances)
        if (d < 30f) { allClear = false; break; }

    if (allClear && (Hairdryer1Ventilator > 0f || Hairdryer2Ventilator > 0f))
    {
        Hairdryer1Ventilator = 0f;
        Hairdryer2Ventilator = 0f;
    }

    if (inAnyDanger && !wasInDangerLastFrame) dangerZoneHits++;
    wasInDangerLastFrame = inAnyDanger;

    return overrideActive;
}
