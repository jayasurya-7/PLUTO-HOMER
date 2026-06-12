using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TS.DoubleSlider;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System;


public class AssistsceneHandler : MonoBehaviour
{
    enum AssessStates
    {
        INIT,
        ASSESS
    };
    private bool isButtonPressed = false;
    public TMP_Text lText;
    public TMP_Text rText;
    public TMP_Text insText;
    public TMP_Text cText, inst, inst1;
    public TMP_Text relaxText;
    
    public TMP_Text jointAngle;
    public TextMeshProUGUI mechName;

    private int _linx, _rinx;
    private float _tmin = 0f, _tmax  =0f;

    public GameObject CurrPositioncursor;
    public GameObject redoButton;
    private AssessStates _state;

    private float angLimit;
    public DoubleSlider apromSlider;
    public bool isSelected = false;
    public Image shadow;

    //public assessmentSceneHandler panelControl;

    private List<string[]> DirectionText = new List<string[]>
     {
         new string[] { "Flexion", "Extension" },
         new string[] { "Ulnar Dev", "Radial Dev" },
         new string[] { "Pronation", "Supination" },
         new string[] { "Open", "Closed"},
         new string[] { "", "" },
         new string[] { "", "" }
     };


     float currentAngle = PlutoComm.angle;
    float targetPositiveEnd ;  // Positive limit
    float targetNegativeEnd ; // Negative limit
    float endpointTolerance = 5f;
    bool runOnce1 = false;
    float torque = 0f;

    // Track if reached both ends
    bool reachedPositive = false;
    bool reachedNegative = false;

    // Store raw APROM assessment samples
    private List<(float angle, float torque, float time)> _aromSamples = new();

    // Flags to track which side we're heading toward
    bool goingPositive = true;

    // For tracking stuck situation
    float previousAngle = 0f;
    float stuckTimer = 0f;
    float stuckThresholdTime = 3.0f; // seconds
    // Add these as class-level variables:
    int positiveStuckAttempts = 0;
    int negativeStuckAttempts = 0;
    const int maxStuckAttempts = 2;
    float maxAngle = 0f;
    float minAngle = 0f;
    bool onceReached = false, firstPositiveStart=true, firstNegativeStart = true;
    float trailDuration = 1f;
    float stopClock=0f;
    float positiveTimer = 0f;
    float negativeTimer = 0f;
    float maxDirectionDuration = 15f;
    bool runOnce = false;

    void Start()
    {
          // Set mechanism name
        mechName.text = PlutoComm.MECHANISMSTEXT[PlutoComm.GetPlutoCodeFromLabel(PlutoComm.MECHANISMS, AppData.Instance.selectedMechanism.name)];
  
        InitializeAssessment();
    }
    void ResetAssessment()
    {
        // PlutoComm.setControlType("NONE");
        runOnce1 = false;
        torque = 0f;
        goingPositive = true;
        reachedPositive = false;
        reachedNegative = false;
        onceReached = false;
        firstPositiveStart = true;
        firstNegativeStart = true;
        stuckTimer = 0f;
        positiveStuckAttempts = 0;
        negativeStuckAttempts = 0;
        minAngle = 0f;
        maxAngle = 0f;
        _tmin = 0f;
        _tmax = 0f;
        stopClock = 0f;
        apromSlider.minAng = 0;
        apromSlider.maxAng = 0;
        inst.text = "";
        inst1.text = "";
        shadow.color = new Color(1f, 0.5f, 0f, 0.7f); // Orange with 70% opacity

        redoButton.SetActive(false);
        runOnce = false;
}


