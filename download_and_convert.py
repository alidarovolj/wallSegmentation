import torch
from transformers import AutoModelForSemanticSegmentation, AutoImageProcessor
import onnx
import os

# Настройки
model_id = "leftattention/segformer-b4-wall"
output_path = "segformer-b4-wall.onnx"

print("🔄 Загрузка модели из Hugging Face...")
try:
    # Загружаем модель и процессор
    model = AutoModelForSemanticSegmentation.from_pretrained(model_id)
    processor = AutoImageProcessor.from_pretrained(model_id)
    
    # Переводим в режим eval
    model.eval()
    
    print("✅ Модель успешно загружена!")
    print(f"📊 Информация о модели:")
    print(f"   - Количество классов: {model.config.num_labels}")
    print(f"   - Размер входного изображения: {processor.size}")
    
    # Создаем пример входного тензора
    # Используем размер из конфигурации процессора
    height = processor.size.get("height", 512)
    width = processor.size.get("width", 512)
    dummy_input = torch.randn(1, 3, height, width)
    
    print(f"🔧 Экспорт в ONNX формат...")
    print(f"   - Входной тензор: [1, 3, {height}, {width}]")
    
    # Экспорт в ONNX
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
    
    # Проверяем созданный файл
    if os.path.exists(output_path):
        file_size = os.path.getsize(output_path) / (1024 * 1024)  # MB
        print(f"✅ УСПЕХ! Модель сконвертирована:")
        print(f"   - Файл: {output_path}")
        print(f"   - Размер: {file_size:.1f} MB")
        print(f"")
        print(f"🎯 Следующие шаги:")
        print(f"   1. Скопируйте файл '{output_path}' в папку Assets/Models/ вашего Unity проекта")
        print(f"   2. В AsyncSegmentationManager выберите эту модель")
        print(f"   3. Запустите сцену")
    else:
        print("❌ Ошибка: файл не был создан")
        
except Exception as e:
    print(f"❌ Ошибка при загрузке или конвертации: {e}")
    print("💡 Попробуйте:")
    print("   - Проверить интернет соединение")
    print("   - Переустановить библиотеки: pip install --upgrade transformers torch")