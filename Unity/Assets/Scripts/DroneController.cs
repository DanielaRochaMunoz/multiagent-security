using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class DroneController : MonoBehaviour
{
    [Header("References")]
    public Transform landingPad;
    public GameObject terrain;
    public Camera mainCamera;      // Added from first script
    public Camera droneCamera;     // Added from first script

    [Header("Movement Settings")]
    public int gridResolution = 20;
    public float takeOffHeight = 10f; // Height to ascend from landing pad
    public float speed = 5f;
    public float landingSpeed = 2f;
    public float startDelay = 11f;
    private bool missionComplete = false;

    [Header("Manual Control Settings")]
    public float manualSpeed = 8f;
    public float manualVerticalSpeed = 5f;
    public float rotationSpeed = 300f;
    public float mouseSensitivity = 2.5f;
    private bool isCursorLocked = true;

    private bool isManualControl = false;
    private Vector3 lastAutomaticPosition;
    private int lastPatrolIndex;
    private bool wasPatrolling;
    private bool wasReturning;

    [Header("Camera Settings")]
    public float cameraTiltSpeed = 2f;
    public float maxTiltAngle = 45f;
    public float minTiltAngle = -45f;
    public float lookAheadDistance = 20f;
    public float cameraHeight = 2f;          // Height offset for camera position
    public float cameraForward = -2f;        // Forward offset for camera position
    public float droneCameraFOV = 90f;       // Wider FOV for drone camera
    private Vector3 targetLookPoint;
    private Quaternion targetRotation;

    [Header("Debug Visualization")]
    public bool showDebugVisuals = true;
    public Color boundaryColor = Color.yellow;
    public Color patrolPointColor = Color.red;
    public float lineWidth = 0.2f;
    public float pointSize = 0.5f;

    [Header("Boundary Vertices")]
    public Vector3 vertex0 = new Vector3(0, 10, 0);
    public Vector3 vertex1 = new Vector3(10, 10, 0);
    public Vector3 vertex2 = new Vector3(15, 10, 15);
    public Vector3 vertex3 = new Vector3(0, 10, 20);
    public Vector3 vertex4 = new Vector3(-15, 10, 15);
    public Vector3 vertex5 = new Vector3(-10, 10, 0);

    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;
    private bool isPatrolling = false;
    private bool isReturning = false;
    private List<GameObject> debugObjects = new List<GameObject>();
    private GameObject debugContainer;
    private bool canStart = false;
    private bool hasStarted = false;
    
    [Header("Flight Modes")]
    public bool isInvestigating = false;
    public Vector3 investigationTarget;
    private string currentMode = "patrol";
    
    [Header("Tracking Settings")]
    public GameObject bearTarget; // Reference to the bear object
    public float trackingHeight = 10f; // Height to maintain while tracking
    public float trackingDistance = 5f; // Distance to maintain from target
    public float orbitSpeed = 30f; // Speed of orbiting around target
    private float currentOrbitAngle = 0f;
    
    [Serializable]
    public class DroneCommand
    {
        public string command_type;
        public DroneCommandParameters parameters;
    }

    [Serializable]
    public class DroneCommandParameters
    {
        public string mode;
        public DronePosition position;
    }

    [Serializable]
    public class DronePosition
    {
        public float x;
        public float y;
        public float z;
    }

    void Start()
    {
        ValidateSetup();
        Debug.Log($"Landing pad position: {landingPad.position}");

        int droneLayer = 8;

        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = droneLayer;
        }

        if (droneCamera != null)
        {
            droneCamera.cullingMask &= ~(1 << droneLayer);
        }

        // Set initial position to landing pad position
        transform.position = new Vector3(landingPad.position.x, landingPad.position.y + 2f, landingPad.position.z);
        Debug.Log($"Initial drone position: {transform.position}");

        GeneratePatrolPoints();
        InitializeDebugVisuals();
        InitializeCameras();

        // Initialize camera target look point
        if (droneCamera != null)
        {
            targetLookPoint = transform.position + transform.forward * lookAheadDistance;
            targetRotation = droneCamera.transform.rotation;
        }

        // Initialize cursor lock
        UpdateCursorState(true);

        // Start the delayed sequence
        StartCoroutine(DelayedStart());
    }

    private void InitializeCameras()
    {
        if (mainCamera != null && droneCamera != null)
        {
            mainCamera.enabled = true;
            droneCamera.enabled = false;
        }
    }

    private void UpdateCameraRotation()
    {
        if (droneCamera == null) return;

        if (isInvestigating && bearTarget != null)
        {
            // Investigation camera behavior
            Vector3 directionToTarget = (bearTarget.transform.position - transform.position).normalized;
            directionToTarget.y -= 0.5f;
            directionToTarget.Normalize();

            Vector3 lookTarget = bearTarget.transform.position;
            targetLookPoint = Vector3.Lerp(targetLookPoint, lookTarget, Time.deltaTime * cameraTiltSpeed);
        }
        else if (isPatrolling)
        {
            // Patrol camera behavior - look at next patrol point
            Vector3 targetPoint = patrolPoints[currentPatrolIndex];
            Vector3 directionToTarget = (targetPoint - transform.position).normalized;
            
            // Add slight downward tilt for patrol
            directionToTarget.y -= 0.3f;
            directionToTarget.Normalize();

            // Calculate look target
            Vector3 lookTarget = transform.position + directionToTarget * lookAheadDistance;
            targetLookPoint = Vector3.Lerp(targetLookPoint, lookTarget, Time.deltaTime * cameraTiltSpeed);
        }

        // Apply camera rotation
        Vector3 directionToLookPoint = targetLookPoint - droneCamera.transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(directionToLookPoint);
        droneCamera.transform.rotation = Quaternion.Slerp(
            droneCamera.transform.rotation,
            targetRotation,
            Time.deltaTime * cameraTiltSpeed
        );
    }

    private void UpdateCameraPosition()
    {
        if (droneCamera != null)
        {
            // Position the camera relative to the drone
            Vector3 cameraPosition = transform.position +
                                     Vector3.up * cameraHeight +
                                     transform.forward * cameraForward;

            droneCamera.transform.position = cameraPosition;

            // Set the camera's FOV
            droneCamera.fieldOfView = droneCameraFOV;
        }
    }

    private Vector3[] GetBoundaryVertices()
    {
        return new Vector3[] { vertex0, vertex1, vertex2, vertex3, vertex4, vertex5 };
    }

    private IEnumerator DelayedStart()
    {
        Debug.Log($"Waiting {startDelay} seconds before starting drone sequence");
        yield return new WaitForSeconds(startDelay);
        canStart = true;
        hasStarted = true;  // Explicitly set hasStarted to true
        Debug.Log("Delay complete, initiating takeoff");
        
        // Begin takeoff sequence
        TakeOff();
    }

    private void ValidateSetup()
    {
        if (landingPad == null)
        {
            Debug.LogError("LandingPad is not assigned!");
            return;
        }

        if (terrain == null)
        {
            Debug.LogError("Terrain reference is missing!");
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogWarning("Main Camera reference is missing!");
        }

        if (droneCamera == null)
        {
            Debug.LogWarning("Drone Camera reference is missing!");
        }
    }

    private void SwitchToDroneCamera()
    {
        if (mainCamera != null && droneCamera != null)
        {
            mainCamera.enabled = false;
            droneCamera.enabled = true;
            Debug.Log("Switched to drone camera view");
        }
    }

    private void SwitchToMainCamera()
    {
        if (mainCamera != null && droneCamera != null)
        {
            mainCamera.enabled = true;
            droneCamera.enabled = false;
            Debug.Log("Switched to main camera view");
        }
    }
    
    private void TrackTarget()
    {
        if (bearTarget == null) return;

        // Update orbit angle
        currentOrbitAngle += orbitSpeed * Time.deltaTime;
        if (currentOrbitAngle >= 360f) currentOrbitAngle -= 360f;

        // Calculate desired position
        float targetX = bearTarget.transform.position.x + trackingDistance * Mathf.Cos(currentOrbitAngle * Mathf.Deg2Rad);
        float targetZ = bearTarget.transform.position.z + trackingDistance * Mathf.Sin(currentOrbitAngle * Mathf.Deg2Rad);
        float targetY = bearTarget.transform.position.y + trackingHeight;

        Vector3 targetPosition = new Vector3(targetX, targetY, targetZ);

        // Move towards the calculated position
        transform.position = Vector3.Lerp(transform.position, targetPosition, speed * Time.deltaTime);

        // Make the drone look at the target
        Vector3 directionToTarget = (bearTarget.transform.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, cameraTiltSpeed * Time.deltaTime);
    }
    
    public void HandleModeCommand(string mode, Vector3 target)
    {
        switch (mode)
        {
            case "investigate":
                StartInvestigation(target);
                break;
            case "patrol":
                ResumePatrol();
                break;
            case "manual":
                EnableManualControl();
                break;
        }
    }

    

    public void StartInvestigation(Vector3 target)
    {
        if (currentMode != "investigate" && bearTarget != null)
        {
            // Store current patrol state
            wasPatrolling = isPatrolling;
            wasReturning = isReturning;

            // Switch to investigation mode
            isPatrolling = false;
            isReturning = false;
            isInvestigating = true;
            currentMode = "investigate";

            Debug.Log($"Starting investigation, following bear");
        }
    }

    
    public void ResumePatrol()
    {
        if (currentMode != "patrol")
        {
            // Reset investigation state
            isInvestigating = false;
            investigationTarget = Vector3.zero;
            
            // Reset camera orientation
            if (droneCamera != null)
            {
                // Reset to a forward-facing, slightly downward position
                Vector3 defaultLookDirection = transform.forward;
                defaultLookDirection.y -= 0.3f;
                targetLookPoint = transform.position + defaultLookDirection * lookAheadDistance;
            }
            
            // Resume patrol
            isPatrolling = true;
            currentPatrolIndex = FindNearestPatrolPoint();
            currentMode = "patrol";
            
            Debug.Log("Resuming patrol mode");
        }
    }

    // Modified TakeOff method to include camera switch
    private void TakeOff()
    {
        float targetHeight = landingPad.position.y + takeOffHeight;

        // Debug the current height and target height
        Debug.Log($"Current height: {transform.position.y}, Target height: {targetHeight}");

        StartCoroutine(TakeOffSequence(targetHeight));
    }
    
    private IEnumerator TakeOffSequence(float targetHeight)
    {
        while (transform.position.y < targetHeight)
        {
            Vector3 newPosition = transform.position + Vector3.up * speed * Time.deltaTime;
            transform.position = newPosition;
            Debug.Log($"Moving up, new height: {newPosition.y}");
            yield return null;
        }

        // Ensure we're exactly at target height
        transform.position = new Vector3(transform.position.x, targetHeight, transform.position.z);
        Debug.Log("Takeoff complete, starting patrol");
        
        // Switch to drone camera and start patrolling
        SwitchToDroneCamera();
        isPatrolling = true;
        StartPatrol();
    }

    private void LogDroneState()
    {
        Debug.Log($"Drone State: canStart={canStart}, hasStarted={hasStarted}, " +
                  $"missionComplete={missionComplete}, isPatrolling={isPatrolling}, " +
                  $"isReturning={isReturning}, isManualControl={isManualControl}");
    }

    // Modified Land method to include camera switch and set y=4
    private void Land()
    {
        if (transform.position.y > landingPad.position.y + 2.3f)  // Reference landing pad height
        {
            transform.position += Vector3.down * landingSpeed * Time.deltaTime;
        }
        else
        {
            transform.position = new Vector3(landingPad.position.x, landingPad.position.y + 2.1f, landingPad.position.z);
            Debug.Log("Landing complete");

            StartCoroutine(DelaySwitchToMainCamera(3f));
            SwitchToMainCamera();
            isReturning = false;
            missionComplete = true;
        }
    }

    private IEnumerator DelaySwitchToMainCamera(float delay)
    {
        yield return new WaitForSeconds(delay);
        SwitchToMainCamera();
    }

    private void InitializeDebugVisuals()
    {
        ClearDebugVisuals();

        if (!showDebugVisuals) return;

        debugContainer = new GameObject("BoundaryVisuals");
        debugContainer.transform.parent = transform.parent;
        debugObjects.Add(debugContainer);

        Vector3[] boundaryVertices = GetBoundaryVertices();

        // Create boundary lines
        for (int i = 0; i < boundaryVertices.Length; i++)
        {
            Vector3 start = boundaryVertices[i];
            Vector3 end = boundaryVertices[(i + 1) % boundaryVertices.Length];
            CreateDebugLine(start, end, debugContainer.transform);
        }

        // Create patrol point markers
        if (patrolPoints != null)
        {
            foreach (Vector3 point in patrolPoints)
            {
                CreateDebugPoint(point, debugContainer.transform);
            }
        }
    }

    private void CreateDebugLine(Vector3 start, Vector3 end, Transform parent)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        Vector3 center = start + direction / 2;

        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        line.transform.parent = parent;

        line.transform.position = center;
        line.transform.localScale = new Vector3(lineWidth, distance / 2, lineWidth);
        line.transform.up = direction.normalized;

        Material material = new Material(Shader.Find("Standard"));
        material.color = boundaryColor;
        line.GetComponent<Renderer>().material = material;

        Destroy(line.GetComponent<Collider>()); // Changed from specific collider type to general
        debugObjects.Add(line);
    }

    private void CreateDebugPoint(Vector3 position, Transform parent)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.transform.parent = parent;
        point.transform.position = position;
        point.transform.localScale = new Vector3(pointSize, pointSize, pointSize);

        Material material = new Material(Shader.Find("Standard"));
        material.color = patrolPointColor;
        point.GetComponent<Renderer>().material = material;

        Destroy(point.GetComponent<Collider>()); // Changed from specific collider type to general
        debugObjects.Add(point);
    }

    private void GeneratePatrolPoints()
    {
        List<Vector3> points = new List<Vector3>();
        float padding = 1f;
        Vector3[] boundaryVertices = GetBoundaryVertices();

        // Calculate bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Vector3 vertex in boundaryVertices)
        {
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minZ = Mathf.Min(minZ, vertex.z);
            maxZ = Mathf.Max(maxZ, vertex.z);
        }

        minX += padding;
        maxX -= padding;
        minZ += padding;
        maxZ -= padding;

        float stepX = (maxX - minX) / (gridResolution - 1);
        float stepZ = (maxZ - minZ) / (gridResolution - 1);

        // Generate grid points
        for (int i = 0; i < gridResolution; i++)
        {
            for (int j = 0; j < gridResolution; j++)
            {
                float x = minX + (i * stepX);
                float z = minZ + (j * stepZ);
                Vector3 point = new Vector3(x, landingPad.position.y + takeOffHeight, z);

                if (IsPointInPolygon(point))
                {
                    points.Add(point);
                }
            }
        }

        // Add boundary edge points
        for (int i = 0; i < boundaryVertices.Length; i++)
        {
            Vector3 start = boundaryVertices[i];
            Vector3 end = boundaryVertices[(i + 1) % boundaryVertices.Length];

            for (float t = 0.1f; t < 0.9f; t += 0.15f)
            {
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y = landingPad.position.y + takeOffHeight; // Set y relative to landing pad

                Vector3 center = CalculatePolygonCenter();
                Vector3 directionToCenter = (center - point).normalized;
                point += directionToCenter * padding;

                if (!points.Contains(point) && IsPointInPolygon(point))
                {
                    points.Add(point);
                }
            }
        }

        // Add landing pad point
        Vector3 landingPadPoint = new Vector3(
            landingPad.position.x,
            landingPad.position.y + takeOffHeight,
            landingPad.position.z
        );
        if (!points.Contains(landingPadPoint) && IsPointInPolygon(landingPadPoint))
        {
            points.Add(landingPadPoint);
        }

        patrolPoints = points.ToArray();
        Debug.Log($"Generated {patrolPoints.Length} patrol points");
    }

    private Vector3 CalculatePolygonCenter()
    {
        Vector3[] boundaryVertices = GetBoundaryVertices();
        Vector3 center = Vector3.zero;
        foreach (Vector3 vertex in boundaryVertices)
        {
            center += vertex;
        }
        return center / boundaryVertices.Length;
    }

    private bool IsPointInPolygon(Vector3 point)
    {
        Vector3[] boundaryVertices = GetBoundaryVertices();
        bool inside = false;
        for (int i = 0, j = boundaryVertices.Length - 1; i < boundaryVertices.Length; j = i++)
        {
            if (((boundaryVertices[i].z <= point.z && point.z < boundaryVertices[j].z) ||
                 (boundaryVertices[j].z <= point.z && point.z < boundaryVertices[i].z)) &&
                (point.x < (boundaryVertices[j].x - boundaryVertices[i].x) * (point.z - boundaryVertices[i].z) /
                 (boundaryVertices[j].z - boundaryVertices[i].z) + boundaryVertices[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private void ClearDebugVisuals()
    {
        foreach (GameObject obj in debugObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        debugObjects.Clear();
    }

    private void UpdateCursorState(bool locked)
    {
        isCursorLocked = locked;
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        // Debug state every few seconds
        if (Time.frameCount % 100 == 0)
        {
            LogDroneState();
        }

        // Handle cursor lock toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UpdateCursorState(!isCursorLocked);
        }

        // Check for manual control toggle
        if (Input.GetKeyDown(KeyCode.F))
        {
            EnableManualControl();
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            DisableManualControl();
        }

        if (canStart && hasStarted && !missionComplete)
        {
            if (isManualControl)
            {
                HandleManualControl();
            }
            else if (isInvestigating && bearTarget != null)
            {
                investigationTarget = bearTarget.transform.position;
                TrackTarget();
                UpdateCameraRotation();
            }
            else if (isPatrolling)
            {
                Patrol();
                UpdateCameraRotation(); // Add camera update during patrol
            }
            else if (isReturning)
            {
                ReturnToLandingPad();
                UpdateCameraRotation(); // Add camera update during return
            }
        }
    }
    
    public void ProcessReceivedMessage(string message)
    {
        try
        {
            Debug.Log($"DroneController processing message: {message}");
            DroneCommand command = JsonUtility.FromJson<DroneCommand>(message);

            if (command.command_type == "set_flight_mode")
            {
                string mode = command.parameters.mode;
                Vector3 target = Vector3.zero;

                if (mode == "investigate" && command.parameters.position != null)
                {
                    target = new Vector3(
                        command.parameters.position.x,
                        command.parameters.position.y,
                        command.parameters.position.z
                    );
                }
                HandleModeCommand(mode, target);
            }
            else if (command.command_type == "navigate_to" && command.parameters.position != null)
            {
                Vector3 target = new Vector3(
                    command.parameters.position.x,
                    command.parameters.position.y,
                    command.parameters.position.z
                );
                AddNavigationTarget(target);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message in DroneController: {e.Message}");
        }
    }

    public void EnableManualControl()
    {
        if (!isManualControl)
        {
            isManualControl = true;
        
            // Save current state
            lastAutomaticPosition = transform.position;
            lastPatrolIndex = currentPatrolIndex;
            wasPatrolling = isPatrolling;
            wasReturning = isReturning;

            // Reset drone's rotation to eliminate pitch and roll, retaining only yaw
            Vector3 currentEulerAngles = transform.eulerAngles;
            Quaternion newDroneRotation = Quaternion.Euler(0, currentEulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, newDroneRotation, cameraTiltSpeed * Time.deltaTime);
            
            LogDroneAndCameraRotation();
            
            // Reset camera position and rotation
            if (droneCamera != null)
            {
                // Reset the camera's local rotation to ensure it's looking forward
                droneCamera.transform.localRotation = Quaternion.identity;
            
                // Optionally, set the camera's rotation to align perfectly forward
                droneCamera.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            
                // Update the camera's position to ensure it's correctly placed
                UpdateCameraPosition();
            }

            // Lock the drone's rotation to only allow yaw during Manual Control
            // (Optional: Implement as per requirement)
            // This can be done in LateUpdate as shown earlier

            // Pause automatic behavior
            isPatrolling = false;
            isReturning = false;
            isInvestigating = false;

            Debug.Log("Manual control enabled with drone rotation reset");
        }
    }
    
    
    private void LateUpdate()
    {
        if (isManualControl)
        {
            // Lock the drone's rotation to only allow yaw
            Vector3 eulerAngles = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0, eulerAngles.y, 0);
        }
    }

    private void LogDroneAndCameraRotation()
    {
        Debug.Log($"Drone Rotation: {transform.rotation.eulerAngles}");
        if (droneCamera != null)
        {
            Debug.Log($"Camera Rotation: {droneCamera.transform.rotation.eulerAngles}");
        }
    }

    public void DisableManualControl()
    {
        if (isManualControl)
        {
            isManualControl = false;

            // Determine whether to resume patrol or return sequence
            if (wasPatrolling)
            {
                isPatrolling = true;
                currentPatrolIndex = FindNearestPatrolPoint();
            }
            else if (wasReturning)
            {
                isReturning = true;
            }

            Debug.Log("Resuming automatic control");
        }
    }

    public void HandleManualControl()
    {
        // Only process input if cursor is locked
        if (!isCursorLocked) return;

        // Get input axes for movement
        float horizontal = Input.GetAxis("Horizontal"); // A/D keys
        float vertical = Input.GetAxis("Vertical");     // W/S keys
        float upDown = 0;

        // Mouse input for camera rotation with added sensitivity
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Up/Down movement with Space/Left Shift
        if (Input.GetKey(KeyCode.Space)) upDown = 1;
        if (Input.GetKey(KeyCode.LeftShift)) upDown = -1;

        // Handle drone rotation with mouse
        transform.Rotate(Vector3.up, mouseX * rotationSpeed * Time.deltaTime);

        // Handle camera pitch (vertical rotation)
        if (droneCamera != null)
        {
            // Get current vertical angle
            Vector3 currentRotation = droneCamera.transform.localEulerAngles;
            float currentXAngle = currentRotation.x;

            // Convert angle to -180 to 180 range
            if (currentXAngle > 180)
                currentXAngle -= 360;

            // Calculate new vertical angle (pitch)
            float newXAngle = currentXAngle - mouseY * rotationSpeed * Time.deltaTime;

            // Clamp the vertical angle between minTiltAngle and maxTiltAngle
            newXAngle = Mathf.Clamp(newXAngle, minTiltAngle, maxTiltAngle);

            // Apply the new rotation to the camera
            droneCamera.transform.localEulerAngles = new Vector3(newXAngle, 0, 0);
        }

        // Calculate movement direction in world space
        Vector3 forward = transform.forward * vertical;
        Vector3 right = transform.right * horizontal;
        Vector3 up = Vector3.up * upDown;

        // Combine movements
        Vector3 movement = (forward + right).normalized * manualSpeed;
        movement += up * manualVerticalSpeed;

        // Apply movement
        transform.position += movement * Time.deltaTime;
    }

    private int FindNearestPatrolPoint()
    {
        float minDistance = float.MaxValue;
        int nearestIndex = 0;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, patrolPoints[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError("No patrol points available!");
            return;
        }

        Vector3 target = patrolPoints[currentPatrolIndex];
        MoveTowardsTarget(target);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >= patrolPoints.Length)
            {
                Debug.Log("Patrol complete, returning to base");
                isPatrolling = false;
                isReturning = true;
                currentPatrolIndex = 0;
            }
        }
    }
    
    // Add these methods to your DroneController class

    public void AddNavigationTarget(Vector3 target)
    {
        // Convert patrolPoints array to a list for easier management
        List<Vector3> patrolList = new List<Vector3>(patrolPoints);
        patrolList.Add(target);

        // Update patrol points
        patrolPoints = patrolList.ToArray();

        // Update debug visuals if needed
        if (showDebugVisuals)
        {
            InitializeDebugVisuals();
        }

        Debug.Log($"Added new navigation target: {target}");
    }

    public void StartPatrol()
    {
        if (!isPatrolling && !isReturning)
        {
            isPatrolling = true;
            currentPatrolIndex = 0;
            Debug.Log("Patrol started");
        }
    }

    public void StopPatrol()
    {
        isPatrolling = false;
        Debug.Log("Patrol stopped");
    }

    private void ReturnToLandingPad()
    {
        // First move to position above landing pad
        Vector3 targetAbove = new Vector3(landingPad.position.x, transform.position.y, landingPad.position.z);
        MoveTowardsTarget(targetAbove);

        // Once we're above the landing pad, start descending
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(landingPad.position.x, 0, landingPad.position.z)) < 0.1f)
        {
            Land();
        }
    }

    private void MoveTowardsTarget(Vector3 target)
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        Vector3[] boundaryVertices = GetBoundaryVertices();
        if (!Application.isPlaying && boundaryVertices != null)
        {
            Gizmos.color = boundaryColor;
            for (int i = 0; i < boundaryVertices.Length; i++)
            {
                Gizmos.DrawLine(boundaryVertices[i],
                    boundaryVertices[(i + 1) % boundaryVertices.Length]);
            }
        }
    }

    void OnDestroy()
    {
        ClearDebugVisuals();
    }
    
    public void ToggleDebugVisuals()
    {
        showDebugVisuals = !showDebugVisuals;
        InitializeDebugVisuals();
        Debug.Log($"Debug visuals toggled to: {showDebugVisuals}");
    }

    void OnValidate()
    {
        // Update visuals when values change in inspector
        if (Application.isPlaying && debugContainer != null)
        {
            InitializeDebugVisuals();
        }
    }
}
