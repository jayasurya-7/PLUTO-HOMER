using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class welcomSceneHandler : MonoBehaviour
{
    //public GameObject loading;
    public TextMeshProUGUI userName;
    public TextMeshProUGUI timeRemainingToday;
    public TextMeshProUGUI appVersion;
    public TextMeshProUGUI todaysDay;
    public TextMeshProUGUI todaysDate;
    public int daysPassed;
    public TextMeshProUGUI[] prevDays = new TextMeshProUGUI[7];
    public TextMeshProUGUI[] prevDates = new TextMeshProUGUI[7];
    public Image[] pies = new Image[7];
    public bool piChartUpdated = false;
    private DaySummary[] daySummaries;
    public readonly string nextScene = "CHMECH";

    // Private variables
    private bool buttonEventAttached = false;
    bool changeScene = false;

    // Reconnection notification variables (assign in inspector)
    public GameObject reconnectPanel;
    public TextMeshProUGUI reconnectTimerText;
    public TextMeshProUGUI reconnectStatusText;
    public Image arrowImage;
    public Image loadingCircleImage;
    private float reconnectTimer = 0f;
    private float reconnectAttemptTimer = 0f;
    private const float RECONNECT_TIMEOUT = 15f;
    private const float RECONNECT_ATTEMPT_INTERVAL = 4f;
    private bool isInitialized = false;
    private int lastDisplayedTime = -1;
    private float animationTimer = 0f;
    private const float BREATHING_SPEED = 1.5f;
    private const float ROTATION_SPEED = 180f;
    private bool sceneDestroyed = false;

    void Start()
    {

        if (!AppData.isNRSVersion)
        {
            if (!Directory.Exists(DataManager.basePath))
        {
            SceneManager.LoadScene("CONFIG");
            return;
        }

        // Get all subdirectories excluding metadata
        var validUserDirs = Directory.GetDirectories(DataManager.basePath)
        .Select(Path.GetFileName)
        .Where(name => !name.ToLower().Contains("meta"))
        .ToList();


        if (validUserDirs.Count == 1)
        {
            AppData.Instance.setUser(validUserDirs[0]);
            DataManager.setUserId(AppData.Instance.userID);
        }

        if (!File.Exists(DataManager.configFile))
        {
            SceneManager.LoadScene("CONFIG");
            return;
        }
        }

        // Initialize reconnection UI
        InitializeReconnectionUI();

        // Try to initialize
        try
        {
            AppData.Instance.Initialize(SceneManager.GetActiveScene().name);
            isInitialized = true;
            AppLogger.SetCurrentScene(SceneManager.GetActiveScene().name);
            AppLogger.LogInfo($"'{SceneManager.GetActiveScene().name}' scene started.");
            daySummaries = AppData.Instance.userData.CalculateMoveTimePerDay();
            Debug.Log($"status : {DataManager.status}");

            appVersion.text = $"Version: {AppData.AppVersion}";
            if (!piChartUpdated)
            {
                UpdateUserData();
                UpdatePieChart();
            }
        }
        catch (Exception ex)
        {
            // Connection failed - show reconnection panel, keep retrying
            AppLogger.LogError($"Device connection failed on scene start: {ex.Message}");
            isInitialized = false;
            reconnectTimer = 0f;
        }

    }

    void Update()
    {
        PlutoComm.sendHeartbeat();

        // Try to initialize if not already done
        if (!isInitialized)
        {
            HandleReconnection();
        }

        // Attach button event when initialized (can happen after reconnection)
        if (!buttonEventAttached && isInitialized && ConnectToRobot.isPLUTO)
        {
            buttonEventAttached = true;
            PlutoComm.OnButtonReleased += onPlutoButtonReleased;
            AppLogger.LogInfo("✓ PLUTO button event attached.");
        }

        // ALWAYS update reconnection UI - this is critical!
        UpdateReconnectionUI();

        // Check if it time to switch to the next scene
        if (changeScene == true && isInitialized) {
            LoadTargetScene();
            changeScene = false;
        }
        if (Input.GetKey(KeyCode.LeftControl) &&
            Input.GetKey(KeyCode.LeftShift) &&
            Input.GetKeyDown(KeyCode.X)) // magic key combo
        {
            SceneManager.LoadScene("CONFIG");
            Debug.Log("Key pressed");
        }
    }

    public void onPlutoButtonReleased()
    {
        AppLogger.LogInfo("PLUTO button released.");
        changeScene = true;
    }

    private void LoadTargetScene()
    {
        AppLogger.LogInfo($"Switching to the next scene '{nextScene}'.");
        SceneManager.LoadScene(nextScene);
    } 


    private void UpdateUserData()
    {
        if (AppData.Instance.userData == null)
        {
            AppLogger.LogError("User data is null - cannot update UI");
            return;
        }

        userName.text = AppData.Instance.userData.hospNumber;

        int movetime = AppData.Instance.userData.totalMoveTimeRemaining;
        if (AppData.Instance.userData.isExceeded)
        {
            timeRemainingToday.text = $"Done +{movetime}[min]";
            timeRemainingToday.color = Color.green;
        }
        else
        {
            timeRemainingToday.text = $"{movetime} min";
        }

        todaysDay.text = AppData.Instance.userData.getCurrentDayOfTraining().ToString();
        todaysDate.text = DateTime.Now.ToString("ddd, dd-MM-yyyy");
        if (!File.Exists(awsManager.filePathUploadStatus))
            awsManager.createFile(userName.text);
    }

    private void UpdatePieChart()
    {
        if (daySummaries == null)
        {
            AppLogger.LogError("daySummaries is NULL!");
            return;
        }

        int N = daySummaries.Length;
        AppLogger.LogInfo($"UpdatePieChart: {N} days to display");

        for (int i = 0; i < N; i++)
        {
            Debug.Log($"{i} | {daySummaries[i].Day} | {daySummaries[i].Date} | {daySummaries[i].MoveTime}");

            if (prevDays[i] == null)
                AppLogger.LogError($"prevDays[{i}] is NULL - NOT ASSIGNED IN INSPECTOR!");
            else
                prevDays[i].text = daySummaries[i].Day;

            if (prevDates[i] == null)
                AppLogger.LogError($"prevDates[{i}] is NULL - NOT ASSIGNED IN INSPECTOR!");
            else
                prevDates[i].text = daySummaries[i].Date;

            if (pies[i] != null)
            {
                pies[i].fillAmount = daySummaries[i].MoveTime / AppData.Instance.userData.totalMoveTimePrsc;
                pies[i].color = new Color32(148,234,107,255);
            }
        }
        piChartUpdated = true;
    }

    private void InitializeReconnectionUI()
    {
        // Validate that panel references are assigned in inspector
        if (reconnectPanel == null)
        {
            AppLogger.LogError("Reconnect Panel not assigned in inspector!");
            return;
        }
        if (reconnectTimerText == null)
        {
            AppLogger.LogError("Reconnect Timer Text not assigned in inspector!");
            return;
        }

        // Ensure panel starts hidden
        reconnectPanel.SetActive(false);
        AppLogger.LogInfo("Reconnection UI initialized.");
    }

    private void HandleReconnection()
    {
        // Stop reconnection attempts if scene is being destroyed
        if (sceneDestroyed)
            return;

        reconnectTimer += Time.deltaTime;
        reconnectAttemptTimer += Time.deltaTime;

        // Try to reconnect every 1 second (main thread)
        if (reconnectAttemptTimer >= RECONNECT_ATTEMPT_INTERVAL && !ConnectToRobot.isPLUTO)
        {
            reconnectAttemptTimer = 0f;
            try
            {
                ConnectToRobot.Connect(AppData.COMPort);
            }
            catch (Exception ex)
            {
                Debug.Log($"Reconnection attempt failed: {ex.Message}");
            }
        }

        // When device connects, load user data
        if (ConnectToRobot.isPLUTO && !isInitialized)
        {
            try
            {
                PlutoComm.getVersion();
                // Start sensorstream.
                PlutoComm.sendHeartbeat();
                PlutoComm.setDiagnosticMode();
                AppData.Instance.userData = new PlutoUserData(DataManager.configFile, DataManager.sessionFile);
                isInitialized = true;
                daySummaries = AppData.Instance.userData.CalculateMoveTimePerDay();
                UpdateUserData();
                UpdatePieChart();
                AppLogger.LogInfo("✓ Patient data loaded successfully after reconnection.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to load patient data: {ex.Message}");
            }
        }

        // Timeout check
        if (reconnectTimer >= RECONNECT_TIMEOUT)
        {
            AppLogger.LogInfo($"Reconnection timeout ({RECONNECT_TIMEOUT}s). Device failed to connect.");
        }
    }

    private void UpdateReconnectionUI()
    {
        if (reconnectPanel == null)
        {
            AppLogger.LogError("Reconnect Panel is NOT assigned in inspector!");
            return;
        }

        // Hide panel if device is connected
        if (ConnectToRobot.isPLUTO)
        {
            if (reconnectPanel.activeSelf)
            {
                reconnectPanel.SetActive(false);
                lastDisplayedTime = -1;
                animationTimer = 0f;
                AppLogger.LogInfo("✓ Device connected! Reconnection panel HIDDEN.");
                Debug.Log("Device connected - panel hidden!");
            }
            return;
        }

        // Show panel if device is NOT connected
        if (!ConnectToRobot.isPLUTO)
        {
            if (!reconnectPanel.activeSelf)
            {
                reconnectPanel.SetActive(true);
                Debug.Log("Device NOT connected - panel shown!");
            }

            // Update status text
            if (reconnectStatusText != null)
            {
                reconnectStatusText.text = "Unable to connect to PLUTO device.\n\n" ;
            }
        }

        // Update timer display only when value changes
        if (reconnectTimerText != null && reconnectPanel.activeSelf)
        {
            int remainingTime = Mathf.Max(0, Mathf.CeilToInt(RECONNECT_TIMEOUT - reconnectTimer));
            if (remainingTime != lastDisplayedTime)
            {
                reconnectTimerText.text = $"Time Remaining: {remainingTime}s";
                lastDisplayedTime = remainingTime;
            }
        }

        // Animate arrow (breathing effect) and loading circle (rotation)
        if (reconnectPanel.activeSelf)
        {
            animationTimer += Time.deltaTime;

            // Arrow breathing effect (alpha fade)
            if (arrowImage != null)
            {
                float alpha = 0.5f + 0.5f * Mathf.Sin(animationTimer * BREATHING_SPEED * Mathf.PI);
                Color arrowColor = arrowImage.color;
                arrowColor.a = alpha;
                arrowImage.color = arrowColor;
            }

            // Loading circle rotation
            if (loadingCircleImage != null)
            {
                loadingCircleImage.transform.Rotate(0, 0, -ROTATION_SPEED * Time.deltaTime);
            }
        }
    }

    private void OnDestroy()
    {
        sceneDestroyed = true;
        if (ConnectToRobot.isPLUTO)
        {
            PlutoComm.OnButtonReleased -= onPlutoButtonReleased;
        }
    }

    private void OnApplicationQuit()
    {
        ConnectToRobot.disconnect();
        AppLogger.StopLogging();
    }
}