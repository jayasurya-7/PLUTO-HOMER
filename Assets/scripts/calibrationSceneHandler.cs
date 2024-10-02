using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AppData;

public class calibrationSceneHandler : MonoBehaviour
{
    private string selectedMechanism;
    private bool isCalibrating = false;

    // Define the target positions in centimeters (e.g., 0 cm for together, 9.96 cm for separation)
    private float togetherPosition = 0.0f;
    private float separationPosition = 9.96f;

    // Start is called before the first frame update
    void Start()
    {
        // Read the selected mechanism from MechanismSelection class
        selectedMechanism = MechanismSelection.selectedOption;
        Debug.Log("Selected Mechanism: " + selectedMechanism);

        // Check if HOC mechanism is selected to enable auto-calibration
        if (selectedMechanism == "hoc")
        {
            Debug.Log("HOC mechanism selected. Ready for auto-calibration.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Simulate Pluto button press using the 'C' key for testing
        if (Input.GetKeyDown(KeyCode.C) && !isCalibrating)
        {
            StartCoroutine(AutoCalibrateHOC());
        }
    }

    // Coroutine to handle the HOC auto-calibration process
    IEnumerator AutoCalibrateHOC()
    {
        isCalibrating = true;
        float dist = PlutoComm.getHOCDisplay(PlutoComm.angle);
        // Step 1: Calculate distance between handles
        float currentDistance = dist;
        Debug.Log("Initial Distance between handles: " + currentDistance);

        // Step 2: Apply torque to bring handles together
        ApplyTorqueToMoveHandles(currentDistance, togetherPosition);
        Debug.Log("Applying torque to bring handles together...");

        // Wait for 1 second to allow the handles to move
        yield return new WaitForSeconds(1.0f);

        // Step 3: Move handles to separation position (9.96 cm)
        ApplyTorqueToMoveHandles(togetherPosition, separationPosition);
        Debug.Log("Moving handles to separation position: " + separationPosition + " cm");

        // Wait for 1 second to reach the target separation
        yield return new WaitForSeconds(1.0f);

        // Step 4: Bring handles together again
        ApplyTorqueToMoveHandles(separationPosition, togetherPosition);
        Debug.Log("Bringing handles together again...");

        // Wait for 1 second to complete the movement
        yield return new WaitForSeconds(1.0f);

        // Calibration complete
        isCalibrating = false;
        Debug.Log("Auto-calibration complete!");
    }

    // Function to calculate the current distance between the handles
    private float CalculateDistanceBetweenHandles()
    {
        // Dummy implementation for calculating distance. Replace with actual logic.
        float leftHandlePosition = 0.0f;  // Replace with actual left handle position
        float rightHandlePosition = 10.0f;  // Replace with actual right handle position

        // Calculate the distance between the handles
        return Mathf.Abs(rightHandlePosition - leftHandlePosition);
    }

    // Function to apply torque to move the handles to the target position
    private void ApplyTorqueToMoveHandles(float currentPos, float targetPos)
    {
        float distance = targetPos - currentPos;

        // Determine the direction of the torque: positive or negative
        float torqueValue = (distance > 0) ? 10.0f : -10.0f;

        // Send the torque command to the device (Placeholder: Replace with actual command)
        SendTorqueCommandToPlutoDevice(torqueValue);
    }

    // Placeholder function to send torque command to Pluto device
    private void SendTorqueCommandToPlutoDevice(float torqueValue)
    {
        Debug.Log("Sending torque command: " + torqueValue);
        // Implement the actual command to control the torque on the device
        // This might involve using your serial communication script to send torque data to the device
    }
}
