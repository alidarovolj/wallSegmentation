using UnityEngine;

public static class ColorMap
{
      // PASCAL VOC2012 dataset colors for DeepLabV3+ MobileNet (21 classes)
      private static readonly Color[] colorMap = new Color[]
      {
        new Color(0.0f, 0.0f, 0.0f, 0.3f),     // 0 - background (transparent black)
        new Color(0.8f, 0.2f, 0.2f, 0.8f),    // 1 - aeroplane (red)
        new Color(0.2f, 0.8f, 0.2f, 0.8f),    // 2 - bicycle (green)
        new Color(0.8f, 0.8f, 0.2f, 0.8f),    // 3 - bird (yellow)
        new Color(0.2f, 0.2f, 0.8f, 0.8f),    // 4 - boat (blue)
        new Color(0.8f, 0.2f, 0.8f, 0.8f),    // 5 - bottle (magenta)
        new Color(0.8f, 0.6f, 0.2f, 0.8f),    // 6 - bus (orange)
        new Color(0.6f, 0.2f, 0.8f, 0.8f),    // 7 - car (purple)
        new Color(0.2f, 0.8f, 0.8f, 0.8f),    // 8 - cat (cyan)
        new Color(0.8f, 0.4f, 0.4f, 0.8f),    // 9 - chair (light red)
        new Color(0.4f, 0.8f, 0.4f, 0.8f),    // 10 - cow (light green)
        new Color(0.4f, 0.4f, 0.8f, 0.8f),    // 11 - diningtable (light blue)
        new Color(0.8f, 0.8f, 0.4f, 0.8f),    // 12 - dog (light yellow)
        new Color(0.8f, 0.4f, 0.8f, 0.8f),    // 13 - horse (light magenta)
        new Color(0.4f, 0.8f, 0.8f, 0.8f),    // 14 - motorbike (light cyan)
        new Color(1.0f, 0.6f, 0.4f, 0.8f),    // 15 - person (skin tone)
        new Color(0.6f, 1.0f, 0.4f, 0.8f),    // 16 - pottedplant (bright green)
        new Color(0.6f, 0.4f, 1.0f, 0.8f),    // 17 - sheep (bright blue)
        new Color(1.0f, 0.4f, 0.6f, 0.8f),    // 18 - sofa (pink)
        new Color(0.4f, 1.0f, 0.6f, 0.8f),    // 19 - train (mint green)
        new Color(1.0f, 1.0f, 0.0f, 0.8f),    // 20 - tvmonitor (bright yellow)
        new Color(0.0f, 0.4f, 0.8f),  // 21 - Water (Blue)
        new Color(0.9f, 0.9f, 0.0f),  // 22 - Painting (Yellow)
        new Color(0.2f, 0.4f, 0.8f),  // 23 - Sofa (Blue)
        new Color(0.7f, 0.5f, 0.3f),  // 24 - Shelf (Wood)
        new Color(0.8f, 0.6f, 0.4f),  // 25 - House (Tan)
        new Color(0.0f, 0.3f, 0.6f),  // 26 - Sea (Deep Blue)
        new Color(0.9f, 0.9f, 0.9f),  // 27 - Mirror (Silver)
        new Color(0.6f, 0.2f, 0.2f),  // 28 - Rug (Dark Red)
        new Color(0.5f, 0.7f, 0.3f),  // 29 - Field (Light Green)
        new Color(0.4f, 0.6f, 0.8f),  // 30 - Armchair (Light Blue)
        new Color(0.7f, 0.3f, 0.1f),  // 31 - Seat (Orange Brown)
        new Color(0.5f, 0.4f, 0.2f),  // 32 - Fence (Brown)
        new Color(0.8f, 0.6f, 0.3f),  // 33 - Desk (Light Brown)
        new Color(0.4f, 0.4f, 0.4f),  // 34 - Rock (Gray)
        new Color(0.6f, 0.3f, 0.1f),  // 35 - Wardrobe (Dark Brown)
        new Color(1.0f, 1.0f, 0.8f),  // 36 - Lamp (Light Yellow)
        new Color(0.9f, 0.9f, 1.0f),  // 37 - Bathtub (White)
        new Color(0.5f, 0.5f, 0.5f),  // 38 - Railing (Gray)
        new Color(0.8f, 0.4f, 0.6f),  // 39 - Cushion (Pink)
        new Color(0.3f, 0.3f, 0.3f),  // 40 - Base (Dark Gray)
        new Color(0.7f, 0.5f, 0.2f),  // 41 - Box (Cardboard)
        new Color(0.8f, 0.8f, 0.8f),  // 42 - Column (Light Gray)
        new Color(0.9f, 0.7f, 0.0f),  // 43 - Signboard (Yellow)
        new Color(0.6f, 0.4f, 0.3f),  // 44 - Chest (Wood)
        new Color(0.4f, 0.4f, 0.4f),  // 45 - Counter (Gray)
        new Color(0.9f, 0.8f, 0.6f),  // 46 - Sand (Tan)
        new Color(0.9f, 0.9f, 0.9f),  // 47 - Sink (White)
        new Color(0.5f, 0.5f, 0.6f),  // 48 - Skyscraper (Blue Gray)
        new Color(0.6f, 0.3f, 0.1f),  // 49 - Fireplace (Brick Red)
        new Color(0.9f, 0.9f, 0.9f)   // 50 - Refrigerator (White)
      };

