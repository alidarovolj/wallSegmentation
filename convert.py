import torch
from transformers import AutoModelForSemanticSegmentation, AutoImageProcessor
import onnx
import os

model_id = "leftattention/segformer-b4-wall"
output_path = "segformer-b4-wall.onnx"

print("Loading model from Hugging Face...")
try:
    model = AutoModelForSemanticSegmentation.from_pretrained(model_id)
    processor = AutoImageProcessor.from_pretrained(model_id)
    
    model.eval()
    
    print("Model loaded successfully!")
    print(f"Number of classes: {model.config.num_labels}")
    print(f"Input size: {processor.size}")
    
    height = processor.size.get("height", 512)
    width = processor.size.get("width", 512)
    dummy_input = torch.randn(1, 3, height, width)
    
    print(f"Exporting to ONNX format...")
    print(f"Input tensor: [1, 3, {height}, {width}]")
    
    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        input_names=['pixel_values'],
        output_names=['logits'],
        opset_version=13,
        export_params=True,
        do_constant_folding=True,
        dynamic_axes={
            'pixel_values': {0: 'batch_size'},
            'logits': {0: 'batch_size'}
        }
    )
    
    if os.path.exists(output_path):
        file_size = os.path.getsize(output_path) / (1024 * 1024)
        print(f"SUCCESS! Model converted:")
        print(f"   - File: {output_path}")
        print(f"   - Size: {file_size:.1f} MB")
        print(f"")
        print(f"Next steps:")
        print(f"   1. Copy '{output_path}' to Assets/Models/ in your Unity project")
        print(f"   2. Select this model in AsyncSegmentationManager")
        print(f"   3. Run the scene")
    else:
        print("ERROR: file was not created")
        
except Exception as e:
    print(f"Error during loading or conversion: {e}")
    print("Try:")
    print("   - Check internet connection")
    print("   - Reinstall libraries: pip install --upgrade transformers torch")