import os
import time
import json
from pathlib import Path
import cv2
import numpy as np
from ultralytics import YOLO
import pandas as pd
from datetime import datetime

class YOLOBenchmark:
    def __init__(self, models_dir="models", images_dir="images"):
        self.models_dir = Path(models_dir)
        self.images_dir = Path(images_dir)
        self.results = []
        
    def get_model_files(self):
        """Get all YOLO model files from the models directory"""
        model_extensions = ['.pt', '.onnx', '.engine']
        models = []
        
        if not self.models_dir.exists():
            print(f"Models directory '{self.models_dir}' not found!")
            return models
            
        for ext in model_extensions:
            models.extend(self.models_dir.glob(f"*{ext}"))
        return models
    
    def get_image_files(self):
        """Get all image files from the images directory"""
        image_extensions = ['.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp']
        images = []
        
        if not self.images_dir.exists():
            print(f"Images directory '{self.images_dir}' not found!")
            return images
            
        for ext in image_extensions:
            images.extend(self.images_dir.glob(f"*{ext}"))
        return images
    
    def run_inference(self, model, image_path):
        """Run inference on a single image and return results with timing"""
        start_time = time.time()
        
        try:
            results = model(image_path, verbose=False)
            inference_time = time.time() - start_time
            
            # Extract detection information
            detections = []
            if len(results) > 0:
                result = results[0]
                if result.boxes is not None:
                    boxes = result.boxes
                    for i in range(len(boxes)):
                        detection = {
                            'class_id': int(boxes.cls[i]),
                            'class_name': model.names[int(boxes.cls[i])],
                            'confidence': float(boxes.conf[i]),
                            'bbox': boxes.xyxy[i].tolist(),
                        }
                        detections.append(detection)
            
            return {
                'success': True,
                'inference_time': inference_time,
                'detections': detections,
                'num_detections': len(detections)
            }
            
        except Exception as e:
            return {
                'success': False,
                'error': str(e),
                'inference_time': time.time() - start_time,
                'detections': [],
                'num_detections': 0
            }
    
    def benchmark_model(self, model_path):
        """Benchmark a single model against all images"""
        print(f"\nBenchmarking model: {model_path.name}")
        
        try:
            # Load model
            model_load_start = time.time()
            model = YOLO(str(model_path))
            model_load_time = time.time() - model_load_start
            print(f"Model loaded in {model_load_time:.3f}s")
            
            # Get all images
            images = self.get_image_files()
            if not images:
                print("No images found for benchmarking!")
                return
            
            model_results = {
                'model_name': model_path.name,
                'model_path': str(model_path),
                'model_load_time': model_load_time,
                'total_images': len(images),
                'successful_inferences': 0,
                'failed_inferences': 0,
                'total_inference_time': 0,
                'average_inference_time': 0,
                'total_detections': 0,
                'total_confidence_sum': 0,
                'average_confidence': 0,
                'image_results': []
            }
            
            # Run inference on each image
            for i, image_path in enumerate(images):
                print(f"Processing image {i+1}/{len(images)}: {image_path.name}")
                
                result = self.run_inference(model, image_path)
                
                # Calculate confidence sum for this image
                image_confidence_sum = sum(det['confidence'] for det in result['detections'])
                
                image_result = {
                    'image_name': image_path.name,
                    'image_path': str(image_path),
                    'success': result['success'],
                    'inference_time': result['inference_time'],
                    'num_detections': result['num_detections'],
                    'detections': result['detections'],
                    'average_confidence': image_confidence_sum / result['num_detections'] if result['num_detections'] > 0 else 0
                }
                
                if result['success']:
                    model_results['successful_inferences'] += 1
                    model_results['total_detections'] += result['num_detections']
                    model_results['total_confidence_sum'] += image_confidence_sum
                else:
                    model_results['failed_inferences'] += 1
                    image_result['error'] = result.get('error', 'Unknown error')
                
                model_results['total_inference_time'] += result['inference_time']
                model_results['image_results'].append(image_result)
            
            # Calculate averages
            if model_results['successful_inferences'] > 0:
                model_results['average_inference_time'] = (
                    model_results['total_inference_time'] / model_results['total_images']
                )
            
            if model_results['total_detections'] > 0:
                model_results['average_confidence'] = (
                    model_results['total_confidence_sum'] / model_results['total_detections']
                )
            
            self.results.append(model_results)
            print(f"Completed benchmarking {model_path.name}")
            print(f"Success rate: {model_results['successful_inferences']}/{model_results['total_images']}")
            print(f"Average inference time: {model_results['average_inference_time']:.3f}s")
            print(f"Total detections: {model_results['total_detections']}")
            print(f"Average confidence: {model_results['average_confidence']:.3f}")
            
        except Exception as e:
            print(f"Failed to load model {model_path.name}: {e}")
    
    def run_benchmark(self):
        """Run benchmark on all models"""
        print("Starting YOLO Model Benchmark")
        print("=" * 50)
        
        models = self.get_model_files()
        if not models:
            print("No models found for benchmarking!")
            return
        
        images = self.get_image_files()
        if not images:
            print("No images found for benchmarking!")
            return
            
        print(f"Found {len(models)} models and {len(images)} images")
        
        benchmark_start = time.time()
        
        for model_path in models:
            self.benchmark_model(model_path)
        
        total_benchmark_time = time.time() - benchmark_start
        
        print("\n" + "=" * 50)
        print("Benchmark Summary")
        print("=" * 50)
        print(f"Total benchmark time: {total_benchmark_time:.2f}s")
        print(f"Models tested: {len(self.results)}")
        
        # Print summary table
        self.print_summary()
        
        # Save detailed results
        self.save_results()
    
    def print_summary(self):
        """Print a summary table of all model results"""
        if not self.results:
            return
            
        print("\nModel Performance Summary:")
        print("-" * 120)
        print(f"{'Model':<25} {'Load Time':<10} {'Avg Inference':<15} {'Success Rate':<12} {'Total Detections':<15} {'Avg Confidence':<15}")
        print("-" * 120)
        
        for result in self.results:
            success_rate = f"{result['successful_inferences']}/{result['total_images']}"
            print(f"{result['model_name']:<25} "
                  f"{result['model_load_time']:<10.3f} "
                  f"{result['average_inference_time']:<15.3f} "
                  f"{success_rate:<12} "
                  f"{result['total_detections']:<15} "
                  f"{result['average_confidence']:<15.3f}")
    
    def save_results(self):
        """Save detailed results to JSON and CSV files"""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        
        # Save detailed JSON results
        json_filename = f"benchmark_results_{timestamp}.json"
        with open(json_filename, 'w') as f:
            json.dump(self.results, f, indent=2)
        print(f"\nDetailed results saved to: {json_filename}")
        
        # Save summary CSV
        csv_filename = f"benchmark_summary_{timestamp}.csv"
        summary_data = []
        
        for result in self.results:
            summary_data.append({
                'Model Name': result['model_name'],
                'Model Load Time (s)': result['model_load_time'],
                'Total Images': result['total_images'],
                'Successful Inferences': result['successful_inferences'],
                'Failed Inferences': result['failed_inferences'],
                'Success Rate (%)': (result['successful_inferences'] / result['total_images']) * 100,
                'Total Inference Time (s)': result['total_inference_time'],
                'Average Inference Time (s)': result['average_inference_time'],
                'Total Detections': result['total_detections'],
                'Average Detections per Image': result['total_detections'] / result['total_images'] if result['total_images'] > 0 else 0,
                'Average Confidence': result['average_confidence']
            })
        
        df = pd.DataFrame(summary_data)
        df.to_csv(csv_filename, index=False)
        print(f"Summary saved to: {csv_filename}")

def main():
    # You can customize the directories here
    models_dir = "models"
    images_dir = "images"
    
    benchmark = YOLOBenchmark(models_dir, images_dir)
    benchmark.run_benchmark()

if __name__ == "__main__":
    main()