using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class ColorPaletteManager : MonoBehaviour
{
    [System.Serializable]
    public class ColorTitle
    {
        public string ru;
        public string en;
        public string kz;
    }

    [System.Serializable]
    public class ParentColor
    {
        public int id;
        public string hex;
        public string title;
    }

    [System.Serializable]
    public class Catalog
    {
        public int id;
        public string title;
        public string code;
    }

    [System.Serializable]
    public class ColorData
    {
        public int id;
        public string hex;
        public ColorTitle title;
        public string ral;
        public ParentColor parent_color;
        public Catalog catalog;
        public bool is_favourite;
    }

    [System.Serializable]
    public class ColorApiResponse
    {
        public List<ColorData> data;
    }

    public GameObject colorSwatchPrefab; 
    public Transform paletteContainer; 

    // Добавлена ссылка на SegmentationManager
    public SegmentationManager segmentationManager;

    private const string ApiUrl = "https://api.remalux.kz/api/colors?page=1&perPage=10";

    void Start()
    {
        StartCoroutine(FetchColors());
    }

    IEnumerator FetchColors()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(ApiUrl))
        {
            // Устанавливаем заголовки, как в вашем примере
            webRequest.SetRequestHeader("Accept", "application/json, text/plain, */*");
            
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error fetching colors: " + webRequest.error);
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                ColorApiResponse colorResponse = JsonUtility.FromJson<ColorApiResponse>(jsonResponse);
                CreatePalette(colorResponse.data);
            }
        }
    }

    void CreatePalette(List<ColorData> colors)
    {
        if (colorSwatchPrefab == null || paletteContainer == null)
        {
            Debug.LogError("Color Swatch Prefab or Palette Container is not assigned in the inspector!");
            return;
        }

        // Проверяем, назначен ли SegmentationManager
        if (segmentationManager == null)
        {
            Debug.LogError("SegmentationManager is not assigned in the inspector!");
            // Попробуем найти его в сцене, чтобы избежать ошибок
            segmentationManager = FindObjectOfType<SegmentationManager>();
            if (segmentationManager == null)
            {
                Debug.LogError("Could not find SegmentationManager in the scene!");
                return;
            }
        }

        // Очищаем предыдущие цвета, если они есть
        foreach (Transform child in paletteContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var colorData in colors)
        {
            GameObject swatch = Instantiate(colorSwatchPrefab, paletteContainer);
            UnityEngine.UI.Image image = swatch.GetComponent<UnityEngine.UI.Image>();
            
            // Получаем или добавляем компонент Button
            UnityEngine.UI.Button button = swatch.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                button = swatch.AddComponent<UnityEngine.UI.Button>();
            }

            if (image != null)
            {
                Color newColor;
                if (ColorUtility.TryParseHtmlString(colorData.hex, out newColor))
                {
                    image.color = newColor;

                    // Настраиваем обработчик клика
                    button.onClick.AddListener(() => OnColorSelected(newColor));
                }
            }
        }
    }

    void OnColorSelected(Color selectedColor)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetPaintColor(selectedColor);
        }
        else
        {
            Debug.LogError("Cannot set color because SegmentationManager is not available.");
        }
    }
} 