// using System.Collections;

// using System.Collections.Concurrent;

// using System.Collections.Generic;

// using System.Diagnostics;

// using System.IO;

// using System.Threading;

// using TMPro;
 
// using UnityEngine;
// using UnityEngine.SceneManagement;
// using UnityEngine.UI;

// using XCharts.Runtime;

// using Debug = UnityEngine.Debug;

// public class dataUpload : MonoBehaviour

// {

//     public TextMeshProUGUI dataStatus;

//     public string status = null;

//     private bool hasUploaded = false; // ensures script runs only
 
//     public string message = "waiting...";

//     public float progress = 0f;

//     public Image progressBar;

//     public TextMeshProUGUI numFiles;

//     int totalFiles;

//     void Start()

//     {

//         Debug.Log($"status : {DataManager.status}");
//         AppLogger.SetCurrentScene(SceneManager.GetActiveScene().name);
//     AppLogger.LogInfo($"{SceneManager.GetActiveScene().name} scene started.");

//         StartCoroutine(CheckUploadStatusRoutine());

//     }
 
//     void Update()

//     {

//         if (DataManager.status != "no_upload")

//         {

//             // dataStatus.text = "Data is Uploading...";

//             dataStatus.color = Color.green;

//         }

//         // dataStatus.text = $"{DataManager.status}";

//         dataStatus.text = message;

//         numFiles.text = $"Files To Upload : {totalFiles.ToString()}";

 
//         if (message == "DONE")

//         {

//             ShutdownSystem();

//         }

//         progressBar.fillAmount = progress / 100f;

//     }

//     public void uploadfile()

//     {

//         // dataStatus.text = $"{DataManager.status}";

//         if (DataManager.status == "upload_needed" && !hasUploaded)

//         {

//             hasUploaded = true;

//             Debug.Log("Upload started...");
//             AppLogger.LogInfo($"Upload started...");


//             RunPythonUploader();

//         }

//         else if (DataManager.status == "no_upload" && hasUploaded)

//         {

//             Debug.Log("Upload completed. Shutting down...");
//             AppLogger.LogInfo($"Upload completed. Shutting down...");


//             ShutdownSystem();

//         }

//         else if (DataManager.status == "no_upload")

//         {
 
//             Debug.Log("Upload completed. Shutting down...");
//             AppLogger.LogInfo($"Upload completed. Shutting down...");


//             ShutdownSystem();

//         }

//     }

//     IEnumerator CheckUploadStatusRoutine()

//     {

//         while (true)

//         {

//            ReadFile();

//             // dataStatus.text = $"{DataManager.status}";

//             if (DataManager.status == "upload_needed" && !hasUploaded)

//             {

//                 hasUploaded = true;

//                 Debug.Log("Upload started...");
//             AppLogger.LogInfo($"Upload started....");


//                 RunPythonUploader();

//             }

//             else if (DataManager.status == "no_upload" && hasUploaded)

//             {

//                 Debug.Log("Upload completed. Shutting down...");

//                 ShutdownSystem();

//                 yield break;

//             }

//             else if (DataManager.status == "no_upload")

//             {
 
//                 Debug.Log("Upload completed. Shutting down...");
//             AppLogger.LogInfo($"Upload completed. Shutting down...");

//                 ShutdownSystem();

//                 yield break;

//             }
 
//             yield return new WaitForSeconds(60f); // wait 1 minute

//         }

//     }

//     public void ReadFile()

//     {

//         if (!File.Exists(DataManager.GetUploadStatusFile))

//         {

//             Debug.LogError("File not found: " + DataManager.GetUploadStatusFile);
//             AppLogger.LogError($"File not found:  { DataManager.GetUploadStatusFile}");

//             return;

//         }
 
//         string[] lines = File.ReadAllLines(DataManager.GetUploadStatusFile);

//         string status;
 
//         foreach (string line in lines)

//         {

//             if (string.IsNullOrWhiteSpace(line)) continue;
 
//             string[] parts = line.Split(',');
 
//             if (parts.Length > 1)

//             {

//                 status = parts[1].Trim(); // second column

//                 DataManager.setStatus(status);
 
