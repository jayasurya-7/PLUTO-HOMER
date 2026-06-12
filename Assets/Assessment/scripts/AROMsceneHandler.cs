
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TS.DoubleSlider;
using UnityEngine.SceneManagement;

public class AROMsceneHandler : MonoBehaviour
{
    enum AssessStates
    {
        INIT,
        TRIAL_RUNNING,
        TRIAL_COMPLETE,
        ASSESSMENT_COMPLETE
    };

    // --- UI References ---
    public TMP_Text lText;
    public TMP_Text rText;
    public TMP_Text insText;
    public TMP_Text cText;
    public TMP_Text relaxText;
    public TMP_Text feedbackText;
    public TMP_Text jointAngle;
    public TMP_Text directionArrow;

    public GameObject nextButton;
    public GameObject startButton;
    public GameObject CurrPositioncursor;
    public UnityEngine.UI.Image aromLockedImage;

    public DoubleSlider aromSlider;
    public bool isSelected = false;
    public assessmentSceneHandler panelControl;

    // --- State ---
    private AssessStates _state;
    private bool isButtonPressed = false;
    private float angLimit;
    private int _linx, _rinx;
    private float _tmin, _tmax;

    // --- Trial / Cycle structure ---
    private const int NUM_TRIALS = 1;
    private const int CYCLES_PER_TRIAL = 5;
    private int _currentTrial = 0;
    private int _completedCycles = 0;
    private List<(float lo, float hi)>[] _trialCycles;
    private (float lo, float hi)[] _trialBests;

    // --- Velocity rolling window (10 signed samples, deg/s) ---
    private const int VEL_WINDOW = 10;
    private Queue<float> _velWindow = new Queue<float>(VEL_WINDOW);
    private float _lastAngle;
    private bool _atRest;

    // === NON-HOC: Direction-based bidirectional algorithm ===
    private int _currDirection = 0;   // +1 = toward HI, -1 = toward LO, 0 = neutral
    private bool _wasGoingHi = false; // patient has moved toward HI this cycle
    private bool _wasGoingLo = false; // patient has moved toward LO this cycle
    private float _hiArmAngle;        // angle when HI direction was first detected this cycle
    private float _loArmAngle;        // angle when LO direction was first detected this cycle
    private const float VEL_DIR_THRESHOLD = 2f; // deg/s to classify direction
    private float _peakHi = float.NegativeInfinity;
    private float _peakLo = float.PositiveInfinity;
    private bool _hiFinalized, _loFinalized;
    private float _finalizedHi, _finalizedLo;

    // === HOC: State-based opening/closing algorithm ===
    enum HocState { OPENING, CLOSING }
    private HocState _hocState = HocState.OPENING;
    private float _hocPeakOpen;     // max angle reached during OPENING
    private float _hocPeakClose;    // min angle reached during CLOSING
    private bool _hocOpenFinalized, _hocCloseFinalized;
    private const float HOC_REVERSAL_THRESHOLD_DEG = 5f;  // 5 degrees, same as non-HOC

    // --- Algorithm constants ---
    private const float REVERSAL_THRESHOLD = 5f;
    private const float MIN_AROM_RANGE     = 5f;

    // --- Direction text lookup ---
    private List<string[]> DirectionText = new List<string[]>
    {
        new string[] { "Flexion",   "Extension" },
        new string[] { "Radial Dev","Ulnar Dev"  },
        new string[] { "Pronation", "Supination" },
        new string[] { "Closed",      "Open"     },
        new string[] { "",          ""           },
        new string[] { "",          ""           }
    };

    // --- Cycle marker colors — one distinct color per cycle position (1–5) ---
    private static readonly Color[] CycleColors = new Color[]
    {
        new Color(1.0f, 0.2f, 0.2f, 1f),  // cycle 1 — red
        new Color(1.0f, 0.6f, 0.1f, 1f),  // cycle 2 — orange
        new Color(0.9f, 0.9f, 0.0f, 1f),  // cycle 3 — yellow
        new Color(0.2f, 0.8f, 0.3f, 1f),  // cycle 4 — green
        new Color(0.2f, 0.5f, 1.0f, 1f),  // cycle 5 — blue
    };

