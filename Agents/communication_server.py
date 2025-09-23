import socket
import json
import threading
import logging
from owlready2 import get_ontology, Thing, FunctionalProperty
import csv
import time

# Configuración de logs para seguimiento del sistema
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(message)s')

class CommunicationServer:
    def __init__(self, host="127.0.0.1", port=5005):
        self.host = host
        self.port = port
        self.server = None
        self.clients = {}  # Almacena las conexiones de cámaras, drones y seguridad
        self.running = False
        self.drone_message_buffer = []  # Mensajes pendientes para el dron
        self.message_queue = []  # Mensajes pendientes para personal de seguridad

        # Inicializa la ontología para eventos del sistema
        self.ontology = self.initialize_ontology()

        # Métricas de éxito
        self.total_alerts_received = 0  # Cuenta las alertas recibidas
        self.successful_alerts = 0  # Cuenta las alertas exitosas

    def initialize_ontology(self):
        # Define la ontología para el sistema de seguridad
        onto = get_ontology("http://example.org/drone_security.owl")
        with onto:
            class Entidad(Thing):  # Clase base
                pass

            class Camara(Entidad):  # Cámaras en la red
                pass

            class Dron(Entidad):  # Drones registrados
                pass

            class PersonalSeguridad(Entidad):  # Personal de seguridad
                pass

            class detecta_movimiento(Camara >> str, FunctionalProperty):  # Relación cámara-evento
                pass

            class recibe_alerta(PersonalSeguridad >> str, FunctionalProperty):  # Relación seguridad-evento
                pass

            class responde_a_alerta(Dron >> str, FunctionalProperty):  # Relación dron-respuesta
                pass

        # Guarda ontología en formato RDF
        onto.save(file="drone_security.owl", format="rdfxml")
        logging.info("Ontology initialized and saved.")
        return onto

    def register_event_in_ontology(self, sender, event_type, details):
        # Registra eventos en la ontología según el origen
        with self.ontology:
            if sender.startswith("camera_"):
                camera_instance = self.ontology.Camara(sender)
                camera_instance.detecta_movimiento = str(details)
            elif sender == "security_personnel":
                security_instance = self.ontology.PersonalSeguridad(sender)
                security_instance.recibe_alerta = str(details)
            elif sender == "drone":
                drone_instance = self.ontology.Dron(sender)
                drone_instance.responde_a_alerta = str(details)

    def start_server(self):
        # Inicia el servidor para aceptar conexiones
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.bind((self.host, self.port))
        self.server.listen(5)
        self.running = True
        logging.info(f"Server running on {self.host}:{self.port}...")

        threading.Thread(target=self.accept_clients).start()

    def accept_clients(self):
        # Acepta nuevas conexiones de clientes
        while self.running:
            try:
                client_socket, client_address = self.server.accept()
                logging.info(f"New connection from {client_address}")

                # Identifica el tipo de cliente conectado
                client_type = self.identify_client(client_socket)
                if client_type:
                    self.clients[client_type] = client_socket
                    threading.Thread(target=self.handle_client, args=(client_socket, client_type)).start()
                else:
                    client_socket.close()

            except Exception as e:
                logging.error(f"Error accepting client: {e}")

    def identify_client(self, client_socket):
        # Obtiene el tipo de cliente desde el mensaje inicial
        try:
            data = client_socket.recv(1024).decode('utf-8')
            if data:
                message = json.loads(data)
                return message.get("sender")
        except Exception as e:
            logging.error(f"Error identifying client: {e}")
        return None

    def handle_client(self, client_socket, client_type):
        # Gestiona mensajes del cliente conectado
        while self.running:
            try:
                data = client_socket.recv(1024).decode('utf-8')
                if data:
                    message = json.loads(data)

                    # Registra el evento en la ontología
                    self.register_event_in_ontology(
                        sender=message.get("sender"),
                        event_type=message.get("event_type"),
                        details=message.get("coordinates")
                    )

                    # Incrementar contador de alertas recibidas
                    if message.get("event_type") == "movement_detected":
                        self.total_alerts_received += 1

                    # Reenvía mensaje según su origen
                    if client_type.startswith("camera_"):
                        self.forward_to_security(message)
                    elif client_type == "security_personnel":
                        self.forward_to_drone(message)

            except Exception as e:
                logging.error(f"Error handling client ({client_type}): {e}")
                break

        client_socket.close()

    def forward_to_security(self, message):
        # Reenvía mensajes de cámaras al personal de seguridad
        security_socket = self.clients.get("security_personnel")
        if security_socket:
            try:
                security_socket.send(json.dumps(message).encode('utf-8'))
            except Exception as e:
                logging.error(f"Error forwarding message to security personnel: {e}")

    def forward_to_drone(self, message):
        # Reenvía mensajes del personal de seguridad al dron
        drone_socket = self.clients.get("drone")
        if drone_socket:
            try:
                drone_socket.send(json.dumps(message).encode('utf-8'))
                if message.get("event_type") == "alert_responded":
                    self.successful_alerts += 1
                    self.save_metrics_to_csv()
            except Exception as e:
                logging.error(f"Error forwarding message to drone: {e}")

    def save_metrics_to_csv(self):
        # Guardar métricas en archivo CSV
        with open('metrics.csv', mode='a', newline='') as file:
            writer = csv.writer(file)
            writer.writerow([time.time(), 'communication_server', self.total_alerts_received, self.successful_alerts, 'N/A'])

if __name__ == "__main__":
    comm_server = CommunicationServer()
    comm_server.start_server()
