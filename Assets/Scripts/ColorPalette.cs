using UnityEngine;

[CreateAssetMenu(fileName = "NewColorPalette", menuName = "AR Painting/Color Palette")]
public class ColorPalette : ScriptableObject
{
      [Header("Palette Info")]
      public string paletteName;
      public Color[] colors;
}