    // -------------------------------------------------------------------------
    void Start()
    {
        AppLogger.LogInfo(
            $"ROM data loaded for mechanism {AppData.Instance.selectedMechanism.name}: "
            + $"AROM [{AppData.Instance.selectedMechanism.oldRom.aromMin}, "
            + $"{AppData.Instance.selectedMechanism.oldRom.aromMax}], "
            + $"PROM [{AppData.Instance.selectedMechanism.oldRom.promMin}, "
            + $"{AppData.Instance.selectedMechanism.oldRom.promMax}]"
        );
        InitializeAssessment();
    }

    public void InitializeAssessment()
    {
        PlutoComm.setControlType("NONE");
        nextButton.SetActive(false);

        if (aromLockedImage != null)
            aromLockedImage.color = Color.white;

        angLimit = AppData.Instance.selectedMechanism.IsMechanism("HOC")
            ? 93f  // HOC uses -93 to 0 range
            : PlutoComm.MECHOFFSETVALUE[PlutoComm.mechanism] + 10.0f;

        // All mechanisms use the same unified slider setup
        float sliderMin = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? -93f : -angLimit;
        float sliderMax = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? 0f : angLimit;

        float startAngle = PlutoComm.angle;
        aromSlider.Setup(sliderMin, sliderMax, startAngle, startAngle);
        aromSlider.UpdateMinMaxvalues = false;
        aromSlider.HideHandles();
        aromSlider.minAng = startAngle;
        aromSlider.maxAng = startAngle;

        // cText label not needed with unified sliders

        // HOC labels are always fixed (not affected by training side)
        if (AppData.Instance.selectedMechanism.IsMechanism("HOC"))
        {
            lText.text = "Open";    // -93° side (left)
            rText.text = "Closed";  // 0° side (right)
        }
        else
        {
            (_rinx, _linx) = AppData.Instance.IsTrainingSide("RIGHT") ? (1, 0) : (0, 1);
            rText.text = DirectionText[PlutoComm.mechanism - 1][_rinx];
            lText.text = DirectionText[PlutoComm.mechanism - 1][_linx];
        }

        // Allocate trial data arrays
        _trialCycles = new List<(float, float)>[NUM_TRIALS];
        _trialBests  = new (float, float)[NUM_TRIALS];
        for (int i = 0; i < NUM_TRIALS; i++)
            _trialCycles[i] = new List<(float, float)>();

        _currentTrial    = 0;
        _completedCycles = 0;
        _velWindow.Clear();
        _lastAngle = PlutoComm.angle;
        _atRest    = false;

        _state = AssessStates.INIT;
        PlutoComm.OnButtonReleased += OnPlutoButtonReleased;

        UpdateStatusText();
    }

    void OnDestroy()
    {
        if (ConnectToRobot.isPLUTO)
            PlutoComm.OnButtonReleased -= OnPlutoButtonReleased;
    }

    // -------------------------------------------------------------------------
    void Update()
    {
        if (isSelected)
        {
            RunStateMachine();
            UpdateStatusText();
        }
        else
        {
            // PROM tab active — show whatever AROM result we have
            if (_state == AssessStates.ASSESSMENT_COMPLETE)
            {
                string unit = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? "cm" : "°";
                float lo = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(_tmin) : _tmin;
                float hi = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(_tmax) : _tmax;
                relaxText.text = "Assessment Completed\n"
                    + FormatRelaxText(AppData.Instance.selectedMechanism.oldRom.aromMin,
                                      AppData.Instance.selectedMechanism.oldRom.aromMax)
                    + $"\nCurrent AROM: {lo:F1}{unit} : {hi:F1}{unit}"
                    + $" ({Mathf.Abs(hi - lo):F1}{unit})";
            }
            else
            {
                relaxText.text = "Assessment Completed\n"
                    + FormatRelaxText(AppData.Instance.selectedMechanism.oldRom.aromMin,
                                      AppData.Instance.selectedMechanism.oldRom.aromMax);
            }
        }
    }

