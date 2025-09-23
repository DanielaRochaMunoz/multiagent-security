import csv
import matplotlib.pyplot as plt
import pandas as pd
import numpy as np
from datetime import datetime

# Función para leer el archivo CSV y cargar los datos
def read_metrics_from_csv(filename='metrics.csv'):
    data = []
    with open(filename, mode='r') as file:
        reader = csv.reader(file)
        for row in reader:
            data.append(row)
    return data

# Función para procesar los datos
def process_data(data):
    # Convertimos los datos en un DataFrame para facilitar el manejo
    df = pd.DataFrame(data, columns=['timestamp', 'agent_type', 'total_alerts_received', 'successful_alerts', 'response_time'])

    # Filtrar filas con valores no válidos en 'timestamp'
    df = df[pd.to_numeric(df['timestamp'], errors='coerce').notnull()]

    # Convertimos el timestamp a formato legible
    df['timestamp'] = pd.to_datetime(pd.to_numeric(df['timestamp']), unit='s', errors='coerce')

    # Aseguramos que los valores numéricos sean del tipo correcto
    df['total_alerts_received'] = pd.to_numeric(df['total_alerts_received'], errors='coerce').fillna(0)
    df['successful_alerts'] = pd.to_numeric(df['successful_alerts'], errors='coerce').fillna(0)
    
    # Para response_time, si es "N/A" lo reemplazamos con NaN y después lo convertimos a tipo numérico
    df['response_time'] = pd.to_numeric(df['response_time'], errors='coerce').fillna(0)  # Convertir "N/A" a 0

    # Eliminamos las filas duplicadas basadas en 'timestamp' y 'agent_type'
    df = df.drop_duplicates(subset=['timestamp', 'agent_type'])

    # Cálculos acumulativos para las alertas
    df['total_alerts_received'] = df.groupby('agent_type')['total_alerts_received'].cumsum()
    df['successful_alerts'] = df.groupby('agent_type')['successful_alerts'].cumsum()

    return df

# Función para generar las gráficas
def generate_graphs(df):
    # Colores y estilos para los tipos de agentes
    agent_styles = {
        'camera': {'color': 'blue', 'linestyle': '--', 'marker': 'o'},
        'drone': {'color': 'green', 'linestyle': '-', 'marker': 's'},
        'security': {'color': 'orange', 'linestyle': ':', 'marker': '^'}
    }

    # 1. Gráfica de alertas recibidas vs tiempo
    plt.figure(figsize=(12, 6))
    for agent_type, style in agent_styles.items():
        agent_data = df[df['agent_type'] == agent_type]
        plt.plot(agent_data['timestamp'], agent_data['total_alerts_received'], 
                 label=f"{agent_type} - Alerts Received", 
                 color=style['color'], linestyle=style['linestyle'], marker=style['marker'])

    plt.title('Total Alerts Received Over Time', fontsize=14)
    plt.xlabel('Time', fontsize=12)
    plt.ylabel('Total Alerts Received', fontsize=12)
    plt.legend(loc='upper left', bbox_to_anchor=(1, 1), fontsize=10)  # Leyenda fuera del área del gráfico
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.savefig('alerts_received.png')
    plt.show()

    # 2. Gráfica de alertas exitosas vs tiempo
    plt.figure(figsize=(12, 6))
    for agent_type, style in agent_styles.items():
        agent_data = df[df['agent_type'] == agent_type]
        plt.plot(agent_data['timestamp'], agent_data['successful_alerts'], 
                 label=f"{agent_type} - Successful Alerts", 
                 color=style['color'], linestyle=style['linestyle'], marker=style['marker'])

    plt.title('Successful Alerts Over Time', fontsize=14)
    plt.xlabel('Time', fontsize=12)
    plt.ylabel('Successful Alerts', fontsize=12)
    plt.legend(loc='upper left', bbox_to_anchor=(1, 1), fontsize=10)  # Leyenda fuera del área del gráfico
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.savefig('successful_alerts.png')
    plt.show()

    # 3. Gráfica de tiempos de respuesta por tiempo (Escala logarítmica)
    plt.figure(figsize=(12, 6))
    for agent_type, style in agent_styles.items():
        agent_data = df[df['agent_type'] == agent_type]
        # Aseguramos que las cámaras con response_time=0 se incluyan como línea horizontal
        if agent_type == 'camera':
            agent_data['response_time'] = agent_data['response_time'].replace(0, np.nan)  # Reemplazar 0 por NaN para evitar errores
            agent_data['response_time'] = agent_data['response_time'].fillna(0.0001)  # Añadir un valor mínimo para graficar
        else:
            agent_data = agent_data.dropna(subset=['response_time'])  # Filtrar valores NaN para otros agentes

        plt.plot(agent_data['timestamp'], agent_data['response_time'], 
                 label=f"{agent_type} - Response Time", 
                 color=style['color'], linestyle=style['linestyle'], marker=style['marker'])

    plt.title('Response Time Over Time (Log Scale)', fontsize=14)
    plt.xlabel('Time', fontsize=12)
    plt.ylabel('Response Time (seconds)', fontsize=12)
    plt.yscale('log')  # Cambiar a escala logarítmica
    plt.legend(loc='upper left', bbox_to_anchor=(1, 1), fontsize=10)  # Leyenda fuera del área del gráfico
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.savefig('response_time.png')
    plt.show()

# Función principal para generar las gráficas
def main():
    # Leer y procesar los datos
    data = read_metrics_from_csv('metrics.csv')
    df = process_data(data)
    
    # Generar las gráficas
    generate_graphs(df)

if __name__ == "__main__":
    main()
