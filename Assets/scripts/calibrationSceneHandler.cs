using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static AppData;

public class calibrationSceneHandler : MonoBehaviour
{
    private string selectedMechanism;
    private bool isCalibrating = false;
    private float togetherPosition = 0.0f;    
    private float separationPosition = 10.0f;  
    public TextMeshProUGUI textMessage;
    public TextMeshProUGUI mechText;
    private static bool connect = false;

    void Start()
    {
        selectedMechanism = MechanismSelection.selectedOption;
        int mechNumber = PlutoComm.GetPlutoCodeFromLabel(PlutoComm.MECHANISMS, selectedMechanism);
        mechText.text = PlutoComm.MECHANISMSTEXT[mechNumber];
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && !isCalibrating)
        {
            StartCoroutine(AutoCalibrateHOC());
        }

        if (ConnectToRobot.isPLUTO && !connect)
        {
            PlutoComm.OnButtonReleased += onPlutoButtonReleased;
            connect = true;
        }

        if (isCalibrating)
        {
            Debug.Log("Pluto button released, starting calibration");
            StartCoroutine(AutoCalibrateHOC());
            isCalibrating = false;
        }
    }

    IEnumerator AutoCalibrateHOC()
    {
        textMessage.text = "Calibrating...";
 
        float currentDistance = PlutoComm.getHOCDisplay(PlutoComm.angle);

        ApplyTorqueToMoveHandles(currentDistance, togetherPosition);
        yield return new WaitForSeconds(1.0f);

        float currentDistance1 = PlutoComm.getHOCDisplay(PlutoComm.angle);
        if (!CheckPositionTogether(currentDistance1, togetherPosition)) yield break;

        PlutoComm.calibrate(selectedMechanism);

        ApplyTorqueToMoveHandles(currentDistance, separationPosition);

        yield return new WaitForSeconds(1.0f);
        currentDistance = PlutoComm.getHOCDisplay(PlutoComm.angle);
        if (!CheckPositionSeparation(currentDistance, separationPosition)) yield break;

        ApplyTorqueToMoveHandles(currentDistance, togetherPosition);

        yield return new WaitForSeconds(1.0f);
        currentDistance = PlutoComm.getHOCDisplay(PlutoComm.angle);

        isCalibrating = false;
        textMessage.text = "Calibration Done";
        textMessage.color = Color.green;
        PlutoComm.setControlType(PlutoComm.CONTROLTYPE[0]);
    }

    private void ApplyTorqueToMoveHandles(float currentPos, float targetPos)
    {
        float distance = targetPos - currentPos;
        float torqueValue = (distance > 0) ? -0.09f : 0.09f;   // torque values Nm
        PlutoComm.setControlType("TORQUE");
        PlutoComm.setControlTarget(torqueValue);
    }

    private void onPlutoButtonReleased()
    {
        isCalibrating = true;
    }

    
    private bool CheckPositionTogether(float currentPosition, float targetPosition)
    {
        if (currentPosition <= 1.5f)
        {
            return true;
        }
        else
        {
            textMessage.text = $"Error: Together Position NOT reached! Current: {currentPosition}";
            textMessage.color = Color.red;
            isCalibrating = false;
            return false;
        }
    }


    private bool CheckPositionSeparation(float currentPosition, float targetPosition)
    {
        if (currentPosition >= 9.0f)
        {
            return true;
        }
        else
        {
            textMessage.text = $"Error: Separation Position NOT reached! Current: {currentPosition}";
            textMessage.color = Color.red;
            isCalibrating = false;
            return false;
        }
    }

    private void OnDestroy()
    {
        if (ConnectToRobot.isPLUTO)
        {
            PlutoComm.OnButtonReleased -= onPlutoButtonReleased;
        }
    }
}