    // =========================================================================
    // State machine
    // =========================================================================
    void RunStateMachine()
    {
        CurrPositioncursor.SetActive(true);
        startButton.SetActive(false);

        switch (_state)
        {
            case AssessStates.INIT:
                feedbackText.text = "";
                directionArrow.text = "";
                relaxText.text = $"AROM Assessment\n{NUM_TRIALS} trials × {CYCLES_PER_TRIAL} cycles each\n\nPress PLUTO button to start Trial 1";
                if (isButtonPressed || Input.GetKeyDown(KeyCode.Return))
                {
                    isButtonPressed = false;
                    InitTrial(0);
                }
                break;

            case AssessStates.TRIAL_RUNNING:
                UpdateVelocityAndRest();
                UpdateCycleDetection();
                ShowTrialRunningUI();
                break;

            case AssessStates.TRIAL_COMPLETE:
                ShowTrialCompleteUI();
                if (isButtonPressed || Input.GetKeyDown(KeyCode.Return))
                {
                    isButtonPressed = false;
                    int nextTrial = _currentTrial + 1;
                    if (nextTrial < NUM_TRIALS)
                    {
                        InitTrial(nextTrial);
                    }
                    else
                    {
                        FinishAssessment();
                    }
                }
                break;

            case AssessStates.ASSESSMENT_COMPLETE:
                ShowAssessmentCompleteUI();
                if (isButtonPressed || Input.GetKeyDown(KeyCode.Return))
                {
                    isButtonPressed = false;
                    OnNextButtonClick();
                }
                break;
        }
    }

    // =========================================================================
    // Trial management
    // =========================================================================
    void InitTrial(int trialIndex)
    {
        _currentTrial    = trialIndex;
        _completedCycles = 0;
        _trialCycles[trialIndex].Clear();

        // Reset panel background color to default (dark slate)
        if (aromLockedImage != null)
            aromLockedImage.color = new Color(0.17f, 0.24f, 0.31f, 1f);  // Dark slate gray

        // Clear previous markers
        aromSlider.ClearCycleMarkers();
        aromSlider.UpdateMinMaxvalues = false;
        aromSlider.HideHandles();

        float startAngle = PlutoComm.angle;

        // Reset slider to start position (both handles at current angle, no fill shown initially)
        aromSlider.minAng = startAngle;
        aromSlider.maxAng = startAngle;
        aromSlider.SliderMin.setSliderVal(startAngle);
        aromSlider.SliderMax.setSliderVal(startAngle);

        // Start the assessment (hides old ROM reference area)
        aromSlider.startAssessment(startAngle);

        _velWindow.Clear();
        _lastAngle = startAngle;
        _atRest    = false;

        ResetCycleState();

        _state = AssessStates.TRIAL_RUNNING;
        AppLogger.LogInfo($"AROM Trial {trialIndex + 1} started.");
    }

    void CompleteCurrentTrial()
    {
        var best = GetBestCycle(_trialCycles[_currentTrial]);
        _trialBests[_currentTrial] = best;

        // Show only the best cycle in bright white/gold, hide all others
        aromSlider.ClearCycleMarkers();
        Color bestColor = new Color(1.0f, 0.95f, 0.2f, 1f);  // bright gold
        aromSlider.AddCycleMarker(best.lo, best.hi, bestColor, -1);  // -1 = no label for best cycle

        // Update slider fill to show the best cycle's range
        aromSlider.SliderMin.setSliderVal(best.lo);
        aromSlider.SliderMax.setSliderVal(best.hi);
        aromSlider.minAng = best.lo;
        aromSlider.maxAng = best.hi;

        float range = best.hi - best.lo;
        AppLogger.LogInfo(
            $"Trial {_currentTrial + 1} complete. Best cycle: LO={best.lo:F1}° HI={best.hi:F1}° (range {range:F1}°)"
            + $" | Slider MinVal={aromSlider.MinValue:F1}° MaxVal={aromSlider.MaxValue:F1}°"
        );

        _state = AssessStates.TRIAL_COMPLETE;
    }

