using UnityEngine;
using UnityEngine.UI;

public class ToggleGameObject : MonoBehaviour
{
    public Toggle toggle;

    public GameObject onImage;
    public GameObject offImage;

    public bool trajON = true;

    public string settingName = "trajectory"; // "trajectory", "easyMode", etc.

    private void Start()
    {
        // Force default ON
        toggle.isOn = true;
        ApplyState(toggle.isOn);

        toggle.onValueChanged.AddListener(ApplyState);
    }

    private void ApplyState(bool isOn)
    {
        trajON = isOn;

        if (onImage != null) onImage.SetActive(isOn);
        if (offImage != null) offImage.SetActive(!isOn);

        // Save to AppData
        SaveSetting(isOn);
    }

    private void SaveSetting(bool value)
    {
        if (settingName == "easyMode")
        {
            AppData.Instance.PongEasyMode = value;
            AppLogger.LogInfo($"✓ Pong Easy Mode saved: {value}");
        }
        else if (settingName == "trajectory")
        {
            // Add other settings as needed
        }
    }
}
