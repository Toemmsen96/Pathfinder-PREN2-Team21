#!/usr/bin/env python3
import sys
import os
import argparse
import json
from ultralytics import YOLO
from pathlib import Path
import uuid

# Global model variable
model = None

def load_model(model_path='models/pren_det_v3.onnx'):
    global model
    try:
        model = YOLO(model_path, task='detect')
        return True
    except Exception as e:
        print(f'Error loading model: {e}', file=sys.stderr)
        return False

def detect(image_path, conf=0.25, output_path='', classes='', no_draw=False, save_json=False):
    global model
    
    if model is None:
        raise Exception('Model not loaded')
    
    # Parse classes if provided
    class_list = None
    if classes:
        try:
            class_list = [int(c) for c in classes.split(',')]
        except ValueError:
            raise Exception('Classes must be comma-separated integers')
    
    # Create output directory if needed
    if output_path and not os.path.exists(output_path):
        os.makedirs(output_path)
    
    # Run detection
    results = model(
        source=image_path,
        conf=conf,
        classes=class_list,
        save=not no_draw,
        save_txt=False,
        save_conf=True,
        project=output_path if output_path else 'output',
        name='',
        exist_ok=True
    )
    
    # Process results
    all_detections = []
    for r in results:
        detections = []
        if r.boxes is not None:
            boxes = r.boxes
            
            for box in boxes:
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                conf = float(box.conf[0])
                cls = int(box.cls[0])
                cls_name = model.names[cls]
                
                detection = {
                    'bounding_box': {
                        'left': int(x1),
                        'top': int(y1),
                        'right': int(x2),
                        'bottom': int(y2)
                    },
                    'confidence': conf,
                    'class_name': cls_name,
                    'class_id': cls,
                    'detection_id': str(uuid.uuid4())
                }
                
                detections.append(detection)
        
        all_detections.extend(detections)
        
        # Save JSON if requested
        if save_json and output_path:
            filename = Path(r.path).stem
            json_output = {'detections': detections}
            json_path = os.path.join(output_path, f'{filename}_detection.json')
            with open(json_path, 'w') as f:
                json.dump(json_output, f, indent=2)
    
    return {'detections': all_detections}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--test', action='store_true', help='Test model loading')
    parser.add_argument('--model', default='models/pren_det_v3.onnx', help='Model path')
    parser.add_argument('--image', help='Image path')
    parser.add_argument('--conf', type=float, default=0.25, help='Confidence threshold')
    parser.add_argument('--output', default='', help='Output path')
    parser.add_argument('--classes', default='', help='Classes filter')
    parser.add_argument('--no-draw', action='store_true', help='No drawing')
    parser.add_argument('--json', action='store_true', help='Save JSON')
    
    args = parser.parse_args()
    
    # Load model
    if not load_model(args.model):
        sys.exit(1)
    
    if args.test:
        print('Model loaded successfully')
        sys.exit(0)
    
    if not args.image:
        print('Error: --image is required', file=sys.stderr)
        sys.exit(1)
    
    try:
        result = detect(args.image, args.conf, args.output, args.classes, args.no_draw, args.json)
        print(json.dumps(result))
    except Exception as e:
        print(f'Error: {e}', file=sys.stderr)
        sys.exit(1)

if __name__ == '__main__':
    main()
