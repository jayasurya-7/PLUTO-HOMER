// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
// using System;
// using System.Diagnostics;
// using UnityEngine.SceneManagement;

// #if UNITY_EDITOR
// using UnityEditor;
// #endif

// public class connectStatusHandler : MonoBehaviour
// {
//     private Image connectStatus;
//     private GameObject loading;
//     private TextMeshProUGUI statusText;
//      public TextMeshProUGUI errorTxt;
//     public Button closePanel;
//     public GameObject errorPanel;

//     BatteryStatus status ;
//     float level;
    
//     private float disconnectTimer = 0f;
//     private const float shutdownDelay = 5f;
//     void Awake()
//     {
//         // Subscribe to shutdown events once per instance
//         Application.quitting += CloseAppLogger; //for Exe file
//         AppDomain.CurrentDomain.ProcessExit += (_, __) => CloseAppLogger(); // for external crash like OS Crash

//         #if UNITY_EDITOR
//                 EditorApplication.quitting += CloseAppLogger; //for editor
//         #endif
//     }
//     // Start is called before the first frame update
//     void Start()
//     {
//         connectStatus = GetComponent<Image>(); // Uncomment if connectStatus is on the same GameObject
//         loading = transform.Find("loading").gameObject; // Assuming loading is a child GameObject
//         statusText = transform.Find("statusText").GetComponent<TextMeshProUGUI>();
//         closePanel.onClick.AddListener(delegate { CloseAppLogger(); });
//         if (AppData.Instance != null)return;
//             if (SceneManager.GetActiveScene().name == "plutoDiagnostics") return;
//             errorPanel.SetActive(true);
        
//         AppLogger.LogInfo($"Starting Device with a Battery level of  | level : {SystemInfo.batteryLevel*100}%");
//     }

//     // Update is called once per frame
//     void Update()
//     {
//          level = SystemInfo.batteryLevel;      // 0.0 � 1.0   OR -1 if unsupported
//         status = SystemInfo.batteryStatus;

//         //if level below 30% it show the indication to connect charger
//         if (level < 0.3 && !errorPanel.gameObject.activeSelf && status != BatteryStatus.Charging)// 30% Battery Level Threshold
//         {
//             errorPanel.SetActive(true);
//             AppLogger.LogInfo($"Error Below BatteryLevel   | level : {SystemInfo.batteryLevel * 100}%");
//             errorTxt.text = $"Battery Low{level * 100}%Please Connect the Charger";
//         }
//         //if Battery connected after the indication shown, Indication disappear Dynamically
//         if(status == BatteryStatus.Charging && level <= 0.3 && errorPanel.gameObject.activeSelf)
//         {
//             AppLogger.LogInfo($"closed dynamically when device connect with charger | status : {status}");
//             errorPanel.SetActive(!errorPanel.gameObject.activeSelf);
//         }
//         // Update connection status
//         if (ConnectToRobot.isPLUTO)
//         {
//             connectStatus.color = Color.green;
//             loading.SetActive(false);
//             statusText.text = $"{PlutoComm.version}\n[{PlutoComm.frameRate:F1}Hz]";

//              disconnectTimer = 0f; //reset when connected
//         }
//         else
//         {
//             connectStatus.color = Color.red;
//             loading.SetActive(true);
//             statusText.text = "Not connected";

//             disconnectTimer += Time.deltaTime;

//             if (disconnectTimer >= shutdownDelay)
//             {
//                 string currentScene = SceneManager.GetActiveScene().name;
//                 if (currentScene == "MAIN") // replace with your scene name
//                 {
//                     // Direct shutdown
//                     CloseAppLogger();
//                 //    Process.Start("shutdown", "/s /t 0");
//                 }
//                 else
//                 {
//                     if (AppData.isNRSVersion)
//                     {
//                         Application.Quit();

//                     #if UNITY_EDITOR
//                                 UnityEditor.EditorApplication.isPlaying = false;
//                     #endif
//                     }else{
//                             // Normal flow: load DataUpload
//                             SceneManager.LoadScene("DATAUPLOAD");
//                          }
//                 }
//                 // CloseAppLogger();
//                 // SceneManager.LoadScene("DATAUPLOAD");
//             }
//         }
//     }

//     private void CloseAppLogger()
//     {
//          if (FlappyGameControl.Instance != null)
//         {
//             if (FlappyGameControl.Instance.IsGamePlaying())
//             {
//                 FlappyGameControl.Instance.exitGame();

