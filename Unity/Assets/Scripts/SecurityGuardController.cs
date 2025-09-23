using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SecurityGuardController : MonoBehaviour
{
    [Header("References")]
    public Transform trailer;        // Added trailer reference
    public Camera mainCamera;
    public Camera droneCamera;

    [Header("Movement Settings")]
    public int gridResolution = 20;
    public float speed = 5f;
    public float startDelay = 11f;
    private bool missionComplete = false;
    private const float FIXED_HEIGHT = 4.9f; // Fixed height for movement

    [Header("Camera Settings")]
    public float cameraHeight = 2f;
    public float cameraForward = -2f;
    public float droneCameraFOV = 90f;

    [Header("Debug Visualization")]
    public bool showDebugVisuals = true;
    public Color boundaryColor = Color.yellow;
    public Color patrolPointColor = Color.red;
    public float lineWidth = 0.2f;
    public float pointSize = 0.5f;

    [Header("Boundary Vertices")]
    public Vector3 vertex0 = new Vector3(0, FIXED_HEIGHT, 0);
    public Vector3 vertex1 = new Vector3(10, FIXED_HEIGHT, 0);
    public Vector3 vertex2 = new Vector3(15, FIXED_HEIGHT, 15);
    public Vector3 vertex3 = new Vector3(0, FIXED_HEIGHT, 20);
    public Vector3 vertex4 = new Vector3(-15, FIXED_HEIGHT, 15);
    public Vector3 vertex5 = new Vector3(-10, FIXED_HEIGHT, 0);
    
    [Header("Alert System")]
    public bool isAlertActive = false;
    public int numberOfGuardsToSpawn = 10;
    public float spawnRadius = 2f;
    public GameObject guardPrefab;
    private List<GameObject> spawnedGuards = new List<GameObject>();
    private bool hasSpawnedGuards = false;
    private Vector3[] spawnOffsets;  // Store the initial offset positions
    
    [Header("Tracking Settings")]
    public GameObject bearTarget;
    public float interceptSpeed = 7f;
    public float interceptDistance = 1f;
    
    [Header("Alert Coordination")]
    private bool isReadyToIntercept = false;
    private float alertPreparationTime = 0.5f; // Reduced from 2.0f to 0.5f
    private float formationTime = 0.2f; // Time for guards to get into formation
    private bool isMainGuard = true;
    private static bool canInterceptBear = false;

    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;
    private bool isPatrolling = false;
    private bool isReturning = false;    // New flag for returning to trailer
    private List<GameObject> debugObjects = new List<GameObject>();
    private GameObject debugContainer;
    private bool canStart = false;
    private bool hasStarted = false;


    void Start()
    {
        ValidateSetup();

        // Layer handling
        int droneLayer = 10;
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = droneLayer;
        }

        if (droneCamera != null)
        {
            droneCamera.cullingMask &= ~(1 << droneLayer);
        }

        GeneratePatrolPoints();

        // Set initial position to the trailer's position
        if (trailer != null)
        {
            transform.position = new Vector3(trailer.position.x, FIXED_HEIGHT, trailer.position.z);
            Debug.Log($"Initial guard position: {transform.position}");
        }
        else
        {
            Debug.LogError("Trailer is not assigned!");
        }

        InitializeDebugVisuals();
        StartCoroutine(DelayedStart());
        InitializeCameras();

        // Initialize spawn offsets array
        spawnOffsets = new Vector3[numberOfGuardsToSpawn];
        for (int i = 0; i < numberOfGuardsToSpawn; i++)
        {
            float angle = i * (360f / numberOfGuardsToSpawn);
            float x = spawnRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = spawnRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
            spawnOffsets[i] = new Vector3(x, 0, z);
        }
    }

    private void InitializeCameras()
    {
        if (mainCamera != null && droneCamera != null)
        {
            mainCamera.enabled = false;
            droneCamera.enabled = true;
        }
    }

    private void UpdateCameraPosition()
    {
        if (droneCamera != null)
        {
            // Keep the camera's y and z positions constant
            Vector3 cameraPosition = droneCamera.transform.position;
            cameraPosition.x = transform.position.x + cameraForward; // Adjust for left/right movement

            droneCamera.transform.position = cameraPosition;
            droneCamera.fieldOfView = droneCameraFOV;
        }
    }

    private void UpdateCameraRotation()
    {
        if ((!isPatrolling && !isReturning) || droneCamera == null) return;

        // Keep the camera's rotation fixed without y-axis tilt
        droneCamera.transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(startDelay);
        canStart = true;
        isPatrolling = true;
        Debug.Log("Patrol sequence starting");
    }

    private bool IsPointInBoundary(Vector3 point)
    {
        Vector3[] boundaryVertices = GetBoundaryVertices();
        int vertexCount = boundaryVertices.Length;
        bool inside = false;

        for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
        {
            Vector3 vertI = boundaryVertices[i];
            Vector3 vertJ = boundaryVertices[j];

            if (((vertI.z > point.z) != (vertJ.z > point.z)) &&
                (point.x < (vertJ.x - vertI.x) * (point.z - vertI.z) / (vertJ.z - vertI.z) + vertI.x))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private Vector3[] GetBoundaryVertices()
    {
        return new Vector3[] { vertex0, vertex1, vertex2, vertex3, vertex4, vertex5 };
    }

    private void ValidateSetup()
    {
        if (trailer == null)
        {
            Debug.LogError("Trailer reference is missing!");
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

        Destroy(line.GetComponent<Collider>());
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

        Destroy(point.GetComponent<Collider>());
        debugObjects.Add(point);
    }

    private void GeneratePatrolPoints()
    {
        List<Vector3> points = new List<Vector3>();
        float padding = 1f;
        Vector3[] boundaryVertices = GetBoundaryVertices();

        // Calculate bounding box
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

        // Generate points only within the boundary
        for (int i = 0; i < gridResolution; i++)
        {
            for (int j = 0; j < gridResolution; j++)
            {
                float x = minX + (i * stepX);
                float z = minZ + (j * stepZ);
                Vector3 point = new Vector3(x, FIXED_HEIGHT, z);

                // Only add points that are inside the boundary
                if (IsPointInBoundary(point))
                {
                    points.Add(point);
                }
            }
        }

        // Ensure we have at least some patrol points
        if (points.Count == 0)
        {
            Debug.LogWarning("No patrol points were generated within the boundary. Check boundary vertices and grid resolution.");
            // Add center point as fallback
            Vector3 centerPoint = new Vector3(
                (minX + maxX) / 2,
                FIXED_HEIGHT,
                (minZ + maxZ) / 2
            );
            points.Add(centerPoint);
        }

        patrolPoints = points.ToArray();
        Debug.Log($"Generated {patrolPoints.Length} patrol points within boundary");
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

    void Update()
    {
        if (canStart && !hasStarted)
        {
            hasStarted = true;
            Debug.Log("Starting guard movement");
        }

        if (isMainGuard)
        {
            CheckAlertStatus();
        }

        if (canStart && hasStarted && !missionComplete)
        {
            if (isAlertActive && bearTarget != null)
            {
                InterceptBear();
            }
            else if (isPatrolling)
            {
                Patrol();
            }
            else if (isReturning)
            {
                ReturnToTrailer();
            }

            if (droneCamera != null)
            {
                UpdateCameraPosition();
                UpdateCameraRotation();
            }
        }
    }
    
    private void CheckAlertStatus()
    {
        if (isAlertActive && !hasSpawnedGuards && isMainGuard)
        {
            StartCoroutine(InitiateAlertSequence());
            hasSpawnedGuards = true;
        }
        else if (!isAlertActive && hasSpawnedGuards && isMainGuard)
        {
            DespawnAdditionalGuards();
            hasSpawnedGuards = false;
            canInterceptBear = false;
            isReadyToIntercept = false;
        }
    }
    
private IEnumerator InitiateAlertSequence()
{
    Debug.Log("Alert sequence initiated, preparing guards...");
    
    // Spawn all guards at once
    for (int i = 0; i < numberOfGuardsToSpawn; i++)
    {
        Vector3 spawnPosition = transform.position + spawnOffsets[i];
        spawnPosition.y = FIXED_HEIGHT;

        GameObject newGuard = Instantiate(guardPrefab, spawnPosition, transform.rotation);
        SecurityGuardController guardController = newGuard.GetComponent<SecurityGuardController>();
        
        if (guardController != null)
        {
            // Basic setup
            guardController.trailer = this.trailer;
            guardController.mainCamera = this.mainCamera;
            guardController.droneCamera = this.droneCamera;
            guardController.speed = this.speed;
            guardController.interceptSpeed = this.interceptSpeed;
            guardController.bearTarget = this.bearTarget;
            guardController.isAlertActive = true;
            guardController.isMainGuard = false;
            
            // Important: Initialize the guard as already started
            guardController.canStart = true;
            guardController.hasStarted = true;
            guardController.startDelay = 0f; // Set delay to 0 for spawned guards
            
            // Enable the controller
            guardController.enabled = true;
            
            // Disable camera on spawned guards
            if (guardController.droneCamera != null)
                guardController.droneCamera.enabled = false;
                
            guardController.SetPatrolPoints(this.patrolPoints);
            guardController.SetCurrentPatrolIndex(this.currentPatrolIndex);
            guardController.SetSpawnOffset(spawnOffsets[i]);
            
            spawnedGuards.Add(newGuard);
        }
    }
    
    Debug.Log($"Spawned {numberOfGuardsToSpawn} guards simultaneously");
    
    // Enable interception immediately
    canInterceptBear = true;
    isReadyToIntercept = true;
    
    Debug.Log("Guards ready for interception!");
    yield return null;
}
    
    
    private void InterceptBear()
    {
        if (bearTarget == null) return;

        Vector3 bearPosition = bearTarget.transform.position;
        bearPosition.y = FIXED_HEIGHT;
        
        // Move towards bear
        transform.position = Vector3.MoveTowards(
            transform.position,
            bearPosition,
            interceptSpeed * Time.deltaTime
        );

        // Rotate to face the bear
        Vector3 directionToBear = (bearPosition - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(directionToBear);

        // Only check for catch if interception is allowed
        if (canInterceptBear && Vector3.Distance(transform.position, bearPosition) < interceptDistance)
        {
            CatchBear();
        }
    }
    
    private void CatchBear()
    {
        if (bearTarget != null)
        {
            Debug.Log("Bear intercepted by security guard!");
            
            // Only the main guard should handle the cleanup
            if (isMainGuard)
            {
                Destroy(bearTarget);
                isAlertActive = false;
                canInterceptBear = false;
                
                // Reset all guards to patrol
                foreach (GameObject guard in spawnedGuards)
                {
                    if (guard != null)
                    {
                        SecurityGuardController controller = guard.GetComponent<SecurityGuardController>();
                        if (controller != null)
                        {
                            controller.isAlertActive = false;
                        }
                    }
                }
            }
            
            // Individual guard behavior
            isPatrolling = true;
            currentPatrolIndex = FindNearestPatrolPoint();
        }
    }
    
    private void UpdateSpawnedGuardsForBearInterception()
    {
        if (bearTarget == null) return;

        foreach (GameObject guard in spawnedGuards)
        {
            if (guard != null)
            {
                SecurityGuardController guardController = guard.GetComponent<SecurityGuardController>();
                if (guardController != null)
                {
                    // Update the bear reference for each guard
                    guardController.bearTarget = this.bearTarget;
                }
            }
        }
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
    
    private void SpawnAdditionalGuards()
    {
        if (guardPrefab == null)
        {
            Debug.LogError("Guard prefab not assigned!");
            return;
        }

        for (int i = 0; i < numberOfGuardsToSpawn; i++)
        {
            Vector3 spawnPosition = transform.position + spawnOffsets[i];
            spawnPosition.y = FIXED_HEIGHT;

            GameObject newGuard = Instantiate(guardPrefab, spawnPosition, transform.rotation);
            SecurityGuardController guardController = newGuard.GetComponent<SecurityGuardController>();
            
            if (guardController != null)
            {
                // Set up the spawned guard
                guardController.trailer = this.trailer;
                guardController.mainCamera = this.mainCamera;
                guardController.droneCamera = this.droneCamera;
                guardController.speed = this.speed;
                guardController.interceptSpeed = this.interceptSpeed;
                guardController.bearTarget = this.bearTarget;
                guardController.isAlertActive = true;
                guardController.isMainGuard = false; // Mark as spawned guard
                guardController.SetPatrolPoints(this.patrolPoints);
                guardController.SetCurrentPatrolIndex(this.currentPatrolIndex);
                guardController.SetSpawnOffset(spawnOffsets[i]);
            }

            spawnedGuards.Add(newGuard);
        }

        Debug.Log($"Spawned {numberOfGuardsToSpawn} additional guards");
    }
    
    public void SetPatrolPoints(Vector3[] points)
    {
        patrolPoints = points;
    }
    
    public void SetCurrentPatrolIndex(int index)
    {
        currentPatrolIndex = index;
    }
    
    private Vector3 spawnOffset;
    public void SetSpawnOffset(Vector3 offset)
    {
        spawnOffset = offset;
    }
    
    private void UpdateSpawnedGuards()
    {
        foreach (GameObject guard in spawnedGuards)
        {
            if (guard != null)
            {
                // Calculate offset from main guard
                Vector3 currentOffset = guard.transform.position - transform.position;
                
                // Maintain the same relative position while following the main guard
                Vector3 targetPosition = transform.position + currentOffset;
                targetPosition.y = FIXED_HEIGHT;  // Maintain fixed height
                
                // Move towards the target position
                guard.transform.position = Vector3.MoveTowards(
                    guard.transform.position,
                    targetPosition,
                    speed * Time.deltaTime
                );
                
                // Match rotation with main guard
                guard.transform.rotation = transform.rotation;
            }
        }
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError("No patrol points available!");
            return;
        }

        Vector3 target = patrolPoints[currentPatrolIndex];
        
        // If this is a spawned guard (has a spawn offset), adjust the target position
        if (spawnOffset != Vector3.zero)
        {
            target += spawnOffset;
        }

        MoveTowardsTarget(target);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >= patrolPoints.Length)
            {
                Debug.Log("Patrol complete, returning to trailer");
                isPatrolling = false;
                isReturning = true;
            }
        }
    }
    
    private void DespawnAdditionalGuards()
    {
        foreach (GameObject guard in spawnedGuards)
        {
            if (guard != null)
            {
                Destroy(guard);
            }
        }
        spawnedGuards.Clear();
        Debug.Log("Despawned all additional guards");
    }

    private void ReturnToTrailer()
    {
        if (trailer == null)
        {
            Debug.LogError("Trailer is not assigned!");
            return;
        }

        Vector3 target = new Vector3(trailer.position.x, FIXED_HEIGHT, trailer.position.z);
        
        // If this is a spawned guard, adjust the return position based on spawn offset
        if (spawnOffset != Vector3.zero)
        {
            target += spawnOffset;
        }

        MoveTowardsTarget(target);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            Debug.Log("Returned to trailer, patrol mission complete");
            isReturning = false;
            missionComplete = true;
        }
    }

    private void MoveTowardsTarget(Vector3 target)
    {
        Vector3 newPosition = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        // Ensure the y position stays fixed
        newPosition.y = FIXED_HEIGHT;
        transform.position = newPosition;
    }

    void OnDestroy()
    {
        ClearDebugVisuals();
        DespawnAdditionalGuards();
    }

    public void ToggleDebugVisuals()
    {
        showDebugVisuals = !showDebugVisuals;
        InitializeDebugVisuals();
    }

    void OnValidate()
    {
        if (Application.isPlaying && debugContainer != null)
        {
            InitializeDebugVisuals();
        }
    }
}

