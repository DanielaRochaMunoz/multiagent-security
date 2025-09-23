import os
import cv2

# Configura las rutas de las imágenes y las etiquetas
images_path = "Dataset/images/val"  # Cambia esto a 'val' si quieres trabajar con validación
labels_path = "Dataset/labels/val"  # Cambia esto a 'val' si quieres trabajar con validación

# Asegúrate de que las carpetas de labels existen
os.makedirs(labels_path, exist_ok=True)

# Clase del objeto (oso)
class_id = 0

def create_label(image_name):
    # Ruta de la imagen
    image_path = os.path.join(images_path, image_name)

    # Leer la imagen
    image = cv2.imread(image_path)
    if image is None:
        print(f"Error: no se pudo leer la imagen {image_name}")
        return

    height, width, _ = image.shape

    # Crear un bounding box en el centro de la imagen (como ejemplo)
    center_x = 0.5
    center_y = 0.5
    bbox_width = 0.3
    bbox_height = 0.4

    # Crear archivo de etiqueta
    label_name = os.path.splitext(image_name)[0] + ".txt"
    label_path = os.path.join(labels_path, label_name)

    with open(label_path, "w") as label_file:
        label_file.write(f"{class_id} {center_x} {center_y} {bbox_width} {bbox_height}\n")

    print(f"Etiqueta creada: {label_path}")

# Procesar todas las imágenes en la carpeta
for image_name in os.listdir(images_path):
    if image_name.endswith((".png", ".jpg", ".jpeg")):
        create_label(image_name)