//             }
//         }
//         if(PongGameController.Instance != null)
//         {
//             if (PongGameController.Instance.IsGamePlaying())
//             {
//                 PongGameController.Instance.ExitGame();
               
//             }
//         }
//         if(FruitBasketGameController.Instance != null)
//         {
//             if (FruitBasketGameController.Instance.isGamePlaying())
//             {

//                 FruitBasketGameController.Instance.exitGame();
//             }
//         }
//         if(GameManager.Instance != null)
//         {
//             if (GameManager.Instance.IsGamePlaying())
//             {

//                 GameManager.Instance.Exit();
//             }
//         }
//         if(HatGameController.Instance != null)
//         {
//             if (HatGameController.Instance.IsGamePlaying())
//             {

//                 HatGameController.Instance.exitGame();
//             }
//         }
//         AppLogger.StopLogging();
//         PlutoAanLogger.StopLogging();
//         PlutoComLogger.StopLogging();
//          if (AppData.isNRSVersion)
//                     {
//                         Application.Quit();

//                     #if UNITY_EDITOR
//                                 UnityEditor.EditorApplication.isPlaying = false;
//                     #endif
//                     }
//                     else{
//                         //  Application.Quit();

//                     #if UNITY_EDITOR
//                                 UnityEditor.EditorApplication.isPlaying = false;
//                     #endif
//                     Process.Start("shutdown", "/s /t 0");
//                     }
        
//     }
// }





















