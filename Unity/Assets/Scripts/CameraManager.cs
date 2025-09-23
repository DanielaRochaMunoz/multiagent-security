using System.Collections.Generic;
using UnityEngine;
using TMPro; // Import the TextMeshPro namespace

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private List<Camera> cameras; // Drag and drop cameras in the Inspector
    [SerializeField] private TextMeshProUGUI cameraNameText; // TextMeshProUGUI to display the camera name
    private int currentCameraIndex = 0; // Keep track of the currently active camera

    void Start()
    {
        if (cameras == null || cameras.Count == 0)
        {
            Debug.LogError("No cameras assigned in the CameraSwitcher script!");
            return;
        }

        if (cameraNameText == null)
        {
            Debug.LogError("No TextMeshProUGUI component assigned to display the camera name!");
            return;
        }

        // Disable all cameras except the first one and update the UI
        for (int i = 0; i < cameras.Count; i++)
        {
            cameras[i].enabled = (i == currentCameraIndex);
        }

        UpdateCameraNameUI();
    }

    void Update()
    {
        // Check for spacebar press
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SwitchCamera();
        }
    }

    private void SwitchCamera()
    {
        // Disable the current camera
        cameras[currentCameraIndex].enabled = false;

        // Move to the next camera in the list (loop back to 0 if at the end)
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Count;

        // Enable the next camera
        cameras[currentCameraIndex].enabled = true;

        // Update the UI with the new camera name
        UpdateCameraNameUI();

        Debug.Log($"Switched to camera: {cameras[currentCameraIndex].name}");
    }

    private void UpdateCameraNameUI()
    {
        // Update the text to show the active camera's name
        cameraNameText.text = $"Active Camera: {cameras[currentCameraIndex].name}";
    }
}