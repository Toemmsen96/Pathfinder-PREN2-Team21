using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YoloService.Services
{
    public class PythonScriptManager
    {
        private readonly string _scriptPath;

        public PythonScriptManager()
        {
            _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detect_service.py");
        }

        public string ScriptPath => _scriptPath;

        public async Task CreateDetectionScriptAsync()
        {
            var scriptContent = @"#!/usr/bin/env python3
import sys
import os
import argparse
import json
import traceback
from pathlib import Path
import uuid

try:
    from ultralytics import YOLO
except ImportError as e:
    print(f'Failed to import ultralytics: {e}', file=sys.stderr)
    print('Please ensure ultralytics is installed in the virtual environment', file=sys.stderr)
    sys.exit(1)

try:
    import onnxruntime
except ImportError as e:
    print(f'Failed to import onnxruntime: {e}', file=sys.stderr)
    print('Please ensure onnxruntime is installed in the virtual environment', file=sys.stderr)
    sys.exit(1)

# Global model variable
model = None

def load_model(model_path='models/prendet_v4.onnx'):
    global model
    try:
        if not os.path.exists(model_path):
            raise FileNotFoundError(f'Model file not found: {model_path}')
        
        print(f'Loading model from: {model_path}', file=sys.stderr)
        model = YOLO(model_path, task='detect')
        print(f'Model loaded successfully', file=sys.stderr)
        return True
    except Exception as e:
        print(f'Error loading model: {e}', file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return False

def detect(image_path, conf=0.25, output_path='', classes='', no_draw=False, save_json=False):
    global model
    
    if model is None:
        raise Exception('Model not loaded')
    
    if not os.path.exists(image_path):
        raise Exception(f'Image file not found: {image_path}')
    
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
                cls_name = model.names[cls] if cls in model.names else f'class_{cls}'
                
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
    
    return {'status': 'successful' if all_detections else 'failed', 'detections': all_detections, 'count': len(all_detections)}

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
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)

if __name__ == '__main__':
    main()
";

            await File.WriteAllTextAsync(_scriptPath, scriptContent);

            // Make script executable on Linux
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var chmodProcess = Process.Start("chmod", $"+x \"{_scriptPath}\"");
                if (chmodProcess != null)
                {
                    await chmodProcess.WaitForExitAsync();
                }
            }
        }

        public async Task CreateDetectionServerScriptAsync(string modelPath = "models/prendet_v4.onnx")
        {
            var serverScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detect_server.py");
            var scriptContent = @"#!/usr/bin/env python3
import sys
import os
import json
import traceback
from pathlib import Path
import uuid
import threading
import time
import contextlib
import io

# Suppress all warnings and output from imports
import warnings
warnings.filterwarnings('ignore')

# Redirect stdout during imports to catch any stray output
with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
    try:
        from ultralytics import YOLO
    except ImportError as e:
        print(f'Failed to import ultralytics: {e}', file=sys.stderr)
        sys.exit(1)

# Global model variable
model = None
model_lock = threading.Lock()

def load_model(model_path='models/pren_det_v3.onnx'):
    global model
    try:
        if not os.path.exists(model_path):
            raise FileNotFoundError(f'Model file not found: {model_path}')
        
        print(f'Loading model from: {model_path}', file=sys.stderr)
        
        # Suppress all output during model loading
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            model = YOLO(model_path, task='detect')
        
        print(f'Model loaded successfully', file=sys.stderr)
        return True
    except Exception as e:
        print(f'Error loading model: {e}', file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return False

def detect(image_path, conf=0.25, output_path='', classes='', no_draw=False, save_json=False):
    global model
    
    with model_lock:
        if model is None:
            raise Exception('Model not loaded')
        
        if not os.path.exists(image_path):
            raise Exception(f'Image file not found: {image_path}')
        
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
        
        # Suppress all YOLO output by redirecting stdout temporarily
        import contextlib
        import io
        
        with contextlib.redirect_stdout(io.StringIO()):
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
                exist_ok=True,
                verbose=False
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
                    cls_name = model.names[cls] if cls in model.names else f'class_{cls}'
                    
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
        
        return {'status': 'successful' if all_detections else 'failed', 'detections': all_detections, 'count': len(all_detections)}

def main():
    # Load model at startup
    if not load_model():
        sys.exit(1)
    
    # Ensure stdout is clean before signaling ready
    sys.stdout.flush()
    print('READY', file=sys.stderr, flush=True)  # Signal that server is ready
    
    # Process commands from stdin
    for line in sys.stdin:
        try:
            line = line.strip()
            if not line:
                continue
                
            if line == 'PING':
                print(json.dumps({'status': 'pong'}), flush=True)
                continue
                
            if line == 'EXIT':
                break

            if line == 'READY':
                print(json.dumps({'status': 'ready'}), flush=True)
                continue
            
            # Parse detection request
            request = json.loads(line)
            
            result = detect(
                request['image_path'],
                request.get('conf', 0.25),
                request.get('output_path', ''),
                request.get('classes', ''),
                request.get('no_draw', False),
                request.get('save_json', False)
            )
            
            print(json.dumps(result), flush=True)
            
        except Exception as e:
            error_result = {
                'status': 'error',
                'message': str(e),
                'detections': [],
                'count': 0
            }
            print(json.dumps(error_result), flush=True)

if __name__ == '__main__':
    main()
";

            // Replace the default model path with the provided one
            scriptContent = scriptContent.Replace("models/pren_det_v3.onnx", modelPath);

            await File.WriteAllTextAsync(serverScriptPath, scriptContent);

            // Make script executable on Linux
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var chmodProcess = Process.Start("chmod", $"+x \"{serverScriptPath}\"");
                if (chmodProcess != null)
                {
                    await chmodProcess.WaitForExitAsync();
                }
            }
        }

        public string ServerScriptPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detect_server.py");
    }
}