    void FinishAssessment()
    {
        var final = ComputeFinalArom();
        _tmin = final.lo;
        _tmax = final.hi;

        // Find which trial had the best result
        int bestTrialIndex = 0;
        float bestRange = _trialBests[0].hi - _trialBests[0].lo;
        for (int i = 1; i < NUM_TRIALS; i++)
        {
            float range = _trialBests[i].hi - _trialBests[i].lo;
            if (range > bestRange)
            {
                bestRange = range;
                bestTrialIndex = i;
            }
        }

        // Find which cycle in the best trial was selected
        var bestCycles = _trialCycles[bestTrialIndex];
        var selectedBestCycle = GetBestCycle(bestCycles);
        int bestCycleIndex = -1;
        for (int i = 0; i < bestCycles.Count; i++)
        {
            if (bestCycles[i].lo == selectedBestCycle.lo && bestCycles[i].hi == selectedBestCycle.hi)
            {
                bestCycleIndex = i + 1; // 1-indexed
                break;
            }
        }

        // Clear all markers and show only the final best AROM
        aromSlider.ClearCycleMarkers();
        Color finalBestColor = new Color(1.0f, 0.95f, 0.2f, 1f);  // bright gold
        aromSlider.AddCycleMarker(_tmin, _tmax, finalBestColor, -1);  // -1 = no label for final best

        // Update slider to show final AROM
        aromSlider.SliderMin.setSliderVal(_tmin);
        aromSlider.SliderMax.setSliderVal(_tmax);
        aromSlider.minAng = _tmin;
        aromSlider.maxAng = _tmax;

        _state = AssessStates.ASSESSMENT_COMPLETE;
        AppLogger.LogInfo(
            $"AROM Assessment complete. Final AROM: {_tmin:F1}° to {_tmax:F1}° ({_tmax - _tmin:F1}°) "
            + $"from Trial {bestTrialIndex + 1} Cycle {bestCycleIndex}"
        );
    }

    // =========================================================================
    // Velocity, rest, and direction detection
    // =========================================================================
    void UpdateVelocityAndRest()
    {
        float angle = PlutoComm.angle;
        float dt    = Time.deltaTime;

        if (dt > 0f)
        {
            float v = (angle - _lastAngle) / dt;  // signed deg/s
            if (_velWindow.Count >= VEL_WINDOW)
                _velWindow.Dequeue();
            _velWindow.Enqueue(v);
        }
        _lastAngle = angle;

        if (_velWindow.Count < VEL_WINDOW)
        {
            _atRest       = false;
            _currDirection = 0;
            return;
        }

        float sum = 0f;
        foreach (float v in _velWindow) sum += v;
        float avg = sum / VEL_WINDOW;

        _atRest = Mathf.Abs(avg) < VEL_DIR_THRESHOLD;

        if      (avg >  VEL_DIR_THRESHOLD) _currDirection =  1;  // moving toward HI
        else if (avg < -VEL_DIR_THRESHOLD) _currDirection = -1;  // moving toward LO
        else                               _currDirection =  0;  // neutral / at rest
    }

    // =========================================================================
    // Cycle detection — dual algorithm (HOC vs. non-HOC)
    // =========================================================================
    void UpdateCycleDetection()
    {
        if (AppData.Instance.selectedMechanism.IsMechanism("HOC"))
            UpdateCycleDetection_HOC();
        else
            UpdateCycleDetection_NonHOC();
    }

