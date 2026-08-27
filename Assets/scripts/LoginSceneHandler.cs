using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginSceneHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Transform resultContainer;
    [SerializeField] private Button resultButtonPrefab;

    private List<string> allPatientIDs = new List<string>();
    private List<GameObject> currentResults = new List<GameObject>();
    private const int MAX_VISIBLE_RESULTS = 10;

    void Start()
    {
        PlutoComm.sendHeartbeat();
        AppLogger.SetCurrentScene(SceneManager.GetActiveScene().name);
        AppLogger.LogInfo($"'{SceneManager.GetActiveScene().name}' scene started.");

        if (!AppData.isNRSVersion)
        {
            if (!Directory.Exists(DataManager.basePath))
            {
                SceneManager.LoadScene("CONFIG");
                return;
            }

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

        LoadAllPatientIDs();

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchInputChanged);
    }

    void LoadAllPatientIDs()
    {
        allPatientIDs.Clear();
        if (!Directory.Exists(DataManager.basePath))
            return;

        allPatientIDs = Directory.GetDirectories(DataManager.basePath)
            .Select(Path.GetFileName)
            .Where(name => !name.ToLower().Contains("meta"))
            .OrderBy(x => x)
            .ToList();

        if (allPatientIDs.Count == 1)
        {
            AppData.Instance.setUser(allPatientIDs[0]);
            DataManager.setUserId(AppData.Instance.userID);
            if (searchInput != null)
                searchInput.text = allPatientIDs[0];
            ClearResults();
        }
        else if (allPatientIDs.Count > 0)
        {
            DisplayResults(allPatientIDs.Take(MAX_VISIBLE_RESULTS).ToList());
        }
    }

    void OnSearchInputChanged(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            DisplayResults(allPatientIDs.Take(MAX_VISIBLE_RESULTS).ToList());
            return;
        }

        var filtered = allPatientIDs
            .Where(id => id.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MAX_VISIBLE_RESULTS)
            .ToList();

        DisplayResults(filtered);
    }

    void DisplayResults(List<string> results)
    {
        // if (resultContainer == null)
        // {
        //     Debug.LogError("LoginSceneHandler: resultContainer not assigned in Inspector!");
        //     return;
        // }

        ClearResults();

        foreach (var patientID in results)
        {
            GameObject buttonObj = new GameObject(patientID);
            buttonObj.transform.SetParent(resultContainer, false);

            // Add RectTransform
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40);

            // Add Image background
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.9f, 0.9f, 0.9f);

            // Add Button
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.highlightedColor = new Color(0.7f, 0.9f, 0.95f); // Light blue highlight
            colors.pressedColor = new Color(0.5f, 0.8f, 0.9f);      // Darker blue pressed
            button.colors = colors;

            // Add LayoutElement for fixed height
            LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 40;
            layoutElement.layoutPriority = 1;

            // Create child for text (avoid multiple Graphic components)
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(rect, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = patientID;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 28;
            text.color = Color.black;
            text.fontStyle = FontStyles.Bold;

            // Try to set Poppins Bold font (if available)
            TMP_FontAsset poppinsBold = Resources.Load<TMP_FontAsset>("Fonts & Materials/Poppins-Bold SDF");
            if (poppinsBold != null)
                text.font = poppinsBold;

            // Add button click listener
            string id = patientID;
            button.onClick.AddListener(() => SelectPatient(id));

            currentResults.Add(buttonObj);
        }
    }

    void ClearResults()
    {
        foreach (var result in currentResults)
        {
            Destroy(result);
        }
        currentResults.Clear();
    }

    void SelectPatient(string patientID)
    {
        if (searchInput != null)
            searchInput.text = patientID;
        AppData.Instance.setUser(patientID);
        DataManager.setUserId(patientID);
        ClearResults();
    }

    void Update()
    {
        PlutoComm.sendHeartbeat();

        if (searchInput != null && searchInput.isFocused && Input.GetKeyDown(KeyCode.Return))
        {
            if (currentResults.Count > 0)
            {
                var firstButton = currentResults[0].GetComponent<Button>();
                if (firstButton != null)
                    firstButton.onClick.Invoke();
            }
            else if (allPatientIDs.Contains(searchInput.text))
            {
                SelectPatient(searchInput.text);
            }
        }
    }

    public void OnLoginButtonClick()
    {
        if (searchInput == null || string.IsNullOrEmpty(searchInput.text))
        {
            Debug.LogError("Please enter a Patient ID");
            return;
        }

        string selectedID = searchInput.text.Trim();

        if (!allPatientIDs.Contains(selectedID))
        {
            Debug.LogError($"Patient ID '{selectedID}' not found");
            return;
        }

        SelectPatient(selectedID);
        SceneManager.LoadScene("MAIN");
    }

    void OnDestroy()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(OnSearchInputChanged);
        }
    }
}
