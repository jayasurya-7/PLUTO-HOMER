# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PLUTO-HOMER is a Unity-based hand/wrist rehabilitation therapy application. It connects to a physical robotic device (PLUTO) via serial COM port, guides patients through gamified exercises, and implements an "Assist-As-Needed" (AAN) algorithm that provides motor assistance only when needed. Session data is uploaded to AWS for remote monitoring.

## Build & Development

This is a **Unity project** — there are no command-line build scripts. Development is done through the Unity Editor:

- Open with Unity Hub or Unity Editor (check `ProjectSettings/ProjectVersion.txt` for required version)
- Build via **File > Build Settings** in the Unity Editor
- The physical PLUTO device connects on a COM port configured in `C:/lapconfig.json`
- Data uploads require a Python script at the path specified in `C:/lapconfig.json`

## Architecture

### Scene Flow

The app is a linear scene state machine. Each scene has exactly one MonoBehaviour handler. Main therapy flow:

```
LOGIN -> MAIN -> CHMECH -> CALIB -> [ASSESS -> ASSISTPROFILE] -> SETDUR -> CHGAME -> [GAME] -> CHMECH -> SUMM -> DATAUPLOAD
```

Admin/therapist flow: `Ctrl+Shift+X` on CHMECH loads **PLANSETUP** for configuring therapy plans.

### Core Static Classes (no `new`, no `GetComponent`)

- **`PlutoComm`** ([Assets/scripts/PlutoComm.cs](Assets/scripts/PlutoComm.cs)): All PLUTO device communication. Fires C# events (`OnButtonReleased`, `OnNewPlutoData`, `OnControlModeChange`, `OnMechanismChange`) that scene handlers subscribe to. All state is in static properties (`angle`, `torque`, `control`, `calibration`, `button`).
- **`JediComm`** ([Assets/scripts/JediComm.cs](Assets/scripts/JediComm.cs)): Low-level serial port using JEDI protocol. Runs a dedicated `AboveNormal`-priority reader thread. Calls `PlutoComm.parseByteArray()` which fires events — **these events fire from the reader thread, not the Unity main thread**.
- **`DataManager`** ([Assets/scripts/DataManager.cs](Assets/scripts/DataManager.cs)): All file I/O. CSV read/write, directory structure under `Assets/data/{userID}/data/`. Reads device config from `C:/lapconfig.json`.
- **`awsManager`** ([Assets/scripts/awsManager.cs](Assets/scripts/awsManager.cs)): Spawns `pythonw.exe` to upload data to AWS.

### AppData Singleton

[Assets/scripts/AppData.cs](Assets/scripts/AppData.cs) is a lazy singleton (`AppData.Instance`) holding all cross-scene runtime state: `userID`, `selectedMechanism`, `selectedGame`, `currentSessionNumber`, `aanController`, `trainingSide`, `userData`. `Initialize()` is called once from the MAIN scene — it connects the robot and starts logging.

### AAN Algorithm

`PlutoAANController` ([Assets/scripts/plutoaan.cs](Assets/scripts/plutoaan.cs)) is the production therapy algorithm. It runs a state machine across discrete movement trials, adapting `currentCtrlBound` using a forgetting factor (0.9) based on actual vs. desired success rate. `Update(angle, deltaT, trialDone)` is called every game frame. A `Stopwatch` triggers assistance after 1.5 seconds of patient stall.

### Scene Handler Pattern

Every scene handler follows this exact pattern — preserve it when adding new scenes:

```csharp
void Start() {
    PlutoComm.sendHeartbeat();
    AppLogger.SetCurrentScene(...);
    PlutoComm.OnButtonReleased += OnPlutoButtonReleased;
    // UI init
}

void Update() {
    PlutoComm.sendHeartbeat(); // every frame
    if (changeScene) { SceneManager.LoadScene(...); changeScene = false; }
}

void OnDestroy() {
    if (ConnectToRobot.isPLUTO) PlutoComm.OnButtonReleased -= OnPlutoButtonReleased;
}
```

The `changeScene` boolean defers scene transitions to the Unity main thread (since `PlutoComm` events fire from the serial reader thread). Use `ConcurrentQueue<Action>` when you need to queue multiple actions from background threads (see `summarySceneHandler.cs`).

### Data Storage