    public void InitializeAssessment()
    {
        // Disable control.
        ResetAssessment();

        // Update the min and max values.
        angLimit = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? 93f : PlutoComm.MECHOFFSETVALUE[PlutoComm.mechanism] + 10.0f;

        if (AppData.Instance.selectedMechanism.IsMechanism("HOC"))
        {
            // HOC: use measured PROM values (negative = OPEN, positive = CLOSED)
            targetPositiveEnd = 0.0f;  // CLOSED limit
            targetNegativeEnd = AppData.Instance.selectedMechanism.newRom.promMin;  // OPEN limit
        }
        else
        {
            // Non-HOC: standard positive/negative ranges
            targetNegativeEnd = AppData.Instance.selectedMechanism.newRom.promMin;
            targetPositiveEnd = AppData.Instance.selectedMechanism.newRom.promMax;
        }

        // Unified slider setup: measured ROM for HOC, -angLimit to angLimit for others
        float sliderMin = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? -93f : targetNegativeEnd;
        float sliderMax = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? 0f : targetPositiveEnd;

        apromSlider.Setup(sliderMin, sliderMax, 0, 0);
        apromSlider.minAng = 0;
        apromSlider.maxAng = 0;
        apromSlider.startAssessment(PlutoComm.angle);
        // cText label not needed with unified sliders
        cText.gameObject.SetActive(false);
        inst1.text = "Press PLUTO button to start the AAN";

        // Update the left and right text.
        // HOC labels are always fixed (not affected by training side)
        if (AppData.Instance.selectedMechanism.IsMechanism("HOC"))
        {
            lText.text = "Open";    // negative side
            rText.text = "Closed";  // positive side
        }
        else
        {
            (_rinx, _linx) = AppData.Instance.IsTrainingSide("RIGHT") ? (1, 0) : (0, 1);
            rText.text = DirectionText[PlutoComm.mechanism - 1][_rinx];
            lText.text = DirectionText[PlutoComm.mechanism - 1][_linx];
        }

        // Set the state to INIT.
        _state = AssessStates.INIT;
        inst.text = "";
        // inst1.text = "";

        // Attach callback for PLUTO button release.
        PlutoComm.OnButtonReleased +=    OnPlutoButtonReleased;

        UpdateStatusText();
    }

