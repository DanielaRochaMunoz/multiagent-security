using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class YoloDetection : MonoBehaviour
{
    private string serverUrl = "http://127.0.0.1:8000/stream"; // URL del servidor Flask
    public Camera[] cameras; // Cámaras para capturar frames
    public float frameInterval = 0.5f; // Intervalo entre envíos en segundos
    public int renderTextureResolution = 512; // Resolución del RenderTexture

    private void Start()
    {
        if (cameras == null || cameras.Length == 0)
        {
            Debug.LogError("No se asignaron cámaras al script.");
            return;
        }

        Debug.Log("Iniciando envío automático de frames para múltiples cámaras...");
        StartCoroutine(AutoSendFrames());
    }

    private IEnumerator AutoSendFrames()
    {
        while (true)
        {
            foreach (Camera cam in cameras)
            {
                if (cam != null)
                {
                    StartCoroutine(CaptureAndSendFrame(cam));
                }
            }
            yield return new WaitForSeconds(frameInterval);
        }
    }

    private IEnumerator CaptureAndSendFrame(Camera camera)
    {
        RenderTexture renderTexture = new RenderTexture(renderTextureResolution, renderTextureResolution, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        camera.Render();

        Texture2D capturedTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        capturedTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        capturedTexture.Apply();

        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        byte[] imageData = capturedTexture.EncodeToJPG(100); // Calidad máxima
        Destroy(capturedTexture);

        using (UnityWebRequest request = new UnityWebRequest($"{serverUrl}/{camera.name}", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(imageData);
            request.uploadHandler.contentType = "application/octet-stream";
            request.downloadHandler = new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;

                try
                {
                    AlertResponse response = JsonUtility.FromJson<AlertResponse>(responseText);

                    if (response.alert)
                    {
                        Debug.Log($"🚨 Alerta recibida del servidor para {camera.name}. Activando investigación...");
                        ActivateInvestigation(camera.transform.position);
                    }
                }
                catch
                {
                    Debug.LogError("Error al procesar la respuesta del servidor.");
                }
            }
            else
            {
                Debug.LogError($"Error al enviar el frame desde {camera.name}: {request.error}");
            }
        }
    }

    private void ActivateInvestigation(Vector3 targetPosition)
    {
        DroneController drone = FindObjectOfType<DroneController>();
        if (drone != null)
        {
            Debug.Log("Iniciando investigación con el dron.");
            drone.StartInvestigation(targetPosition);
        }
        else
        {
            Debug.LogError("No se encontró un DroneController en la escena.");
        }
    }

    [System.Serializable]
    public class AlertResponse
    {
        public string camera_id;
        public bool alert;
    }
}