      // ADE20K dataset class names for TopFormer
      public static readonly string[] classNames = new string[]
      {
        "Wall",          // 0 - Стена
        "Building",      // 1 - Здание
        "Sky",           // 2 - Небо
        "Floor",         // 3 - Пол
        "Tree",          // 4 - Дерево
        "Ceiling",       // 5 - Потолок
        "Road",          // 6 - Дорога
        "Bed",           // 7 - Кровать
        "Window",        // 8 - Окно
        "Grass",         // 9 - Трава
        "Cabinet",       // 10 - Шкаф
        "Sidewalk",      // 11 - Тротуар
        "Person",        // 12 - Человек
        "Earth",         // 13 - Земля
        "Door",          // 14 - Дверь
        "Table",         // 15 - Стол
        "Mountain",      // 16 - Гора
        "Plant",         // 17 - Растение
        "Curtain",       // 18 - Занавеска
        "Chair",         // 19 - Стул
        "Car",           // 20 - Автомобиль
        "Water",         // 21 - Вода
        "Painting",      // 22 - Картина
        "Sofa",          // 23 - Диван
        "Shelf",         // 24 - Полка
        "House",         // 25 - Дом
        "Sea",           // 26 - Море
        "Mirror",        // 27 - Зеркало
        "Rug",           // 28 - Ковер
        "Field",         // 29 - Поле
        "Armchair",      // 30 - Кресло
        "Seat",          // 31 - Сиденье
        "Fence",         // 32 - Забор
        "Desk",          // 33 - Письменный стол
        "Rock",          // 34 - Камень
        "Wardrobe",      // 35 - Гардероб
        "Lamp",          // 36 - Лампа
        "Bathtub",       // 37 - Ванна
        "Railing",       // 38 - Перила
        "Cushion",       // 39 - Подушка
        "Base",          // 40 - Основание
        "Box",           // 41 - Коробка
        "Column",        // 42 - Колонна
        "Signboard",     // 43 - Вывеска
        "Chest",         // 44 - Комод
        "Counter",       // 45 - Стойка
        "Sand",          // 46 - Песок
        "Sink",          // 47 - Раковина
        "Skyscraper",    // 48 - Небоскреб
        "Fireplace",     // 49 - Камин
        "Refrigerator"   // 50 - Холодильник
      };

      public static Color32 GetColor(int classIndex)
      {
            if (classIndex >= 0 && classIndex < colorMap.Length)
            {
                  return colorMap[classIndex];
            }
            // If class index is higher than available colors, generate a pseudo-random color
            return GenerateColorFromIndex(classIndex);
      }

      public static Color[] GetAllColors()
      {
            return colorMap;
      }

      private static Color32 GenerateColorFromIndex(int index)
      {
            // Generate a deterministic but varied color based on the index
            float hue = (index * 137.508f) % 360f / 360f; // Golden angle approximation for good distribution
            float saturation = 0.7f + (index % 3) * 0.1f; // Vary saturation
            float value = 0.8f + (index % 2) * 0.2f; // Vary brightness

            Color color = Color.HSVToRGB(hue, saturation, value);
            return new Color32((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), 120);
      }

