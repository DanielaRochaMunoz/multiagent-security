using UnityEngine;
using System.Collections;

public class CameraAnimationHandler : MonoBehaviour
{
    public float transitionBuffer = 0.1f;
    public Animator cameraAnimator;
    public DroneController droneController;
    
    void Start() {
        Debug.Log($"Animation state at start: {cameraAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime}");
        Debug.Log($"Current camera position: {transform.position}");
        
        Camera cam = GetComponent<Camera>();
        if (cam != null) {
            cam.tag = "MainCamera";
            if (Camera.main != null) Camera.main.tag = "Untagged"; // Remove MainCamera tag from any other camera
            Debug.Log($"Set camera {cam.name} as main camera");
        }
        
        if (Camera.main != null) {
            Debug.Log($"Main camera culling mask: {Camera.main.cullingMask}");
            Camera.main.cullingMask |= (1 << LayerMask.NameToLayer("Default"));
        }
        
        if (cameraAnimator != null) {
            var camera = GetComponent<Camera>();
            if (camera != null) {
                camera.Render();
                Debug.Log($"Camera rendered at position: {camera.transform.position}");
            }
        }

        if (!cameraAnimator.GetCurrentAnimatorStateInfo(0).IsName("MainCameraAnimation")) {
            cameraAnimator.Play("MainCameraAnimation", 0, 0f);
        }
    }

    void Update() {
        var state = cameraAnimator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("MainCameraAnimation") && 
            state.normalizedTime >= (1f - transitionBuffer)) {
            StartCoroutine(TransitionToDrone());
        }
    }
    
    IEnumerator TransitionToDrone() {
        yield return new WaitForSeconds(transitionBuffer);

        Camera currentCam = GetComponent<Camera>();
        // Use droneCamera field instead of GetComponent
        Camera droneCamera = droneController?.droneCamera;

        if (droneCamera != null) {
            if (currentCam != null) {
                currentCam.enabled = false;
            }
            droneCamera.enabled = true;
            droneController.enabled = true;
            Debug.Log($"Switched to drone camera at {droneCamera.transform.position}");
        } else {
            Debug.LogError("Drone camera reference not set on DroneController!");
        }

        enabled = false;
    }
}