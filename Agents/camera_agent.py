import socket
import json
import random
import time
import logging
import csv

# Configurar sistema de logs
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(message)s')

class CameraAgent:
    def __init__(self, camera_id, server_ip="127.0.0.1", server_port=5005):
        self.camera_id = camera_id
        self.server_ip = server_ip
        self.server_port = server_port
        self.client_socket = None
        self.connected = False

        # Métricas de éxito
        self.total_alerts_sent = 0  # cuenta las alertas enviadas al servidor
        self.successful_alerts = 0  # cuenta las alertas exitosas procesadas por el dron

    def connect_to_server(self):
        # conecta la camara al servidor de comunicacion
        try:
            self.client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.client_socket.connect((self.server_ip, self.server_port))
            self.connected = True
        except Exception as e:
            logging.error(f"Error connecting camera: {e}")

    def simulate_detection(self):
        # genera detección aleatoria y manda los datos al servidor
        probability = round(random.uniform(0.7, 1.0), 2)
        coordinates = {"x": random.randint(0, 100), "y": random.randint(0, 100)}
        message = {
            "sender": f"camera_{self.camera_id}",
            "event_type": "movement_detected",
            "coordinates": coordinates,
            "probability": probability,
        }

        # enviar mensaje al servidor
        try:
            self.client_socket.send(json.dumps(message).encode('utf-8'))
            self.total_alerts_sent += 1
            self.save_metrics_to_csv()  # Guardar las métricas en CSV
        except Exception as e:
            logging.error(f"Error sending detection: {e}")
            self.connected = False

    def monitor_area(self):
        # ciclo para monitorear el área y enviar detecciones
        if not self.connected:
            self.connect_to_server()

        while self.connected:
            self.simulate_detection()
            time.sleep(5)  # espera 5 segundos entre detecciones

    def save_metrics_to_csv(self):
        """Guardar las métricas de éxito en un archivo CSV"""
        # Si no hay tiempo de respuesta (como para las cámaras), guardamos NaN
        response_time_value = 'NaN'  # Ya que no aplica para las cámaras, usaremos 'NaN'
        
        with open('metrics.csv', mode='a', newline='') as file:
            writer = csv.writer(file)
            writer.writerow([time.time(), 'camera', self.total_alerts_sent, self.successful_alerts, response_time_value])  # Usamos 'NaN' para tiempo de respuesta

if __name__ == "__main__":
    # crear objeto cámara y empezar a monitorear
    camera = CameraAgent(camera_id=1)
    camera.monitor_area()
