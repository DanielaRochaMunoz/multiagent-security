import os

# Ruta donde tienes todas las imágenes (temporalmente)
images_path = "Dataset/temp_images"  # Cambia esto a la carpeta donde están tus imágenes

# Renombrar todas las imágenes
for index, file_name in enumerate(os.listdir(images_path)):
    if file_name.endswith((".png", ".jpg", ".jpeg")):  # Asegúrate de procesar solo imágenes
        new_name = f"oso_{index+1}.png"  # Cambia a ".jpg" si es necesario
        old_path = os.path.join(images_path, file_name)
        new_path = os.path.join(images_path, new_name)
        os.rename(old_path, new_path)

print(f"Imágenes renombradas en {images_path}")