//                 if (status == "upload_needed")

//                 {

//                     // dataStatus.text = "Upload needed";

//                     Debug.Log("Upload is needed!");

//                 }

//                 else if (status == "no_upload")

//                 {

//                     // dataStatus.text = "No upload required";

//                     Debug.Log("No upload required.");

//                 }

//                 else

//                 {

//                     Debug.Log("Unknown status: " + status);

//                 }

//             }

//         }

//     }
 
 
//     void RunPythonUploader()

//     {

//         string pythonScriptPath = @"C:/pythonscripts/uploadToAWS.pyw";

//         // string pythonExecutionPath = @"C:/Users/Homer 6/AppData/Local/Programs/Python/Python313/pythonw.exe";

//         // string pythonExecutionPath = @"C:/Users/gokul/AppData/Local/Programs/Python/Python313/pythonw.exe";
//         // string pythonExecutionPath = @"C:/Program Files/Python314/pythonw.exe"; //device-6
//         // string pythonExecutionPath = @"C:/Program Files/Python313/pythonw.exe";// device -2
//         // string pythonExecutionPath = @"C:/Users/HOMER_10/AppData/Local/Programs/Python/Python314/pythonw.exe";  //Device-7
//         // string pythonExecutionPath = @"C:/Users/HOMER_08/AppData/Local/Programs/Python/Python313/pythonw.exe"; //Device -8
//         // string pythonExecutionPath = @"C:/Users/HOMER_11/AppData/Local/Programs/Python/Python314/pythonw.exe";  //Device-9

//         string pythonExecutionPath = awsManager.pythonExecutionPath;


//         if (!File.Exists(pythonScriptPath))

//         {

//             Debug.LogError("Python script not found: " + pythonScriptPath);
//             AppLogger.LogError($"Python script not found: {pythonScriptPath}");


//             return;

//         }
 
//         try
//         {

//             Process process = new Process();

//             process.StartInfo.FileName = pythonExecutionPath;

//             process.StartInfo.Arguments = $"\"{pythonScriptPath}\"";

//             process.StartInfo.UseShellExecute = false;

//             process.StartInfo.CreateNoWindow = true;

//             // ? Redirect both standard output and error

//             process.StartInfo.RedirectStandardOutput = true;

//             process.StartInfo.RedirectStandardError = true;

//             process.OutputDataReceived += OnPythonOutput;

//             process.ErrorDataReceived += OnPythonError;
 
//             process.Start();
 
//             process.BeginOutputReadLine();

//             process.BeginErrorReadLine();
 
//             // Optional: Thread to wait for process exit

//             new Thread(() =>

//             {

//                 process.WaitForExit();

//                 Debug.Log("Python uploader finished.");

//             }).Start();
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError("Error running Python script: " + ex.Message);
//         }
//     }

//     private void OnPythonOutput(object sender, DataReceivedEventArgs e)
//     {
//         if (string.IsNullOrEmpty(e.Data))
//             return;

//         Debug.Log($"[Python] {e.Data}");

//         // --- Handle total file info (don't show on UI) ---

//         if (e.Data.StartsWith("TOTAL_FILES:"))
//         {
//             totalFiles = int.Parse(e.Data.Split(':')[1]);
//             AppLogger.LogInfo($"Files To Upload : {totalFiles.ToString()}");

//             return; // Skip showing in UI text
//         }
//         if (e.Data.StartsWith("COMMAND:"))
//         {
//             message = e.Data.Split(':')[1];
//             return; // Skip showing in UI text
//         }
//         // Try to parse progress (Python prints numbers like 25.0, 50.0, etc.)
//         if (float.TryParse(e.Data, out float p))
//         {
//             progress = Mathf.Clamp(p, 0f, 100f);
//         }
//         else if (e.Data == "DONE")
//         {
//             progress = 100f;
//             Debug.Log("Upload complete!");
//             AppLogger.LogInfo("Upload Completed");
//             message = e.Data;
//         }
//         else if (e.Data.StartsWith("ERROR"))
//         {
//             Debug.LogError(e.Data);
//             AppLogger.LogError($"{e.Data}");
//             message = e.Data;
//             ShutdownSystem();
//         }
//     }
 