    // --- HOC: State-based OPENING/CLOSING with rest-gated boundaries ---
    void UpdateCycleDetection_HOC()
    {
        float angle = PlutoComm.angle;

        // Peak tracking has NO rest gate — we update peaks every frame for smooth slider animation
        // Rest gate only applies to FINALIZATION (below)

        if (_hocState == HocState.OPENING)
        {
            // Track maximum opening (most negative angle, toward -93)
            if (!_hocOpenFinalized && angle < _hocPeakOpen)
            {
                _hocPeakOpen = angle;
                AppLogger.LogInfo($"[HOC] OPENING peak updated to {angle:F1}° ({ConvertToCM(angle):F2}cm)");
            }

            // Detect reversal: only while at rest. If angle increases >5° from peak, finalize OPEN
            if (!_hocOpenFinalized && _atRest && (angle - _hocPeakOpen) > HOC_REVERSAL_THRESHOLD_DEG)
            {
                _hocOpenFinalized = true;
                _finalizedLo = _hocPeakOpen;
                AppLogger.LogInfo($"[HOC] ✓ OPEN boundary finalized at {_hocPeakOpen:F1}° ({ConvertToCM(_hocPeakOpen):F2}cm)");

                // Switch to CLOSING state
                _hocState = HocState.CLOSING;
                _hocPeakClose = angle;
                AppLogger.LogInfo($"[HOC] State → CLOSING");
            }
        }
        else if (_hocState == HocState.CLOSING)
        {
            // Track maximum closing (most positive angle, toward 0)
            if (!_hocCloseFinalized && angle > _hocPeakClose)
            {
                _hocPeakClose = angle;
                AppLogger.LogInfo($"[HOC] CLOSING peak updated to {angle:F1}° ({ConvertToCM(angle):F2}cm)");
            }

            // Detect reversal: only while at rest. If angle decreases >5° from peak, finalize CLOSE
            if (!_hocCloseFinalized && _atRest && (_hocPeakClose - angle) > HOC_REVERSAL_THRESHOLD_DEG)
            {
                _hocCloseFinalized = true;
                _finalizedHi = _hocPeakClose;
                AppLogger.LogInfo($"[HOC] ✓ CLOSE boundary finalized at {_hocPeakClose:F1}° ({ConvertToCM(_hocPeakClose):F2}cm)");
            }
        }

        // Cycle complete when both OPEN and CLOSE are finalized
        if (_hocOpenFinalized && _hocCloseFinalized)
        {
            AppLogger.LogInfo($"[HOC] ★ CYCLE {_completedCycles + 1} COMPLETE: {_finalizedLo:F1}° → {_finalizedHi:F1}° (range {ConvertToCM(_finalizedHi - _finalizedLo):F2}cm)");
            RecordCycle(_finalizedLo, _finalizedHi);
        }
    }

    // --- Non-HOC: Direction-based bidirectional with extent guard ---
    void UpdateCycleDetection_NonHOC()
    {
        float angle = PlutoComm.angle;

        // Arm HI and track running maximum
        if (!_hiFinalized && _currDirection == 1)
        {
            if (!_wasGoingHi)
            {
                _wasGoingHi = true;
                _hiArmAngle = angle;
                AppLogger.LogInfo($"[CYCLE] HI armed at {angle:F1}°");
            }
            if (angle > _peakHi)
            {
                _peakHi = angle;
                AppLogger.LogInfo($"[CYCLE] HI peak updated to {angle:F1}° (extent from arm: {_peakHi - _hiArmAngle:F1}°)");
            }
        }

        // Arm LO and track running minimum
        if (!_loFinalized && _currDirection == -1)
        {
            if (!_wasGoingLo)
            {
                _wasGoingLo = true;
                _loArmAngle = angle;
                AppLogger.LogInfo($"[CYCLE] LO armed at {angle:F1}°");
            }
            if (angle < _peakLo)
            {
                _peakLo = angle;
                AppLogger.LogInfo($"[CYCLE] LO peak updated to {angle:F1}° (extent from arm: {_loArmAngle - _peakLo:F1}°)");
            }
        }

        // Finalize HI: direction flipped to LO, peak moved >5° toward HI, reversed >5° from peak
        if (!_hiFinalized && _wasGoingHi && _currDirection == -1)
        {
            float hiExtent = _peakHi - _hiArmAngle;
            float hiReversal = _peakHi - angle;
            AppLogger.LogInfo($"[CYCLE] HI check: angle={angle:F1}° peak={_peakHi:F1}° extent={hiExtent:F1}° reversal={hiReversal:F1}°");

            if (hiExtent > REVERSAL_THRESHOLD && hiReversal > REVERSAL_THRESHOLD)
            {
                _finalizedHi = _peakHi;
                _hiFinalized = true;
                _wasGoingLo = false;
                _peakLo = float.PositiveInfinity;
                AppLogger.LogInfo($"[CYCLE] ✓ HI FINALIZED at {_finalizedHi:F1}°");
            }
        }

        // Finalize LO: direction flipped to HI, peak moved >5° toward LO, reversed >5° from peak
        if (!_loFinalized && _wasGoingLo && _currDirection == 1)
        {
            float loExtent = _loArmAngle - _peakLo;
            float loReversal = angle - _peakLo;
            AppLogger.LogInfo($"[CYCLE] LO check: angle={angle:F1}° peak={_peakLo:F1}° extent={loExtent:F1}° reversal={loReversal:F1}°");

            if (loExtent > REVERSAL_THRESHOLD && loReversal > REVERSAL_THRESHOLD)
            {
                _finalizedLo = _peakLo;
                _loFinalized = true;
                _wasGoingHi = false;
                _peakHi = float.NegativeInfinity;
                AppLogger.LogInfo($"[CYCLE] ✓ LO FINALIZED at {_finalizedLo:F1}°");
            }
        }

        if (_hiFinalized && _loFinalized)
        {
            AppLogger.LogInfo($"[CYCLE] ★ CYCLE {_completedCycles + 1} COMPLETE: {_finalizedLo:F1}° → {_finalizedHi:F1}° (range {_finalizedHi - _finalizedLo:F1}°)");
            RecordCycle(_finalizedLo, _finalizedHi);
        }
    }

