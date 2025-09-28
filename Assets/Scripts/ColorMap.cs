using UnityEngine;
using System.Collections.Generic;

public static class ColorMap
{
    // Correct and complete ADE20K color map (150 classes)
    private static readonly Color[] ade20kColorMap = new Color[]
    {
        new Color(120/255f, 120/255f, 120/255f), // 0: wall
        new Color(180/255f, 120/255f, 120/255f), // 1: building
        new Color(6/255f, 230/255f, 230/255f),   // 2: sky
        new Color(80/255f, 50/255f, 50/255f),    // 3: floor
        new Color(4/255f, 200/255f, 3/255f),     // 4: tree
        new Color(120/255f, 120/255f, 80/255f),  // 5: ceiling
        new Color(140/255f, 140/255f, 140/255f), // 6: road
        new Color(204/255f, 5/255f, 255/255f),   // 7: bed
        new Color(230/255f, 230/255f, 230/255f), // 8: windowpane
        new Color(10/255f, 255/255f, 71/255f),   // 9: grass
        new Color(255/255f, 20/255f, 147/255f),  // 10: cabinet
        new Color(20/255f, 255/255f, 20/255f),   // 11: sidewalk
        new Color(255/255f, 0/255f, 0/255f),     // 12: person
        new Color(255/255f, 235/255f, 205/255f), // 13: earth
        new Color(120/255f, 120/255f, 70/255f),  // 14: door
        new Color(255/255f, 165/255f, 0/255f),   // 15: table
        new Color(112/255f, 128/255f, 144/255f), // 16: mountain
        new Color(34/255f, 139/255f, 34/255f),   // 17: plant
        new Color(222/255f, 184/255f, 135/255f), // 18: curtain
        new Color(255/255f, 105/255f, 180/255f), // 19: chair
        new Color(0/255f, 0/255f, 128/255f),     // 20: car
        new Color(0/255f, 0/255f, 255/255f),     // 21: water
        new Color(255/255f, 215/255f, 0/255f),   // 22: painting
        new Color(138/255f, 43/255f, 226/255f),  // 23: sofa
        new Color(245/255f, 222/255f, 179/255f), // 24: shelf
        new Color(210/255f, 105/255f, 30/255f),  // 25: house
        new Color(0/255f, 191/255f, 255/255f),   // 26: sea
        new Color(192/255f, 192/255f, 192/255f), // 27: mirror
        new Color(165/255f, 42/255f, 42/255f),   // 28: rug
        new Color(127/255f, 255/255f, 0/255f),   // 29: field
        new Color(255/255f, 127/255f, 80/255f),  // 30: armchair
        new Color(255/255f, 69/255f, 0/255f),    // 31: seat
        new Color(160/255f, 82/255f, 45/255f),   // 32: fence
        new Color(222/255f, 184/255f, 135/255f), // 33: desk
        new Color(105/255f, 105/255f, 105/255f), // 34: rock
        new Color(139/255f, 69/255f, 19/255f),   // 35: wardrobe
        new Color(255/255f, 250/255f, 205/255f), // 36: lamp
        new Color(240/255f, 255/255f, 255/255f), // 37: bathtub
        new Color(176/255f, 196/255f, 222/255f), // 38: railing
        new Color(255/255f, 182/255f, 193/255f), // 39: cushion
        new Color(70/255f, 130/255f, 180/255f),  // 40: base
        new Color(244/255f, 164/255f, 96/255f),  // 41: box
        new Color(211/255f, 211/255f, 211/255f), // 42: column
        new Color(255/255f, 215/255f, 0/255f),   // 43: signboard
        new Color(245/255f, 222/255f, 179/255f), // 44: chest of drawers
        new Color(220/255f, 220/255f, 220/255f), // 45: counter
        new Color(245/255f, 245/255f, 220/255f), // 46: sand
        new Color(240/255f, 255/255f, 240/255f), // 47: sink
        new Color(72/255f, 61/255f, 139/255f),   // 48: skyscraper
        new Color(178/255f, 34/255f, 34/255f),   // 49: fireplace
        new Color(240/255f, 248/255f, 255/255f), // 50: refrigerator
        new Color(218/255f, 112/255f, 214/255f), // 51: grandstand
        new Color(107/255f, 142/255f, 35/255f),  // 52: path
        new Color(255/255f, 250/255f, 250/255f), // 53: stairs
        new Color(135/255f, 206/255f, 235/255f), // 54: runway
        new Color(255/255f, 228/255f, 181/255f), // 55: case
        new Color(173/255f, 216/255f, 230/255f), // 56: pool table
        new Color(255/255f, 192/255f, 203/255f), // 57: pillow
        new Color(240/255f, 230/255f, 140/255f), // 58: screen door
        new Color(255/255f, 228/255f, 225/255f), // 59: stairway
        new Color(30/255f, 144/255f, 255/255f),  // 60: river
        new Color(169/255f, 169/255f, 169/255f), // 61: bridge
        new Color(139/255f, 69/255f, 19/255f),   // 62: bookcase
        new Color(255/255f, 239/255f, 213/255f), // 63: blind
        new Color(210/255f, 180/255f, 140/255f), // 64: coffee table
        new Color(255/255f, 240/255f, 245/255f), // 65: toilet
        new Color(255/255f, 20/255f, 147/255f),  // 66: flower
        new Color(255/255f, 222/255f, 173/255f), // 67: book
        new Color(143/255f, 188/255f, 143/255f), // 68: hill
        new Color(205/255f, 92/255f, 92/255f),   // 69: bench
        new Color(245/255f, 255/255f, 250/255f), // 70: countertop
        new Color(47/255f, 79/255f, 79/255f),    // 71: stove
        new Color(255/255f, 140/255f, 0/255f),   // 72: palm
        new Color(238/255f, 130/255f, 238/255f), // 73: kitchen island
        new Color(152/255f, 251/255f, 152/255f), // 74: computer
        new Color(240/255f, 230/255f, 140/255f), // 75: swivel chair
        new Color(70/255f, 130/255f, 180/255f),  // 76: boat
        new Color(255/255f, 99/255f, 71/255f),   // 77: bar
        new Color(65/255f, 105/255f, 225/255f),  // 78: arcade machine
        new Color(210/255f, 180/255f, 140/255f), // 79: hovel
        new Color(255/255f, 218/255f, 185/255f), // 80: bus
        new Color(255/255f, 240/255f, 245/255f), // 81: towel
        new Color(255/255f, 255/255f, 224/255f), // 82: light
        new Color(0/255f, 255/255f, 255/255f),   // 83: truck
        new Color(255/255f, 228/255f, 196/255f), // 84: tower
        new Color(255/255f, 215/255f, 0/255f),   // 85: chandelier
        new Color(240/255f, 128/255f, 128/255f), // 86: awning
        new Color(119/255f, 136/255f, 153/255f), // 87: streetlight
        new Color(255/255f, 182/255f, 193/255f), // 88: booth
        new Color(0/255f, 0/255f, 205/255f),     // 89: television receiver
        new Color(176/255f, 224/255f, 230/255f), // 90: airplane
        new Color(188/255f, 143/255f, 143/255f), // 91: dirt track
        new Color(255/255f, 228/255f, 225/255f), // 92: apparel
        new Color(135/255f, 206/255f, 250/255f), // 93: pole
        new Color(250/255f, 235/255f, 215/255f), // 94: land
        new Color(245/255f, 245/255f, 245/255f), // 95: bannister
        new Color(255/255f, 222/255f, 173/255f), // 96: escalator
        new Color(255/255f, 248/255f, 220/255f), // 97: ottoman
        new Color(100/255f, 149/255f, 237/255f), // 98: bottle
        new Color(255/255f, 235/255f, 205/255f), // 99: buffet
        new Color(255/255f, 105/255f, 180/255f), // 100: poster
        new Color(255/255f, 160/255f, 122/255f), // 101: stage
        new Color(240/255f, 230/255f, 140/255f), // 102: van
        new Color(72/255f, 209/255f, 204/255f),  // 103: ship
        new Color(64/255f, 224/255f, 208/255f),  // 104: fountain
        new Color(255/255f, 99/255f, 71/255f),   // 105: conveyer belt
        new Color(240/255f, 255/255f, 240/255f), // 106: canopy
        new Color(240/255f, 248/255f, 255/255f), // 107: washer
        new Color(255/255f, 218/255f, 185/255f), // 108: plaything
        new Color(102/255f, 205/255f, 170/255f), // 109: swimming pool
        new Color(240/255f, 128/255f, 128/255f), // 110: stool
        new Color(221/255f, 160/255f, 221/255f), // 111: barrel
        new Color(255/255f, 228/255f, 196/255f), // 112: basket
        new Color(175/255f, 238/255f, 238/255f), // 113: waterfall
        new Color(250/255f, 240/255f, 230/255f), // 114: tent
        new Color(255/255f, 192/255f, 203/255f), // 115: bag
        new Color(0/255f, 255/255f, 127/255f),   // 116: minibike
        new Color(255/255f, 240/255f, 245/255f), // 117: cradle
        new Color(255/255f, 182/255f, 193/255f), // 118: oven
        new Color(255/255f, 20/255f, 147/255f),  // 119: ball
        new Color(255/255f, 228/255f, 181/255f), // 120: food
        new Color(255/255f, 250/255f, 240/255f), // 121: step
        new Color(0/255f, 128/255f, 128/255f),   // 122: tank
        new Color(233/255f, 150/255f, 122/255f), // 123: trade name
        new Color(255/255f, 245/255f, 238/255f), // 124: microwave
        new Color(255/255f, 160/255f, 122/255f), // 125: pot
        new Color(139/255f, 0/255f, 0/255f),     // 126: animal
        new Color(0/255f, 250/255f, 154/255f),   // 127: bicycle
        new Color(240/255f, 255/255f, 255/255f), // 128: lake
        new Color(245/255f, 245/255f, 245/255f), // 129: dishwasher
        new Color(255/255f, 250/255f, 205/255f), // 130: screen
        new Color(250/255f, 235/255f, 215/255f), // 131: blanket
        new Color(255/255f, 228/255f, 225/255f), // 132: sculpture
        new Color(255/255f, 222/255f, 173/255f), // 133: hood
        new Color(255/255f, 218/255f, 185/255f), // 134: sconce
        new Color(255/255f, 240/255f, 245/255f), // 135: vase
        new Color(255/255f, 0/255f, 0/255f),     // 136: traffic light
        new Color(218/255f, 165/255f, 32/255f),  // 137: tray
        new Color(128/255f, 128/255f, 128/255f), // 138: ashcan
        new Color(245/255f, 245/255f, 220/255f), // 139: fan
        new Color(70/255f, 130/255f, 180/255f),  // 140: pier
        new Color(192/255f, 192/255f, 192/255f), // 141: crt screen
        new Color(255/255f, 222/255f, 173/255f), // 142: plate
        new Color(0/255f, 0/255f, 0/255f),       // 143: monitor
        new Color(176/255f, 196/255f, 222/255f), // 144: bulletin board
        new Color(173/255f, 216/255f, 230/255f), // 145: shower
        new Color(135/255f, 206/255f, 250/255f), // 146: radiator
        new Color(224/255f, 255/255f, 255/255f), // 147: glass
        new Color(255/255f, 250/255f, 250/255f), // 148: clock
        new Color(255/255f, 20/255f, 147/255f)   // 149: flag
    };

    // Correct ADE20K class names (150 classes)
    private static readonly string[] ade20kClassNames = new string[]
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

    /// <summary>
    /// Returns the full color map for the current dataset.
    /// </summary>
    public static Color[] GetAllColors()
    {
        // For now, we are only using ADE20K. This can be extended later.
        return ade20kColorMap;
    }

    /// <summary>
    /// Returns the class name for a given index in the ADE20K dataset.
    /// </summary>
    public static string GetClassName(int classIndex)
    {
        if (classIndex >= 0 && classIndex < ade20kClassNames.Length)
        {
            return ade20kClassNames[classIndex];
        }
        return $"Unknown Class {classIndex}";
    }
}