//     private void OnPythonError(object sender, DataReceivedEventArgs e)
//     {
//         if (!string.IsNullOrEmpty(e.Data))
//         {
//             Debug.LogError("[Python ERROR] " + e.Data);
//         }
//     }
 
//     void ShutdownSystem()
//     {
//         try
//         {
//             AppLogger.StopLogging();
//             PlutoAanLogger.StopLogging();
//             PlutoComLogger.StopLogging();
//             // Application.Quit();
//             // Process.Start("shutdown", "/s /t 0");

//             #if UNITY_EDITOR
//                         UnityEditor.EditorApplication.isPlaying = false;
//             #endif

//             // Process.Start("shutdown", "/s /t 0");
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError("Failed to shutdown: " + ex.Message);
//         }
//     }
// }




using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XCharts.Runtime;
using Debug = UnityEngine.Debug;

public class dataUpload : MonoBehaviour
{
    public TextMeshProUGUI dataStatus;
    public string status = null;
    private bool hasUploaded = false;
    public string message = "waiting...";
    public float progress = 0f;
    public Image progressBar;
    public TextMeshProUGUI numFiles;
    int totalFiles;
    
    private bool isShuttingDown = false;
    private bool isProcessing = false;

    void Start()
    {
        Debug.Log($"status : {DataManager.status}");
        AppLogger.SetCurrentScene(SceneManager.GetActiveScene().name);
        AppLogger.LogInfo($"{SceneManager.GetActiveScene().name} scene started.");
        StartCoroutine(CheckUploadStatusRoutine());
    }

    void Update()
    {
        if (DataManager.status != "no_upload")
        {
            dataStatus.color = Color.green;
        }
        
        dataStatus.text = message;
        numFiles.text = $"Files To Upload : {totalFiles.ToString()}";

        if (message == "DONE" && !isShuttingDown)
        {
            StartShutdownSequence();
        }
        
        progressBar.fillAmount = progress / 100f;
    }

    public void uploadfile()
    {
        if (isShuttingDown || isProcessing) return;
        
        if (DataManager.status == "upload_needed" && !hasUploaded)
        {
            isProcessing = true;
            hasUploaded = true;
            Debug.Log("Upload started...");
            AppLogger.LogInfo($"Upload started...");
            RunPythonUploader();
        }
        else if (DataManager.status == "no_upload" && hasUploaded)
        {
            Debug.Log("Upload completed. Shutting down...");
            AppLogger.LogInfo($"Upload completed. Shutting down...");
            StartShutdownSequence();
        }
        else if (DataManager.status == "no_upload")
        {
            Debug.Log("Upload completed. Shutting down...");
            AppLogger.LogInfo($"Upload completed. Shutting down...");
            StartShutdownSequence();
        }
    }

    IEnumerator CheckUploadStatusRoutine()
    {
        while (true)
        {
            ReadFile();
            
            if (DataManager.status == "upload_needed" && !hasUploaded && !isProcessing)
            {
                isProcessing = true;
                hasUploaded = true;
                Debug.Log("Upload started...");
                AppLogger.LogInfo($"Upload started....");
                RunPythonUploader();
            }
            else if (DataManager.status == "no_upload" && hasUploaded)
            {
                Debug.Log("Upload completed. Shutting down...");
                AppLogger.LogInfo($"Upload completed. Shutting down...");
                StartShutdownSequence();
                yield break;
            }
            else if (DataManager.status == "no_upload")
            {
                Debug.Log("Upload completed. Shutting down...");
                AppLogger.LogInfo($"Upload completed. Shutting down...");
                StartShutdownSequence();
                yield break;
            }
            
            yield return new WaitForSeconds(60f);
        }
    }