    void RecordCycle(float lo, float hi)
    {
        _trialCycles[_currentTrial].Add((lo, hi));
        _completedCycles++;

        float range = hi - lo;
        AppLogger.LogInfo(
            $"Trial {_currentTrial + 1} Cycle {_completedCycles}: "
            + $"{lo:F1}° to {hi:F1}° ({range:F1}°)"
        );

        // Each cycle gets its own distinct color (red → orange → yellow → green → blue)
        Color markerColor = CycleColors[(_completedCycles - 1) % CycleColors.Length];
        aromSlider.AddCycleMarker(lo, hi, markerColor, _completedCycles);  // Pass cycle number for label

        // Update slider display to show widest range seen so far this trial
        float trialLo = lo, trialHi = hi;
        foreach (var c in _trialCycles[_currentTrial])
        {
            if (c.lo < trialLo) trialLo = c.lo;
            if (c.hi > trialHi) trialHi = c.hi;
        }
        aromSlider.SliderMin.setSliderVal(trialLo);
        aromSlider.SliderMax.setSliderVal(trialHi);

        if (_completedCycles >= CYCLES_PER_TRIAL)
        {
            CompleteCurrentTrial();
            return;
        }

        // Reset for next cycle from current position
        ResetCycleState();
    }

    void ResetCycleState()
    {
        float angle = PlutoComm.angle;

        if (AppData.Instance.selectedMechanism.IsMechanism("HOC"))
        {
            // HOC state reset
            _hocState           = HocState.OPENING;
            _hocPeakOpen        = angle;
            _hocPeakClose       = angle;
            _hocOpenFinalized   = false;
            _hocCloseFinalized  = false;
        }
        else
        {
            // Non-HOC state reset
            _peakHi             = float.NegativeInfinity;
            _peakLo             = float.PositiveInfinity;
            _wasGoingHi         = false;
            _wasGoingLo         = false;
            _hiArmAngle         = angle;
            _loArmAngle         = angle;
            _hiFinalized        = false;
            _loFinalized        = false;
            _finalizedHi        = angle;
            _finalizedLo        = angle;
        }
    }

    // =========================================================================
    // Best cycle / final AROM selection
    // =========================================================================
    (float lo, float hi) GetBestCycle(List<(float lo, float hi)> cycles)
    {
        // Sliding window: take last 3 cycles, pick the widest range
        int start = Mathf.Max(0, cycles.Count - 3);
        (float lo, float hi) best = cycles[start];
        float bestRange = best.hi - best.lo;

        for (int i = start + 1; i < cycles.Count; i++)
        {
            float r = cycles[i].hi - cycles[i].lo;
            if (r > bestRange)
            {
                bestRange = r;
                best = cycles[i];
            }
        }
        return best;
    }

    (float lo, float hi) ComputeFinalArom()
    {
        (float lo, float hi) best = _trialBests[0];
        float bestRange = best.hi - best.lo;

        for (int i = 1; i < NUM_TRIALS; i++)
        {
            float r = _trialBests[i].hi - _trialBests[i].lo;
            if (r > bestRange)
            {
                bestRange = r;
                best = _trialBests[i];
            }
        }
        return best;
    }