      // PASCAL VOC 2012 class names (21 classes) - for DeepLabV3+ model
      public static readonly string[] pascalVocClassNames = new string[]
      {
            "background",    // 0
            "aeroplane",     // 1
            "bicycle",       // 2
            "bird",          // 3
            "boat",          // 4
            "bottle",        // 5
            "bus",           // 6
            "car",           // 7
            "cat",           // 8
            "chair",         // 9
            "cow",           // 10
            "diningtable",   // 11
            "dog",           // 12
            "horse",         // 13
            "motorbike",     // 14
            "person",        // 15
            "pottedplant",   // 16
            "sheep",         // 17
            "sofa",          // 18
            "train",         // 19
            "tvmonitor"      // 20
      };

      // BiSeNet class names (12 classes) - Common for Cityscapes/mobile segmentation
      public static readonly string[] biSeNetClassNames = new string[]
      {
            "road",          // 0
            "sidewalk",      // 1
            "building",      // 2
            "wall",          // 3
            "fence",         // 4
            "pole",          // 5
            "traffic_light", // 6
            "traffic_sign",  // 7
            "vegetation",    // 8
            "terrain",       // 9
            "sky",           // 10
            "person"         // 11
      };

      // ADE20K class names (150 classes) - For SegFormer model
      public static readonly string[] ade20kClassNames = new string[]
      {
            "wall", "building", "sky", "floor", "tree", "ceiling", "road", "bed", "windowpane", "grass",
            "cabinet", "sidewalk", "person", "earth", "door", "table", "mountain", "plant", "curtain", "chair",
            "car", "water", "painting", "sofa", "shelf", "house", "sea", "mirror", "rug", "field",
            "armchair", "seat", "fence", "desk", "rock", "wardrobe", "lamp", "bathtub", "railing", "cushion",
            "base", "box", "column", "signboard", "chest of drawers", "counter", "sand", "sink", "skyscraper", "fireplace",
            "refrigerator", "grandstand", "path", "stairs", "runway", "case", "pool table", "pillow", "screen door", "stairway",
            "river", "bridge", "bookcase", "blind", "coffee table", "toilet", "flower", "book", "hill", "bench",
            "countertop", "stove", "palm", "kitchen island", "computer", "swivel chair", "boat", "bar", "arcade machine", "hovel",
            "bus", "towel", "light", "truck", "tower", "chandelier", "awning", "streetlight", "booth", "television receiver",
            "airplane", "dirt track", "apparel", "pole", "land", "bannister", "escalator", "ottoman", "bottle", "buffet",
            "poster", "stage", "van", "ship", "fountain", "conveyer belt", "canopy", "washer", "plaything", "swimming pool",
            "stool", "barrel", "basket", "waterfall", "tent", "bag", "minibike", "cradle", "oven", "ball",
            "food", "step", "tank", "trade name", "microwave", "pot", "animal", "bicycle", "lake", "dishwasher",
            "screen", "blanket", "sculpture", "hood", "sconce", "vase", "traffic light", "tray", "ashcan", "fan",
            "pier", "crt screen", "plate", "monitor", "bulletin board", "shower", "radiator", "glass", "clock", "flag"
      };

      // Get PASCAL VOC class name by index
      public static string GetPascalVocClassName(int classIndex)
      {
            if (classIndex < 0 || classIndex >= pascalVocClassNames.Length)
                  return $"Unknown Class {classIndex}";
            return pascalVocClassNames[classIndex];
      }

      // Get BiSeNet class name by index
      public static string GetBiSeNetClassName(int classIndex)
      {
            if (classIndex < 0 || classIndex >= biSeNetClassNames.Length)
                  return $"Unknown Class {classIndex}";
            return biSeNetClassNames[classIndex];
      }

      // Get ADE20K class name by index
      public static string GetADE20KClassName(int classIndex)
      {
            if (classIndex < 0 || classIndex >= ade20kClassNames.Length)
                  return $"Unknown Class {classIndex}";
            return ade20kClassNames[classIndex];
      }

      // Get class name based on model type and number of classes
      public static string GetClassName(int classIndex, int numClasses, string modelName = "")
      {
            modelName = modelName?.ToLower() ?? "";

            if (modelName.Contains("bisenet") || numClasses == 12)
            {
                  return GetBiSeNetClassName(classIndex);
            }
            else if (modelName.Contains("segformer") || modelName.Contains("model_fp16") || numClasses == 150)
            {
                  return GetADE20KClassName(classIndex);
            }
            else
            {
                  return GetPascalVocClassName(classIndex);
            }
      }
}