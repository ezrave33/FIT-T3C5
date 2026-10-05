using UnityEngine;

/// <summary>
/// Hardware test script — press keys to fire each heat tower.
/// DO NOT include in the graded build (only NavigationalStrategy_ADAPTME may drive cues).
/// </summary>
public class HeatPanelTester : MonoBehaviour
{
    public GameObject eventManager; // drag the EventManager here

    void Update()
    {
        var ns = eventManager.GetComponent<NavigationalStrategy_ADAPTME>();
        if (ns == null) return;

        // Reset every frame
        ns.TurnOnHeatTOWERFRONT = false;
        ns.TurnOnHeatTOWERLEFT  = false;
        ns.TurnOnHeatTOWERBACK  = false;
        ns.TurnOnHeatTOWERRIGHT = false;

        if (Input.GetKey(KeyCode.W)) ns.TurnOnHeatTOWERFRONT = true;
        if (Input.GetKey(KeyCode.A)) ns.TurnOnHeatTOWERLEFT  = true;
        if (Input.GetKey(KeyCode.S)) ns.TurnOnHeatTOWERBACK  = true;
        if (Input.GetKey(KeyCode.D)) ns.TurnOnHeatTOWERRIGHT = true;

        // Mirror to CuesActuator
        ns.TurnOnHeaterLEFT        = ns.TurnOnHeatTOWERLEFT;
        ns.TurnOnHeaterLEFTMIDDLE  = ns.TurnOnHeatTOWERRIGHT;
        ns.TurnOnHeaterRIGHTMIDDLE = ns.TurnOnHeatTOWERBACK;
        ns.TurnOnHeaterRIGHT       = ns.TurnOnHeatTOWERFRONT;
    }
}
