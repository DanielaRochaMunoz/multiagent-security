import socket
import json
import logging
import csv
from owlready2 import *  # Importar Owlready2 para trabajar con ontología
import time

# Configurar sistema de logs
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(message)s')

# Cargar la ontología existente
onto = get_ontology("http://example.org/dron_security_system.owl")

class SecurityPersonnel:
    def __init__(self, server_ip="127.0.0.1", server_port=5005):
        self.server_ip = server_ip
        self.server_port = server_port
        self.client_socket = None
        self.connected = False
        self.successful_alerts = 0  # Contador de alertas exitosas
        self.total_alerts_received = 0  # Contador de alertas recibidas

    def connect_to_server(self):
        # Conecta al servidor de comunicación
        try:
            self.client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.client_socket.connect((self.server_ip, self.server_port))
            self.connected = True
            logging.info("Security Personnel connected to server.")
            # Enviar identificación al servidor
            self.send_message({"sender": "security_personnel"})
            logging.info("Security Personnel identification message sent to server.")
        except Exception as e:
            logging.error(f"Failed to connect Security Personnel to server: {e}")
            self.connected = False

    def send_message(self, message):
        # Envía mensaje al servidor
        if self.connected:
            try:
                self.client_socket.send(json.dumps(message).encode('utf-8'))
                logging.info(f"Security Personnel sent message: {message}")
            except Exception as e:
                logging.error(f"Error sending message from Security Personnel: {e}")

    def receive_message(self):
        # Recibe mensajes del servidor (alertas de cámaras)
        if self.connected:
            try:
                data = self.client_socket.recv(1024).decode('utf-8')
                if data:
                    return json.loads(data)
            except Exception as e:
                logging.error(f"Error receiving message: {e}")
        return None

    def evaluate_alert(self, message):
        # Evalúa la alerta y mide el tiempo de respuesta
        start_time = time.time()  # Inicio del procesamiento
        probability = message.get("probability", 0)
        coordinates = message.get("coordinates", {})

        logging.info(f"Evaluating alert with probability {probability} at coordinates {coordinates}")

        if probability >= 0.8:
            logging.info("High alert detected. Taking control of the drone.")
            # Registrar evento en ontología
            with onto:
                class HighAlert(Thing):
                    pass

                alert_instance = HighAlert(f"Alert_{coordinates['x']}_{coordinates['y']}")
                alert_instance.has_location = coordinates
                alert_instance.has_probability = probability
                alert_instance.has_control = True

            # Enviar comando de control manual al dron
            self.send_message({
                "sender": "security_personnel",
                "event_type": "manual_control",
                "coordinates": coordinates
            })

            # Incrementar alertas exitosas
            self.successful_alerts += 1
            logging.info(f"Successful alerts handled: {self.successful_alerts}")
        else:
            logging.info("Minor alert. No action taken.")

        end_time = time.time()  # Fin del procesamiento
        response_time = end_time - start_time  # Tiempo de respuesta
        self.save_metrics_to_csv(response_time)

    def handle_drone_event(self, message):
        # Maneja eventos del dron (despegue, llegada a punto, aterrizaje)
        event_type = message.get("event_type")
        coordinates = message.get("coordinates")
        
        if event_type == "takeoff":
            logging.info(f"Drone took off at {coordinates}. Monitoring...")
            with onto:
                class Takeoff(Thing):
                    pass
                takeoff_instance = Takeoff(f"Takeoff_{coordinates['x']}_{coordinates['y']}")
                takeoff_instance.has_location = coordinates
                logging.info(f"Takeoff event recorded for drone at {coordinates}")
        
        elif event_type == "arrived_at_point":
            logging.info(f"Drone arrived at point {coordinates}. Monitoring...")
            with onto:
                class Arrival(Thing):
                    pass
                arrival_instance = Arrival(f"Arrival_{coordinates['x']}_{coordinates['y']}")
                arrival_instance.has_location = coordinates
                logging.info(f"Arrival event recorded for drone at {coordinates}")
        
        elif event_type == "land":
            logging.info(f"Drone landed at {coordinates}. Monitoring...")
            with onto:
                class Landing(Thing):
                    pass
                landing_instance = Landing(f"Landing_{coordinates['x']}_{coordinates['y']}")
                landing_instance.has_location = coordinates
                logging.info(f"Landing event recorded for drone at {coordinates}")

    def save_metrics_to_csv(self, response_time):
        """Guardar las métricas de desempeño en un archivo CSV"""
        with open('metrics.csv', mode='a', newline='') as file:
            writer = csv.writer(file)
            writer.writerow([time.time(), 'security', self.total_alerts_received, self.successful_alerts, response_time])
            logging.info("Metrics saved to metrics.csv")

    def run(self):
        # Ciclo principal del personal de seguridad
        if not self.connected:
            self.connect_to_server()

        while self.connected:
            message = self.receive_message()
            if message:
                event_type = message.get("event_type")
                coordinates = message.get("coordinates")

                if event_type == "movement_detected":
                    logging.info("Security Personnel received alert. Evaluating...")
                    self.total_alerts_received += 1
                    logging.info(f"Total alerts received: {self.total_alerts_received}")
                    self.evaluate_alert(message)

                # manejar eventos del dron
                elif event_type in ["takeoff", "arrived_at_point", "land"]:
                    self.handle_drone_event(message)

        # Cerrar conexión al terminar
        self.client_socket.close()

# Ejecución principal
if __name__ == "__main__":
    security_personnel = SecurityPersonnel()
    security_personnel.run()
