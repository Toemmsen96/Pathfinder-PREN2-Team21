import sys
import os
import argparse
import numpy as np
from ultralytics import YOLO # type: ignore
import json
from pathlib import Path
import uuid

#!/usr/bin/env python3

def parse_arguments():
    parser = argparse.ArgumentParser(description='YOLO object detection')
    parser.add_argument('--model', type=str, default='models/prenv2.onnx', help='Path to YOLO model')
    parser.add_argument('--image', type=str, required=True, help='Path to input image or directory')
    parser.add_argument('--conf', type=float, default=0.25, help='Confidence threshold')
    parser.add_argument('--output', type=str, default='', help='Path to save output images')
    parser.add_argument('--classes', type=str, default='', help='Filter by classes, comma separated')
    parser.add_argument('--no-draw', action='store_true', help='Skip drawing on images, only output JSON')
    parser.add_argument('--json', action='store_true', help='Output detection results as JSON')
    parser.add_argument('--verbose', action='store_true', help='Enable verbose output')
    return parser.parse_args()

def main():
    args = parse_arguments()
    
    # Check if image path exists (can be file or directory)
    if not os.path.exists(args.image):
        print(f"Error: Path not found: {args.image}")
        return 1
    
    # Load YOLO model
    try:
        model = YOLO(args.model, task='detect')
        print(f"Model loaded: {args.model}")
    except Exception as e:
        print(f"Error loading model: {e}")
        return 1
    
    # Parse classes if provided
    classes = None
    if args.classes:
        try:
            classes = [int(c) for c in args.classes.split(',')]
            print(f"Filtering by classes: {classes}")
        except ValueError:
            print("Error: Classes must be comma-separated integers")
            return 1
    
    # Create output directory if it doesn't exist
    output_dir = args.output if args.output else 'output'
    if not os.path.exists(output_dir) and (args.output or args.json):
        os.makedirs(output_dir)
        print(f"Created output directory: {output_dir}")
    
    # Perform detection
    try:
        # Will handle both single image and directory
        print(f"Running detection on: {args.image}")
        results = model(
            source=args.image,
            conf=args.conf,
            classes=classes,
            save=not args.no_draw,  # Save images unless no-draw is specified
            save_txt=False,
            save_conf=True,
            project=output_dir,
            name='',
            exist_ok=True
        )
        print(f"Detected {len(results)} objects")
        
        # Process results
        for i, r in enumerate(results):
            # Get the input file path from the result object
            input_path = r.path
            filename = Path(input_path).stem
            
            # Process individual detections
            boxes = r.boxes
            detections = []
            
            for j, box in enumerate(boxes):
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                conf = float(box.conf[0])
                cls = int(box.cls[0])
                cls_name = model.names[cls]
                
                # Format detection to match C# structure
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
                    'detection_id': str(uuid.uuid4())  # Generate a UUID like Guid.NewGuid() in C#
                }
                
                detections.append(detection)
                print(f"{cls_name},{conf:.2f},{int(x1)},{int(y1)},{int(x2)},{int(y2)}")
            
            # Save JSON output if requested
            if args.json:
                json_output = {
                    'detections': detections
                }
                
                json_path = os.path.join(output_dir, f"{filename}_detection.json")
                with open(json_path, 'w') as f:
                    json.dump(json_output, f, indent=2)
                print(f"JSON output saved to: {json_path}")
    
    except Exception as e:
        print(f"Error during detection: {e}")
        import traceback
        traceback.print_exc()
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main())