    // =========================================================================
    // UI helpers
    // =========================================================================
    void ShowTrialRunningUI()
    {
        string unit = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? "cm" : "°";
        bool isHOC  = AppData.Instance.selectedMechanism.IsMechanism("HOC");

        if (isHOC)
        {
            // HOC display: OPENING/CLOSING state
            float displayMin = _hocOpenFinalized  ? _finalizedLo : _hocPeakOpen;   // open = most negative (left)
            float displayMax = _hocCloseFinalized ? _finalizedHi : _hocPeakClose;  // close = most positive (right)

            aromSlider.SliderMin.setSliderVal(displayMin);
            aromSlider.SliderMax.setSliderVal(displayMax);
            aromSlider.minAng = displayMin;
            aromSlider.maxAng = displayMax;

            string openStr  = _hocOpenFinalized  ? $"<color=#00FF00>{ConvertToCM(_finalizedLo):F2}cm✓</color>" : $"{ConvertToCM(_hocPeakOpen):F2}cm";
            string closeStr = _hocCloseFinalized ? $"<color=#00FF00>{ConvertToCM(_finalizedHi):F2}cm✓</color>" : $"{ConvertToCM(_hocPeakClose):F2}cm";
            string stateStr = _hocState == HocState.OPENING ? "←OPEN" : "CLOSE→";
            string restIndicator = _atRest ? "<color=#00FF00>●</color>" : "<color=#FF4444>●</color>";

            relaxText.text =
                $"Trial {_currentTrial + 1}/{NUM_TRIALS}  |  Cycle {_completedCycles + 1}/{CYCLES_PER_TRIAL}\n"
                + $"Open: {openStr}   Close: {closeStr}\n"
                + $"{stateStr} {restIndicator}";
        }
        else
        {
            // Non-HOC display: HI/LO direction-based
            float displayMin = _wasGoingLo && _peakLo != float.PositiveInfinity ? _peakLo : PlutoComm.angle;
            float displayMax = _wasGoingHi && _peakHi != float.NegativeInfinity ? _peakHi : PlutoComm.angle;
            aromSlider.SliderMin.setSliderVal(displayMin);
            aromSlider.SliderMax.setSliderVal(displayMax);
            aromSlider.minAng = displayMin;
            aromSlider.maxAng = displayMax;

            string hiStr, loStr;
            if (_hiFinalized)
                hiStr = $"<color=#00FF00>{_finalizedHi:F1}{unit}✓</color>";
            else if (_wasGoingHi)
                hiStr = $"{_peakHi:F1}{unit}";
            else
                hiStr = "–";

            if (_loFinalized)
                loStr = $"<color=#00FF00>{_finalizedLo:F1}{unit}✓</color>";
            else if (_wasGoingLo)
                loStr = $"{_peakLo:F1}{unit}";
            else
                loStr = "–";

            string dirStr = _currDirection == 1 ? "→" : (_currDirection == -1 ? "←" : "·");
            string restIndicator = _atRest ? "<color=#00FF00>●</color>" : "<color=#FF4444>●</color>";

            relaxText.text =
                $"Trial {_currentTrial + 1}/{NUM_TRIALS}  |  Cycle {_completedCycles + 1}/{CYCLES_PER_TRIAL}\n"
                + $"HI: {hiStr}   LO: {loStr}\n"
                + $"{dirStr} {restIndicator}";
        }

        feedbackText.text   = "";
        directionArrow.text = "";
    }

    void ShowTrialCompleteUI()
    {
        var best = _trialBests[_currentTrial];
        string unit = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? "cm" : "°";
        float lo = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(best.lo) : best.lo;
        float hi = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(best.hi) : best.hi;
        float range = Mathf.Abs(hi - lo);

        // Ensure slider fill is displaying the best cycle range
        aromSlider.SliderMin.setSliderVal(best.lo);
        aromSlider.SliderMax.setSliderVal(best.hi);
        aromSlider.minAng = best.lo;
        aromSlider.maxAng = best.hi;

        bool isLastTrial = (_currentTrial >= NUM_TRIALS - 1);
        string nextPrompt = isLastTrial
            ? "Press button to finish assessment"
            : $"Press button to start Trial {_currentTrial + 2}";

        relaxText.text =
            $"Trial {_currentTrial + 1} Complete!\n"
            + $"Best range: {lo:F1}{unit} to {hi:F1}{unit} ({range:F1}{unit})\n\n"
            + nextPrompt;

        feedbackText.text  = "";
        directionArrow.text = "";

        if (aromLockedImage != null)
            aromLockedImage.color = new Color(0.2f, 0.8f, 0.3f);
    }

