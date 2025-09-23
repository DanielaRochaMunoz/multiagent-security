using UnityEngine;

public class InvestigationController : MonoBehaviour
{
    [SerializeField] private DroneController droneController;

    void Update()
    {
        // Activar el modo de investigación manualmente con la tecla "I"
        if (Input.GetKeyDown(KeyCode.I))
        {
            Vector3 manualTarget = new Vector3(10, 10, 10); // Cambia las coordenadas al objetivo deseado
            droneController.StartInvestigation(manualTarget);
            Debug.Log("Investigación manual activada.");
        }

        // Activar la alerta manualmente con la tecla "A"
        if (Input.GetKeyDown(KeyCode.A))
        {
            droneController.ResumePatrol(); // Esto reinicia el patrullaje después de la alerta
            Debug.Log("Alerta manual activada.");
        }
    }
}
