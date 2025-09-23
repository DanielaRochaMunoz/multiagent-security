import socket
import json
import time
import logging
import csv
from owlready2 import *  # Importar Owlready2 para trabajar con ontología

# Configurar sistema de logs
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(message)s')

# Cargar la ontología existente
onto = get_ontology("http://example.org/dron_security_system.owl")

class DroneAgent:
    def __init__(self, server_ip="127.0.0.1", server_port=5005):
        self.server_ip = server_ip
        self.server_port = server_port
        self.client_socket = None
        self.connected = False
        self.current_position = (0, 0)  # Posición inicial
        self.patrol_points = [(10, 10), (20, 20), (30, 30), (40, 40), (50, 50), 
                              (60, 60), (70, 70), (80, 80), (90, 90), (100, 100), 
                              (110, 110), (120, 120), (130, 130), (140, 140)]  # Puntos de patrullaje
        self.current_point_index = 0  # Punto de inicio en la ruta
        
        # Contadores para las alertas
        self.successful_alerts = 0  # Contador de alertas exitosas
        self.total_alerts_received = 0  # Contador de alertas recibidas
        self.total_patrols = 0  # Contador de patrullajes completados
        self.alerts_handled = 0  # Contador adicional para las alertas manejadas

    def connect_to_server(self):
        """Conecta al servidor principal"""
        attempts = 5
        while attempts > 0:
            try:
                self.client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.client_socket.settimeout(5)
                self.client_socket.connect((self.server_ip, self.server_port))
                self.connected = True
                self.send_message({"sender": "drone"})  # Enviar identificación
                break
            except Exception as e:
                attempts -= 1
                if attempts == 0:
                    self.connected = False
                else:
                    time.sleep(2)

    def send_message(self, message):
        """Enviar mensajes al servidor"""
        if self.connected:
            try:
                self.client_socket.send(json.dumps(message).encode('utf-8'))
            except Exception as e:
                self.connected = False  # Si el envío falla, marcaremos la conexión como no válida
                self.reconnect()  # Intentar reconectar

    def reconnect(self):
        """Intentar reconectar si la conexión se pierde"""
        self.connect_to_server()

    def takeoff(self):
        """Inicia el despegue del dron"""
        self.current_position = (0, 0)
        self.send_message({
            "sender": "drone",
            "event_type": "takeoff",
            "coordinates": self.current_position
        })

    def navigate(self):
        """Se mueve al siguiente punto de patrullaje"""
        target_point = self.patrol_points[self.current_point_index]
        self.move_to_point(target_point)
        self.current_point_index = (self.current_point_index + 1) % len(self.patrol_points)

    def move_to_point(self, target_point):
        """Simula el movimiento al punto destino"""
        self.current_position = target_point
        self.send_message({
            "sender": "drone",
            "event_type": "arrived_at_point",
            "coordinates": self.current_position
        })
        self.detect_deviation(target_point)
        self.generate_alert(target_point)

    def generate_alert(self, target_point):
        """Genera una alerta después de cada movimiento"""
        alert_message = {
            "sender": "drone",
            "event_type": "movement_detected",
            "coordinates": target_point,
            "probability": 0.9  # Simulando que la alerta tiene alta probabilidad
        }
        self.handle_alert(alert_message)

    def handle_alert(self, message):
        """Simula que el dron maneja una alerta"""
        if message.get("event_type") == "movement_detected":
            start_time = time.time()  # Inicia el conteo del tiempo de respuesta
            self.alerts_handled += 1  # Incrementa el contador de alertas manejadas
            
            self.successful_alerts += 1  # Aumenta el contador de alertas exitosas
            self.total_alerts_received += 1  # Aumenta el contador de alertas recibidas
            
            # Calcular el tiempo de respuesta
            end_time = time.time()  # Fin del procesamiento
            response_time = end_time - start_time  # Tiempo de respuesta calculado
            
            # Guarda el tiempo de respuesta en el archivo CSV
            self.save_metrics_to_csv(response_time)

    def detect_deviation(self, target_point):
        """Calcula si el dron se desvió de la ruta"""
        distance = self.calculate_distance(self.current_position, target_point)
        if distance > 5:
            self.correct_deviation(target_point)

    def calculate_distance(self, pos1, pos2):
        """Calcula la distancia entre dos puntos"""
        return ((pos2[0] - pos1[0])**2 + (pos2[1] - pos1[1])**2)**0.5  # Corregido el cálculo

    def correct_deviation(self, target_point):
        """Corrige la desviación regresando al punto objetivo"""
        self.move_to_point(target_point)

    def land(self):
        """Realiza el aterrizaje del dron"""
        self.current_position = (0, 0)
        self.send_message({
            "sender": "drone",
            "event_type": "land",
            "coordinates": self.current_position
        })

    def save_metrics_to_csv(self, response_time):
        """Guardar las métricas de desempeño en un archivo CSV"""
        with open('metrics.csv', mode='a', newline='') as file:
            writer = csv.writer(file)
            writer.writerow([time.time(), 'drone', self.total_alerts_received, self.successful_alerts, response_time])

    def run(self):
        """Ciclo principal de ejecución"""
        if not self.connected:
            self.connect_to_server()

        self.takeoff()  # Despegue inicial

        while self.connected:
            self.navigate()  # Patrullaje entre puntos
            time.sleep(5)

            # Termina el ciclo de patrullaje al completar la ruta
            if self.current_point_index == 0:
                self.land()
                self.total_patrols += 1  # Incrementar el contador de patrullajes completados
                break

if __name__ == "__main__":
    # Instancia y corre el dron
    drone = DroneAgent()
    drone.run()
