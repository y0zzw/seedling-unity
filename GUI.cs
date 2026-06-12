using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

public class GUI : MonoBehaviour {

    public static GUI instance;
    
    public int selectedPlantType = 0;  // 0=小松菜, 1=新植物

    // ★ 新增：兩顆植物選擇按鈕的引用
    public Button buttonPlant0;
    public Button buttonPlant1;
    public Button buttonUpload;
    public TMP_Dropdown weatherDropdown;
    private string weatherServerUrl = "http://10.33.7.32:5050";
    private List<string> availableDates = new List<string>();

    private Color colorSelected   = new Color(0.5f, 0.5f, 0.5f, 1f); // 深灰（選中）
    private Color colorNormal     = new Color(1f,   1f,   1f,   1f); // 白（未選）

    public void SelectPlant(int plantType) {
        selectedPlantType = plantType;
        Debug.Log($"Selected plant type: {plantType}");
        UpdateButtonColors();
    }
    private void UpdateButtonColors() {
    SetButtonHighlight(buttonPlant0, selectedPlantType == 0);
    SetButtonHighlight(buttonPlant1, selectedPlantType == 1);
}

    private void SetButtonHighlight(Button btn, bool isSelected) {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor   = isSelected 
            ? new Color(0.8f, 0.82f, 0.83f, 1f) 
            : new Color(1f, 1f, 1f, 1f);
        cb.selectedColor = cb.normalColor;
        btn.colors = cb;
    }

    void Awake() {
        instance = this;
        UpdateButtonColors(); // 初始狀態：小松菜預設選中
        StartCoroutine(FetchDates());
    }

    public TMP_InputField inputDayCount;

    public TMP_InputField inputTemperature;

    public TMP_InputField inputLight;

    public TMP_InputField inputHumidity;
    //public TMP_InputField inputLightDuration;
    //public TMP_InputField inputLightIntensity;

//    public TMP_InputField inputWaterDuration;
//    public TMP_InputField inputWaterLevel;
//    public TMP_InputField inputWaterInterval1;
//    public TMP_InputField inputWaterInterval2;

    public Slider daySlider;
    public Slider fovSlider;

    public TextMeshProUGUI textDay;
    public TextMeshProUGUI textHeight;
    public TextMeshProUGUI textLeaf;
    public TextMeshProUGUI textWater;
    public TextMeshProUGUI textNode;

    // ── 啟動時抓日期清單，填入 Dropdown ─────────────────────────────
private IEnumerator FetchDates() {
    using (UnityWebRequest req = UnityWebRequest.Get(weatherServerUrl + "/dates")) {
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) {
            Debug.LogError("FetchDates failed: " + req.error);
            yield break;
        }

        // 解析 JSON 陣列，例如 ["0504","0503",...]
        string json = req.downloadHandler.text;
        json = json.Trim('[', ']');
        string[] parts = json.Split(',');

        availableDates.Clear();
        weatherDropdown.ClearOptions();
        var options = new List<string>();

        foreach (string p in parts) {
            string date = p.Trim().Trim('"');
            if (date.Length == 4) {
                availableDates.Add(date);
                // 把 "0504" 轉成 "05/04" 顯示
                options.Add(date.Substring(0, 2) + "/" + date.Substring(2, 2));
            }
        }

        weatherDropdown.AddOptions(options);
        weatherDropdown.onValueChanged.AddListener(OnDateSelected);

        // 預設載入第一筆（最新的）
        if (availableDates.Count > 0)
            StartCoroutine(FetchForecast(availableDates[0]));
    }
}

// ── 選日期後抓預報，自動填入三個 InputField ──────────────────────
private void OnDateSelected(int index) {
    if (index < 0 || index >= availableDates.Count) return;
    StartCoroutine(FetchForecast(availableDates[index]));
}

    private IEnumerator FetchForecast(string start) {
        string url = weatherServerUrl + "/forecast?start=" + start;
        using (UnityWebRequest req = UnityWebRequest.Get(url)) {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("FetchForecast failed: " + req.error);
                yield break;
            }

            // 解析 JSON，逐筆取出三個欄位
            string json = req.downloadHandler.text;
            var rows = ParseForecastJson(json);

            List<string> temps = new List<string>();
            List<string> lights = new List<string>();
            List<string> humids = new List<string>();

            foreach (var row in rows) {
                temps.Add(row[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                lights.Add(row[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                humids.Add(row[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            }

            inputTemperature.text = string.Join("\n", temps);
            inputLight.text       = string.Join("\n", lights);
            inputHumidity.text    = string.Join("\n", humids);
            inputDayCount.text    = rows.Count.ToString();

            Debug.Log($"Loaded {rows.Count} days from {start}");
        }
    }

    // ── 簡易 JSON 解析（不依賴 JsonUtility）────────────────────────
    private List<float[]> ParseForecastJson(string json) {
    var result = new List<float[]>();
    int start = 0;
    while (true) {
        int open = json.IndexOf('{', start);
        if (open < 0) break;
        int close = json.IndexOf('}', open);
        if (close < 0) break;
        string entry = json.Substring(open, close - open + 1);
        float temp     = ExtractFloat(entry, "temperature");
        float light    = ExtractFloat(entry, "light");
        float humidity = ExtractFloat(entry, "humidity");
        result.Add(new float[]{ temp, light, humidity });
        start = close + 1;
    }
    return result;
    }

    private float ExtractFloat(string json, string key) {
        string search = "\"" + key + "\":";
        int idx = json.IndexOf(search);
        if (idx < 0) return 0f;
        idx += search.Length;
        int end = json.IndexOfAny(new char[]{',', '}', ']'}, idx);
        string val = json.Substring(idx, end - idx).Trim();
        return float.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
    }
}