using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Diagnostics;
using UnityEngine.SceneManagement;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class connectStatusHandler : MonoBehaviour
{
    private Image connectStatus;
    private GameObject loading;
    private TextMeshProUGUI statusText;
    public TextMeshProUGUI errorTxt;
    public Button closePanel;
    public GameObject errorPanel;

    BatteryStatus status;
    float level;

    private float disconnectTimer = 0f;
    private const float shutdownDelay = 5f;
    private const float shutdownDelayMainScene = 15f;
    private bool isShuttingDown = false;
    private bool isTransitioning = false;

    void Awake()
    {
        // Subscribe to shutdown events once per instance
        Application.quitting += CloseAppLogger; //for Exe file
        AppDomain.CurrentDomain.ProcessExit += (_, __) => CloseAppLogger(); // for external crash like OS Crash

        #if UNITY_EDITOR
                EditorApplication.quitting += CloseAppLogger; //for editor
        #endif
    }

    void Start()
    {
        connectStatus = GetComponent<Image>();
        loading = transform.Find("loading").gameObject;
        statusText = transform.Find("statusText").GetComponent<TextMeshProUGUI>();
        closePanel.onClick.AddListener(delegate { StartShutdown(); });
        
        if (AppData.Instance != null) return;
        if (SceneManager.GetActiveScene().name == "plutoDiagnostics") return;
        errorPanel.SetActive(true);
        
        AppLogger.LogInfo($"Starting Device with a Battery level of | level : {SystemInfo.batteryLevel * 100}%");
    }

    void Update()
    {
        // Don't process while shutting down or transitioning
        if (isShuttingDown || isTransitioning) return;

        level = SystemInfo.batteryLevel;
        status = SystemInfo.batteryStatus;

        if (level < 0.3 && !errorPanel.gameObject.activeSelf && status != BatteryStatus.Charging)
        {
            errorPanel.SetActive(true);
            AppLogger.LogInfo($"Error Below BatteryLevel | level : {SystemInfo.batteryLevel * 100}%");
            errorTxt.text = $"Battery Low{level * 100}%Please Connect the Charger";
        }

        if (status == BatteryStatus.Charging && level <= 0.3 && errorPanel.gameObject.activeSelf)
        {
            AppLogger.LogInfo($"closed dynamically when device connect with charger | status : {status}");
            errorPanel.SetActive(!errorPanel.gameObject.activeSelf);
        }

        if (ConnectToRobot.isPLUTO)
        {
            connectStatus.color = Color.green;
            loading.SetActive(false);

            // Display firmware if available, otherwise just show frame rate
            string statusDisplay = string.IsNullOrEmpty(PlutoComm.version)
                ? $"[{PlutoComm.frameRate:F1}Hz]"
                : $"{PlutoComm.version}\n[{PlutoComm.frameRate:F1}Hz]";

            statusText.text = statusDisplay;
            disconnectTimer = 0f;
        }
        else
        {
            connectStatus.color = Color.red;
            loading.SetActive(true);
            statusText.text = "Not connected";
            disconnectTimer += Time.deltaTime;

            string currentScene = SceneManager.GetActiveScene().name;
            float currentShutdownDelay = (currentScene == "MAIN") ? shutdownDelayMainScene : shutdownDelay;

            if (disconnectTimer >= currentShutdownDelay)
            {
                AppLogger.LogInfo($"[DISCONNECT] Current scene: '{currentScene}'");
                AppLogger.LogInfo($"[DISCONNECT] Shutdown delay expired: {currentShutdownDelay}s");
                AppLogger.LogInfo($"[DISCONNECT] AppData.isNRSVersion: {AppData.isNRSVersion}");

                if (currentScene == "MAIN")
                {
                    AppLogger.LogInfo("[DISCONNECT] In MAIN scene, starting shutdown");
                    StartShutdown();
                }
                else
                {
                    if (AppData.isNRSVersion)
                    {
                        AppLogger.LogInfo("[DISCONNECT] NRS version, starting shutdown");
                        StartShutdown();
                    }
                    else
                    {
                        // Transition to DATAUPLOAD scene
                        AppLogger.LogInfo("[DISCONNECT] Starting scene transition to DATAUPLOAD");
                        StartSceneTransition("DATAUPLOAD");
                    }
                }
            }
        }
    }

    private void StartSceneTransition(string sceneName)
    {
        if (isTransitioning || isShuttingDown) return;
        isTransitioning = true;
        StartCoroutine(TransitionSequence(sceneName));
    }

    private IEnumerator TransitionSequence(string sceneName)
    {
        // Ensure Time is running
        Time.timeScale = 1f;
        AppLogger.LogInfo($"[EMERGENCY] TransitionSequence START - target scene: {sceneName}");

        // 1. Stop all game instances first
        AppLogger.LogInfo("[EMERGENCY] Calling StopAllGameInstances...");
        yield return StopAllGameInstances();
        AppLogger.LogInfo("[EMERGENCY] StopAllGameInstances completed");

        // 2. Log final transition message BEFORE stopping loggers
        AppLogger.LogInfo($"[TRANSITION] Loading scene: {sceneName}");
        yield return new WaitForSeconds(0.2f);

        // 4. Load the scene (logging stopped now)
        isTransitioning = false;

        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[TRANSITION] Failed to load scene {sceneName}: {ex.Message}");
        }
    }

    private void StartShutdown()
    {
        if (isShuttingDown) return;
        isShuttingDown = true;
        StartCoroutine(ShutdownSequence());
    }

    private IEnumerator ShutdownSequence()
    {
        // 1. Stop all game instances
        yield return StopAllGameInstances();

        // 2. Stop all loggers with delays to flush buffers
        AppLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);
        
        PlutoAanLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);
        
        PlutoComLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);

        // 3. Log final message before shutdown
        AppLogger.LogInfo("All loggers stopped, proceeding with shutdown...");
        yield return new WaitForSeconds(0.3f);

        // 4. Now perform actual shutdown
        PerformSystemShutdown();
    }

    private IEnumerator StopAllGameInstances()
    {
        // Ensure Time is running for coroutine waits to work
        Time.timeScale = 1f;
        AppLogger.LogInfo("[EMERGENCY] Time.timeScale set to 1");

        // Only stop the game that's currently running
        string currentGame = AppData.Instance?.selectedGameName;
        AppLogger.LogInfo($"[EMERGENCY] Current game: '{currentGame}'");

        if (string.IsNullOrEmpty(currentGame))
        {
            AppLogger.LogInfo("[EMERGENCY] No game running, returning");
            yield break; // No game running
        }

        switch(currentGame.ToLower())
        {
            case "tuk":
                AppLogger.LogInfo("[EMERGENCY] Stopping FlappyBird game");
                if (FlappyGameControl.Instance != null && FlappyGameControl.Instance.IsGamePlaying())
                {
                    AppLogger.LogInfo("[EMERGENCY] Calling FlappyGameControl.exitGame(true)");
                    FlappyGameControl.Instance.exitGame(isEmergencyExit: true);
                    yield return new WaitForSecondsRealtime(0.1f);
                    AppLogger.LogInfo("[EMERGENCY] FlappyGameControl exited");
                }
                else
                {
                    AppLogger.LogInfo($"[EMERGENCY] FlappyGameControl - Instance: {FlappyGameControl.Instance}, Playing: {FlappyGameControl.Instance?.IsGamePlaying()}");
                }
                break;

            case "pong":
            case "ponggame":
                AppLogger.LogInfo("[EMERGENCY] Stopping Pong game");
                if (PongGameController.Instance != null && PongGameController.Instance.IsGamePlaying())
                {
                    AppLogger.LogInfo("[EMERGENCY] Calling PongGameController.ExitGame(true)");
                    PongGameController.Instance.ExitGame(isEmergencyExit: true);
                    yield return new WaitForSecondsRealtime(0.1f);
                    AppLogger.LogInfo("[EMERGENCY] PongGameController exited");
                }
                else
                {
                    AppLogger.LogInfo($"[EMERGENCY] PongGameController - Instance: {PongGameController.Instance}, Playing: {PongGameController.Instance?.IsGamePlaying()}");
                }
                break;

            case "fruitbasket":
                AppLogger.LogInfo("[EMERGENCY] Stopping FruitBasket game");
                if (FruitBasketGameController.Instance != null && FruitBasketGameController.Instance.isGamePlaying())
                {
                    AppLogger.LogInfo("[EMERGENCY] Calling FruitBasketGameController.exitGame(true)");
                    FruitBasketGameController.Instance.exitGame(isEmergencyExit: true);
                    yield return new WaitForSecondsRealtime(0.1f);
                    AppLogger.LogInfo("[EMERGENCY] FruitBasketGameController exited");
                }
                else
                {
                    AppLogger.LogInfo($"[EMERGENCY] FruitBasketGameController - Instance: {FruitBasketGameController.Instance}, Playing: {FruitBasketGameController.Instance?.isGamePlaying()}");
                }
                break;

            case "rnr":
                AppLogger.LogInfo("[EMERGENCY] Stopping RNR game");
                if (GameManager.Instance != null && GameManager.Instance.IsGamePlaying())
                {
                    AppLogger.LogInfo("[EMERGENCY] Calling GameManager.Exit(true)");
                    GameManager.Instance.Exit(isEmergencyExit: true);
                    yield return new WaitForSecondsRealtime(0.1f);
                    AppLogger.LogInfo("[EMERGENCY] GameManager exited");
                }
                else
                {
                    AppLogger.LogInfo($"[EMERGENCY] GameManager - Instance: {GameManager.Instance}, Playing: {GameManager.Instance?.IsGamePlaying()}");
                }
                break;

            case "hatrick":
            case "hat":
                AppLogger.LogInfo("[EMERGENCY] Stopping Hat game");
                if (HatGameController.Instance != null && HatGameController.Instance.IsGamePlaying())
                {
                    AppLogger.LogInfo("[EMERGENCY] Calling HatGameController.exitGame(true)");
                    HatGameController.Instance.exitGame(isEmergencyExit: true);
                    yield return new WaitForSecondsRealtime(0.1f);
                    AppLogger.LogInfo("[EMERGENCY] HatGameController exited");
                }
                else
                {
                    AppLogger.LogInfo($"[EMERGENCY] HatGameController - Instance: {HatGameController.Instance}, Playing: {HatGameController.Instance?.IsGamePlaying()}");
                }
                break;

            default:
                AppLogger.LogInfo($"[EMERGENCY] Game name '{currentGame}' not recognized in switch statement");
                break;
        }

        // Pause time to prevent game loop from continuing
        Time.timeScale = 0f;
        AppLogger.LogInfo("[EMERGENCY] Time paused (timeScale = 0)");
        AppLogger.LogInfo("[EMERGENCY] StopAllGameInstances completed");
    }

    private void PerformSystemShutdown()
    {
        if (AppData.isNRSVersion)
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // Use shutdown with a small delay to ensure all processes complete
            Process.Start("shutdown", "/s /t 2");
#endif
        }
    }

    private void CloseAppLogger()
    {
        // This is called from Application.quitting and other external events
        // Just start the shutdown sequence if not already shutting down
        if (!isShuttingDown && !isTransitioning)
        {
            StartShutdown();
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        Application.quitting -= CloseAppLogger;
        AppDomain.CurrentDomain.ProcessExit -= (_, __) => CloseAppLogger();
#if UNITY_EDITOR
        EditorApplication.quitting -= CloseAppLogger;
#endif
    }
}