    IEnumerator RunAssessment()
    {
        float rampDownTimer = 0f;
        const float RAMP_DOWN_DURATION = 2f;  // 2 seconds to ramp down from full to zero

        while (!reachedPositive || !reachedNegative)
        {
            _aromSamples.Add((PlutoComm.angle, torque, stopClock));
            //stopClock -= Time.deltaTime;
            //stopClock = Mathf.Max(0, stopClock);

            // Once both endpoints reached, skip direction-specific logic and go straight to ramp-down
            if (reachedPositive && reachedNegative)
            {
                rampDownTimer += 0.05f;
                float rampProgress = Mathf.Clamp01(rampDownTimer / RAMP_DOWN_DURATION);
                torque = Mathf.Lerp(torque, 0f, rampProgress);
                PlutoComm.setControlTarget(torque);

                if (rampDownTimer >= RAMP_DOWN_DURATION)
                {
                    torque = 0f;
                    PlutoComm.setControlTarget(0f);
                    break;  // Exit loop after ramp-down complete
                }

                previousAngle = currentAngle;
                yield return new WaitForSeconds(0.05f);
                stopClock += 0.05f;
                continue;
            }

            float deltaAngle = Mathf.Abs(currentAngle - previousAngle);
            bool movingTowardTarget = (goingPositive && !AppData.Instance.selectedMechanism.IsMechanism("HOC"))
                                        ? (currentAngle > previousAngle)
                                        : (currentAngle < previousAngle);

            // Stuck detection
            stuckTimer = (!movingTowardTarget || deltaAngle < 3f)
                            ? stuckTimer + Time.deltaTime
                            : 0f;
            float timeFraction = Mathf.Clamp01(stopClock / trailDuration);


            //   float timeFraction = Mathf.Clamp01((trailDuration - stopClock) / trailDuration);
            float smoothTorque = Mathf.SmoothStep(0f, 1f, timeFraction);

            if (goingPositive && !reachedPositive)
            {
                if (firstPositiveStart)
                {
                    trailDuration = 7f;
                    stopClock = 0f;
                    torque = 0f;
                    onceReached = false;
                    firstPositiveStart = false;
                    positiveTimer = 0f;
                }

                positiveTimer += 0.05f;
                if (isButtonPressed && !reachedPositive)
                {
                    isButtonPressed = false;
                    reachedPositive = true;
                    maxAngle = currentAngle;
                    torque = 0f;
                    stopClock = 0f;
                    PlutoComm.setControlTarget(0);
                    // yield return new WaitForSeconds(0.1f);
                    goingPositive = false;
                    yield return null;
                    continue;
                }

                if (positiveTimer >= maxDirectionDuration)
                {
                    reachedPositive = true;
                    maxAngle = currentAngle;
                    torque = 0f;
                    PlutoComm.setControlTarget(0);
                    stopClock = 0f;
                    // yield return new WaitForSeconds(0.1f);
                    goingPositive = false;
                    yield return null;
                    continue;
                }

                if (currentAngle < targetPositiveEnd - endpointTolerance)
                {
                    if (!onceReached)
                        torque = smoothTorque;

                    if (stuckTimer > stuckThresholdTime && torque >= 0.99f)
                    {
                        stuckTimer = 0f;
                        positiveStuckAttempts++;
                        onceReached = true;
                    }

                    if (positiveStuckAttempts >= maxStuckAttempts)
                    {
                        reachedPositive = true;
                        maxAngle = currentAngle;
                        torque = 0f;
                        PlutoComm.setControlTarget(0);
                        // yield return new WaitForSeconds(0.1f);
                        goingPositive = false;
                        yield return null;
                        continue;
                    }

                    if (onceReached && currentAngle > previousAngle && !reachedNegative)
                        torque -= 0.1f;

                    torque = Mathf.Clamp(torque, 0.0f, 1.0f);
                    // if (torque == -1.0f) Debug.Log("here is the issue");
                    PlutoComm.setControlTarget(torque);
                }
                else
                {
                    reachedPositive = true;
                    maxAngle = currentAngle;
                    stopClock = 0f;
                    torque = 0f;
                    PlutoComm.setControlTarget(0);
                    //yield return new WaitForSeconds(0.1f);
                    goingPositive = false;
                }
            }
            else if (!reachedNegative)
            {
                if (firstNegativeStart)
                {
                    PlutoComm.setControlTarget(0f);
                    trailDuration = 7f;
                    stopClock = 0f;
                    torque = 0f;
                    onceReached = false;
                    firstNegativeStart = false;
                    negativeTimer = 0f;
                }
                negativeTimer += 0.05f;

                if (isButtonPressed && !reachedNegative)
                {
                    PlutoComm.setControlType("NONE");
                    yield return new WaitForSeconds(0.1f);
                    isButtonPressed = false;
                    reachedNegative = true;
                    minAngle = currentAngle;
                    torque = 0f;
                    redoButton.SetActive(true);
                    inst.text = $"APROM Reached both ends min : {_tmin},max :{_tmax}.";
                    inst1.text = "Press PLUTO button to move next scene";
                    yield return null;
                    continue;   
                }

                if (negativeTimer >= maxDirectionDuration)
                {
                    PlutoComm.setControlType("NONE");
                    yield return new WaitForSeconds(0.1f);
                    reachedNegative = true;
                    minAngle = currentAngle;
                    torque = 0f;
                    redoButton.SetActive(true);
                    inst.text = $"APROM Reached both ends min : {_tmin},max :{_tmax}.";
                    inst1.text = "Press PLUTO button to move next scene";
                    yield return null;
                    continue;
                }

                if (currentAngle > targetNegativeEnd + endpointTolerance)
                {
                    float revSmoothTorque = -Mathf.SmoothStep(0f, 1f, timeFraction);
                    // if (revSmoothTorque == -1.0f) Debug.Log("2nd place is the issue");
                    if (!onceReached)
                        torque = revSmoothTorque;

                    if (stuckTimer > stuckThresholdTime && torque <= -0.99f)
                    {
                        stuckTimer = 0f;
                        negativeStuckAttempts++;
                        onceReached = true;
                    }

                    if (negativeStuckAttempts >= maxStuckAttempts)
                    {
                        PlutoComm.setControlType("NONE");
                        yield return new WaitForSeconds(0.1f);
                        reachedNegative = true;
                        minAngle = currentAngle;
                        torque = 0f;
                        redoButton.SetActive(true);
                        inst.text = $"APROM Reached both ends min : {_tmin},max :{_tmax}.";
                        inst1.text = "Press PLUTO button to move next scene";
                        // shadow.color = new Color(1f, 0.5f, 0f, 0.5f);
                     shadow.color = new Color(0.2f, 0.85f, 0.4f, 0.8f); 

                        yield return null;
                        continue;
                    }

                    if (onceReached && currentAngle < previousAngle && !reachedPositive)
                        torque += 0.1f;

                    torque = Mathf.Clamp(torque, -1.0f, 0.0f);
                    // torque = Mathf.Min(0.0f, Mathf.Clamp(torque, -1.0f, 0.0f));

                    PlutoComm.setControlTarget(torque);
                }
                else
                {
                    PlutoComm.setControlType("NONE");
                    yield return new WaitForSeconds(0.1f);
                    reachedNegative = true;
                    minAngle = currentAngle;
                    torque = 0f;
                    redoButton.SetActive(true);
                    inst.text = $"APROM Reached both ends min : {_tmin},max :{_tmax}.";
                    inst1.text = "Press PLUTO button to move next scene";
                    //  shadow.color = new Color(0f, 240f, 240f, 1f); 
                     shadow.color = new Color(0.2f, 0.85f, 0.4f, 0.8f); 
                    
                }
            }

            previousAngle = currentAngle;
            yield return new WaitForSeconds(0.05f); // ⏱️ Delay of 0.05 sec between each torque update
            stopClock += 0.05f; // match WaitForSeconds

        }

}



