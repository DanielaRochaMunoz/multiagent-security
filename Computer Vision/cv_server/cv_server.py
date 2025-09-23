import cv2
import numpy as np
from flask import Flask, request, jsonify
import torch

app = Flask(__name__)

# Load YOLO model
model_path = "yolov5/runs/train/exp2/weights/best.pt"  # Path to the model
print("Loading YOLO model...")
model = torch.hub.load("ultralytics/yolov5", "custom", path=model_path, force_reload=True)
print("YOLO model loaded successfully.")

# Function to check if the bounding box contains pink color
def filter_pink(image, bbox):
    x1, y1, x2, y2 = [int(coord) for coord in bbox]
    cropped_image = image[y1:y2, x1:x2]

    # Convert to HSV color space
    hsv = cv2.cvtColor(cropped_image, cv2.COLOR_BGR2HSV)

    # HSV range for pink color
    lower_pink = np.array([130, 40, 40])
    upper_pink = np.array([180, 255, 255])

    # Mask for pink color
    mask = cv2.inRange(hsv, lower_pink, upper_pink)
    pink_pixels = cv2.countNonZero(mask)

    # Minimum threshold to consider the bear pink
    return pink_pixels > 50

@app.route("/stream/<camera_id>", methods=["POST"])
def detect(camera_id):
    try:
        # Decode the received image
        image_data = np.frombuffer(request.data, np.uint8)
        img = cv2.imdecode(image_data, cv2.IMREAD_COLOR)

        if img is None:
            return jsonify({"error": "Invalid image data"}), 400

        # Save frames (optional for debugging)
        cv2.imwrite(f"all_frames_{camera_id}.jpg", img)

        # Process with YOLO
        results = model(img)
        detections = results.xyxy[0].cpu().numpy()

        pink_detected = False
        bear_detected = False
        output = []

        for *bbox, conf, cls in detections:
            is_pink = filter_pink(img, bbox)
            if is_pink:
                pink_detected = True
            if model.names[int(cls)] == "bear" and conf > 0.5:
                bear_detected = True
                output.append({
                    "bbox": [float(coord) for coord in bbox],
                    "class": model.names[int(cls)],
                    "confidence": float(conf)
                })

        # Generate alerts
        if bear_detected and pink_detected:
            print(f"🚨 ALERT: Bear detected on {camera_id}.")
        elif pink_detected:
            print(f"🔍 Something suspicious was detected on {camera_id}.")

        return jsonify({"camera_id": camera_id, "detections": output})
    except Exception as e:
        print(f"Error on {camera_id}: {e}")
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    print("Server ready. Starting Flask...")
    app.run(host="0.0.0.0", port=8000)