All patient data lives as CSV files under `Assets/data/{userID}/data/`:
- `configdata.csv` — therapy plan (mechanism durations, ROM limits, FME types, dates)
- `sessions/sessions.csv` — one row per trial (26 fields: session#, trial#, game, mechanism, score, control bound, success rate, etc.)
- `rawdata/raw-sess{N}-trial{N}-{game}-{mechanism}.csv` — raw sensor stream ~100 Hz
- `rom/` — range-of-motion per mechanism
- `applog/` — three log streams: AppLogger, PlutoComLogger, PlutoAanLogger

### Assessment Scripts

Located in [Assets/Assessment/scripts/](Assets/Assessment/scripts/). These handle the ROM measurement workflow before therapy begins:

#### AROM Assessment (Active Range of Motion)
**File:** [Assets/Assessment/scripts/AROMsceneHandler.cs](Assets/Assessment/scripts/AROMsceneHandler.cs)

Implements a **structured 3-trial × 5-cycle assessment** with dual cycling algorithms optimized for each mechanism type:

**Trial/Cycle Structure:**
- 3 independent trials, each requiring 5 complete cycles
- A cycle = one finalized LOW boundary + one finalized HIGH boundary (order-independent pair)
- Best range selected from last 3 cycles per trial (sliding window to exclude early learning)
- Final AROM = widest best-cycle range across all 3 trials

**Dual Cycling Algorithm:**

1. **Non-HOC mechanisms** (Flexion/Extension, Radial/Ulnar Dev, Pronation/Supination):
   - **Direction-based detection:** Tracks signed velocity (rolling 10-sample window at 100 Hz)
   - **HI boundary:** Arms when patient moves toward HI, finalizes on reversal >5° after advancing >5° from arm point
   - **LO boundary:** Arms when patient moves toward LO, finalizes on reversal >5° after advancing >5° from arm point
   - **Rest-independent:** Reversals fire based on position change alone (no velocity gate)
   - **Extent guard:** Prevents tiny initial drifts from counting as full boundaries (must move >5° first)

2. **HOC mechanism** (Hand Opening/Closing):
   - **State-based detection:** Two-state machine (OPENING → CLOSING → repeat)
   - **OPENING state:** Patient opens hand from rest; tracks maximum angle reached (_hocPeakOpen)
   - **CLOSING state:** Patient closes hand back; tracks minimum angle reached (_hocPeakClose)
   - **Rest-gated boundaries:** Boundaries only update while patient is nearly still (<2 deg/s average)
   - **Reversal threshold:** 0.5 cm (≈0.0873° at 6cm radius) — lower than non-HOC due to smaller ROM
   - **Cycle completion:** Both OPENING and CLOSING extremes must be finalized

**Real-Time UI:**
- Slider shows current explored range (fill spans min/max reached so far, updates every frame)
- Direction arrow shows current movement direction (→ / ← / ·)
- Rest indicator (● green when patient is still, red when moving)
- Cycle marker lines drawn in distinct colors per cycle (red → orange → yellow → green → blue)
- For HOC: "Open: X cm   Close: Y cm" with state indicator (OPEN→ / ←CLOSE)
- For non-HOC: "HI: X°   LO: Y°" with direction arrow

**Trial Completion:**
- When 5th cycle completes, best cycle selected (widest range from cycles 3–5)
- All cycle markers cleared; only best cycle drawn in gold
- Slider fill updated to show best cycle's range
- User confirms or advances to next trial via PLUTO button

**Assessment Completion (After 3 Trials):**
- Final best AROM = widest best-cycle range across all 3 trials
- FinishAssessment() identifies which trial had the best result and which cycle within that trial
- All previous trial markers cleared
- Gold marker lines drawn at the final best AROM range (matching the cyan fill position)
- Display shows: "Best: Trial X / Cycle Y" to indicate which trial and cycle was selected
- Slider fill and marker lines now perfectly aligned at the final AROM range
- Ready to proceed to PROM assessment

**Logging:**
- `[CYCLE]` prefix for non-HOC detection events
- `[HOC]` prefix for HOC-specific state/boundary events
- Each boundary finalization logged with angle/extent/reversal values
- Each cycle completion logged with range and trial/cycle index
- Trial completion logged with best cycle info
- Assessment completion logged with final AROM and which trial/cycle was selected

**Related Classes:**
- [Assets/Assessment/DoubleSlider/Scripts/DoubleSlider.cs](Assets/Assessment/DoubleSlider/Scripts/DoubleSlider.cs) — Unified slider component (single handle pair for all mechanisms, no HOC-specific variants)
- [Assets/Assessment/pannel select.cs](Assets/Assessment/pannel select.cs) — Assessment tab selector (AROM/PROM switching)

#### PROM Assessment (Passive Range of Motion)
**File:** [Assets/Assessment/scripts/PROMsceneHandler.cs](Assets/Assessment/scripts/PROMsceneHandler.cs)
- passive ROM: validates PROM ≥ AROM

#### APROM Assessment (Assisted Passive ROM)
**File:** [Assets/Assessment/scripts/AssistSceneHandler.cs](Assets/Assessment/scripts/AssistSceneHandler.cs)
- APROM assessment using progressive torque via coroutine

### Games

Each game lives in its own folder under `Assets/Games/` (HAT, Ping Pong, RNR, FruitBasket, Hatrick, FlappyBird). Games interact with the AAN controller via `AppData.Instance.aanController` and read device state from `PlutoComm` static properties.

## Key Constraints

- `PlutoComm` events fire from the serial reader thread — never call Unity APIs directly in event handlers; use `changeScene` flag or `ConcurrentQueue<Action>` to marshal to main thread.
- `PlutoComm.sendHeartbeat()` must be called every `Update()` frame or the device will time out.
- Always unsubscribe from `PlutoComm` events in `OnDestroy()` guarded by `ConnectToRobot.isPLUTO` to avoid null ref when running without hardware.
- Device config (COM port, Python path) comes from `C:/lapconfig.json` via `DataManager.getLapConfig()` — do not hardcode paths.
