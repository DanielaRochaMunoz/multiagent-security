using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BearController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Camera droneCamera;

    [Header("Movement Settings")]
    public int gridResolution = 20;
    public float speed = 3f;//
    public float startDelay = 11f;
    private bool missionComplete = false;
    private const float FIXED_HEIGHT = 4.9f; // New constant for fixed height

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

    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;
    private bool isPatrolling = false;
    private List<GameObject> debugObjects = new List<GameObject>();
    private GameObject debugContainer;
    private bool canStart = false;
    private bool hasStarted = false;


    void Start()
    {
        ValidateSetup();

        // Layer handling
        int droneLayer = 9;
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = droneLayer;
        }

        if (droneCamera != null)
        {
            droneCamera.cullingMask &= ~(1 << droneLayer);
        }

        GeneratePatrolPoints();

        // Set initial position to the first patrol point
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            transform.position = new Vector3(patrolPoints[0].x, FIXED_HEIGHT, patrolPoints[0].z);
            Debug.Log($"Initial drone position: {transform.position}");
        }

        InitializeDebugVisuals();
        StartCoroutine(DelayedStart());
        InitializeCameras();
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
        if (!isPatrolling || droneCamera == null) return;

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
            Debug.Log("Starting drone movement");
        }

        if (canStart && hasStarted && !missionComplete)
        {
            if (isPatrolling)
            {
                Patrol();
            }

            if (droneCamera != null)
            {
                UpdateCameraPosition();
                UpdateCameraRotation();
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
        MoveTowardsTarget(target);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >= patrolPoints.Length)
            {
                Debug.Log("Patrol complete, maintaining position");
                isPatrolling = false;
                missionComplete = true;
            }
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