    public void OnExit()
    {
        PlutoComm.setControlType("NONE");
        SceneManager.LoadScene("ASSESS");
    }

    void Update()
    {
        PlutoComm.sendHeartbeat();

        currentAngle = PlutoComm.angle;
        jointAngle.text = $"Angle: {((int)PlutoComm.angle).ToString()}";
        runAssessmentStateMachine();
    }

    void runAssessmentStateMachine()
    {
        Debug.Log($"state : {_state}");
        CurrPositioncursor.SetActive(true);
        switch (_state)
        {
            case AssessStates.INIT:
                if (isButtonPressed || Input.GetKeyDown(KeyCode.Return))
                {
                    PlutoComm.setControlType("TORQUE");

                    //if(PlutoComm.CONTROLTYPE[PlutoComm.controlType]=="TORQUE") startAssessment();
                    startAssessment();
                    isButtonPressed = false;
                }
               // relaxText.text = FormatRelaxText(AppData.Instance.selectedMechanism.oldRom.promMin, AppData.Instance.selectedMechanism.oldRom.promMax);
                break;
            case AssessStates.ASSESS:
                // runAssessment();
                if (!runOnce1)
                {
                    shadow.color = new Color(0f, 255f, 79f, 0.7f); // Orange with 70% opacity
               StartCoroutine(RunAssessment());
                    runOnce1 = true;
                }

                _tmin = apromSlider.minAng;
                _tmax = apromSlider.maxAng;
                 if (reachedNegative && reachedPositive)
                    {
                        PlutoComm.setControlType("NONE");

                        if (isButtonPressed)
                        {
                            AppData.Instance.selectedMechanism.SetNewAPromValues(_tmin, _tmax);
                            PlutoComm.setControlType("NONE");
                            OnSaveClick();
                            isButtonPressed = false;

                            if (AppData.Instance.selectedMechanism.apromCompleted)

                            if(AppData.isPlanSetup)
                            SceneManager.LoadScene("PLANSETUP");
                            else
                            SceneManager.LoadScene("CHGAME");
                        }
                    }
                break;
        }
    }

    public void OnRedoPromClick()
    {
        InitializeAssessment();
        Debug.Log("Redo PROM: Reset to INIT state.");
    }

    public void OnPlutoButtonReleased()
    {
        isButtonPressed = true;
    }

    private float ConvertToCM(float value) => Mathf.Abs(Mathf.Deg2Rad * value * 6f);

    public void OnNextButtonClick()
    {
        PlutoComm.setControlType("NONE");
        OnSaveClick();

    }

    public void OnSaveClick()
    {
        StoreAPromRawData();
        AppData.Instance.selectedMechanism.SaveAssessmentData();
        apromSlider.UpdateMinMaxvalues = false;
        CurrPositioncursor.SetActive(false);
    }

    private void StoreAPromRawData()
    {
        string mechName = AppData.Instance.selectedMechanism.name;
        string dateString = System.DateTime.Now.ToString("yyyy-MM-dd");
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Create assistProfile folder under patient data
        string folderPath = Path.Combine(Path.GetDirectoryName(DataManager.rawPath), "assistProfile");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, $"assistProfile_{dateString}_{mechName}.csv");

        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            writer.WriteLine("DateTime,Mechanism,Time(s),Angle(deg),Torque");
            foreach (var sample in _aromSamples)
            {
                writer.WriteLine($"{timestamp},{mechName},{sample.time:F2},{sample.angle:F1},{sample.torque:F2}");
            }
        }

        _aromSamples.Clear();
        AppLogger.LogInfo($"[APROM] Raw data stored: {filePath}");
    }

    private string FormatRelaxText(float min, float max)
    {
        return AppData.Instance.selectedMechanism.IsMechanism("HOC") ?
            $"Prev PROM: {ConvertToCM(min).ToString("0.0")}cm : {ConvertToCM(max).ToString("0.0")}cm (Aperture: {ConvertToCM(max - min).ToString("0.0")}cm)" :
            $"Prev PROM: {(int)min} : {(int)max} ({(int)(max - min)}°)";
    }

    public void startAssessment()
    {
        _state = AssessStates.ASSESS;
        apromSlider.minAng = 0;
        apromSlider.maxAng = 0;
        Debug.Log("Assessment started");
        apromSlider.startAssessment(PlutoComm.angle);
        apromSlider.UpdateMinMaxvalues = true;
        inst1.text = "";
    }

    private void UpdateStatusText()
    {
        jointAngle.text = $"Angle: {PlutoComm.angle.ToString("0.0")}";
    }
}