    public void ReadFile()
    {
        if (!File.Exists(DataManager.GetUploadStatusFile))
        {
            Debug.LogError("File not found: " + DataManager.GetUploadStatusFile);
            AppLogger.LogError($"File not found: {DataManager.GetUploadStatusFile}");
            return;
        }

        string[] lines = File.ReadAllLines(DataManager.GetUploadStatusFile);
        string status;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split(',');

            if (parts.Length > 1)
            {
                status = parts[1].Trim();
                DataManager.setStatus(status);

                if (status == "upload_needed")
                {
                    Debug.Log("Upload is needed!");
                }
                else if (status == "no_upload")
                {
                    Debug.Log("No upload required.");
                }
                else
                {
                    Debug.Log("Unknown status: " + status);
                }
            }
        }
    }

    void RunPythonUploader()
    {
        string pythonScriptPath = @"C:/pythonscripts/uploadToAWS.pyw";
        string pythonExecutionPath = awsManager.pythonExecutionPath;

        if (!File.Exists(pythonScriptPath))
        {
            Debug.LogError("Python script not found: " + pythonScriptPath);
            AppLogger.LogError($"Python script not found: {pythonScriptPath}");
            isProcessing = false;
            return;
        }

        try
        {
            Process process = new Process();
            process.StartInfo.FileName = pythonExecutionPath;
            process.StartInfo.Arguments = $"\"{pythonScriptPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += OnPythonOutput;
            process.ErrorDataReceived += OnPythonError;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            new Thread(() =>
            {
                process.WaitForExit();
                Debug.Log("Python uploader finished.");
                isProcessing = false;
            }).Start();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error running Python script: " + ex.Message);
            AppLogger.LogError($"Error running Python script: {ex.Message}");
            isProcessing = false;
        }
    }

    private void OnPythonOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data))
            return;

        Debug.Log($"[Python] {e.Data}");

        if (e.Data.StartsWith("TOTAL_FILES:"))
        {
            totalFiles = int.Parse(e.Data.Split(':')[1]);
            AppLogger.LogInfo($"Files To Upload : {totalFiles.ToString()}");
            return;
        }
        if (e.Data.StartsWith("COMMAND:"))
        {
            message = e.Data.Split(':')[1];
            return;
        }
        if (float.TryParse(e.Data, out float p))
        {
            progress = Mathf.Clamp(p, 0f, 100f);
        }
        else if (e.Data == "DONE")
        {
            progress = 100f;
            Debug.Log("Upload complete!");
            AppLogger.LogInfo("Upload Completed");
            message = e.Data;
        }
        else if (e.Data.StartsWith("ERROR"))
        {
            Debug.LogError(e.Data);
            AppLogger.LogError($"{e.Data}");
            message = e.Data;
            isProcessing = false;
            StartShutdownSequence();
        }
    }

    private void OnPythonError(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Debug.LogError("[Python ERROR] " + e.Data);
            AppLogger.LogError($"[Python ERROR] {e.Data}");
        }
    }

    private void StartShutdownSequence()
    {
        if (isShuttingDown) return;
        isShuttingDown = true;
        StartCoroutine(ShutdownSequence());
    }

    private IEnumerator ShutdownSequence()
    {
        // 1. Log final message
        AppLogger.LogInfo("Data upload scene shutting down...");
        yield return new WaitForSeconds(0.2f);

        // 2. Stop all loggers with delays to flush buffers
        AppLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);
        
        PlutoAanLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);
        
        PlutoComLogger.StopLogging();
        yield return new WaitForSeconds(0.5f);

        // 3. Final confirmation log
        Debug.Log("All loggers stopped. Performing system shutdown...");
        yield return new WaitForSeconds(0.2f);

        // 4. Perform system shutdown
        PerformSystemShutdown();
    }

    private void PerformSystemShutdown()
    {
        try
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            if (AppData.isNRSVersion)
            {
                Application.Quit();
            }
            else
            {
                // Use shutdown with a small delay to ensure all processes complete
                Process.Start("shutdown", "/s /t 2");
            }
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to shutdown: " + ex.Message);
            // Fallback to Application.Quit if shutdown fails
            Application.Quit();
        }
    }

    private void OnDestroy()
    {
        // Ensure loggers are stopped if object is destroyed unexpectedly
        if (!isShuttingDown)
        {
            AppLogger.StopLogging();
            PlutoAanLogger.StopLogging();
            PlutoComLogger.StopLogging();
        }
    }
}
 