    void ShowAssessmentCompleteUI()
    {
        string unit = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? "cm" : "°";
        float lo = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(_tmin) : _tmin;
        float hi = AppData.Instance.selectedMechanism.IsMechanism("HOC") ? ConvertToCM(_tmax) : _tmax;
        float range = Mathf.Abs(hi - lo);

        // Find which trial and cycle was the best
        int bestTrialIndex = 0;
        float bestRange = _trialBests[0].hi - _trialBests[0].lo;
        for (int i = 1; i < NUM_TRIALS; i++)
        {
            float r = _trialBests[i].hi - _trialBests[i].lo;
            if (r > bestRange)
            {
                bestRange = r;
                bestTrialIndex = i;
            }
        }

        var bestCycles = _trialCycles[bestTrialIndex];
        var selectedBestCycle = GetBestCycle(bestCycles);
        int bestCycleIndex = -1;
        for (int i = 0; i < bestCycles.Count; i++)
        {
            if (bestCycles[i].lo == selectedBestCycle.lo && bestCycles[i].hi == selectedBestCycle.hi)
            {
                bestCycleIndex = i + 1; // 1-indexed
                break;
            }
        }

        relaxText.text =
            $"Assessment Complete!\n"
            + $"AROM: {lo:F1}{unit} to {hi:F1}{unit} ({range:F1}{unit})\n"
            + $"Best: Trial {bestTrialIndex + 1} / Cycle {bestCycleIndex}\n\n"
            + "Press button to continue to PROM";

        feedbackText.text   = "";
        directionArrow.text = "";
        nextButton.SetActive(false);

        if (aromLockedImage != null)
            aromLockedImage.color = new Color(0f / 255f, 55f / 255f, 52f / 255f);
    }

    // =========================================================================
    // Save / navigation
    // =========================================================================
    public void OnSaveClick()
    {
        _tmin = Mathf.Max(_tmin, -angLimit);
        _tmax = Mathf.Min(_tmax,  angLimit);

        float aromRange = Mathf.Abs(_tmax - _tmin);
        bool isCPM = aromRange <= MIN_AROM_RANGE;

        AppData.Instance.selectedMechanism.SetNewAromValues(_tmin, _tmax);
        AppData.Instance.selectedMechanism.SetAromCPM(isCPM);

        AppLogger.LogInfo($"AROM saved — Range: {aromRange:F1}° — CPM: {isCPM}");

        nextButton.SetActive(false);
        aromSlider.UpdateMinMaxvalues = false;
        CurrPositioncursor.SetActive(false);

        if (aromLockedImage != null)
            aromLockedImage.color = new Color(0f / 255f, 55f / 255f, 52f / 255f);
    }

    public void OnNextButtonClick()
    {
        OnSaveClick();
        panelControl.SelectpROM();
        startButton.SetActive(false);
        nextButton.SetActive(false);
    }

    public void OnRedoAromClick()
    {
        _state = AssessStates.INIT;
        isButtonPressed = false;

        InitializeAssessment();
        UpdateStatusText();
        panelControl.SelectAROM();
        AppData.Instance.selectedMechanism.ResetPromValues();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnrestartButtonClick()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // =========================================================================
    // PLUTO button
    // =========================================================================
    public void OnPlutoButtonReleased()
    {
        isButtonPressed = true;
    }

    // =========================================================================
    // Utility
    // =========================================================================
    private float ConvertToCM(float value) => Mathf.Abs(Mathf.Deg2Rad * value * 6f);

    private string FormatRelaxText(float min, float max)
    {
        return AppData.Instance.selectedMechanism.IsMechanism("HOC")
            ? $"Prev AROM: {ConvertToCM(min):0.0}cm : {ConvertToCM(max):0.0}cm (Aperture: {ConvertToCM(max - min):0.0}cm)"
            : $"Prev AROM: {(int)min} : {(int)max} ({(int)(max - min)}°)";
    }

    private void UpdateStatusText()
    {
        jointAngle.text = PlutoComm.angle.ToString("0.0");
    }

    // Legacy — kept so existing Unity button wiring still works
    public void OnStartButtonClick()
    {
        startButton.SetActive(false);
    }
}
