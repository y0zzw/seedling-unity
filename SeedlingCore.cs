using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SeedlingCore : MonoBehaviour {

    public static SeedlingCore instance;

    public GameObject plantPrefab;
    [SerializeField] List<float> heights = new List<float>(0);
    [SerializeField] List<float> leafCounts = new List<float>(0);
    [SerializeField] List<float> nodeCms = new List<float>(0);
    List<ProceduralPlant> generatedPlants = new List<ProceduralPlant>(0);
    List<int> wateringDays = new List<int>(0);
    [Header("V-GNMS 連線設定")]
    public string vgnmsBaseUrl  = "http://172.20.10.4/api/v1";
    //device的uuid
    public string vgnmsDeviceId = "019e7954-fa17-75cd-8f42-ac15eed0f402";
    public string vgnmsJwtToken = "";
    public int currentlyViewingDay = 0;
    
    public Transform rulerTransform;
    public Camera cam;

    void Awake() {
        instance = this;
    }

    public void RunPrediction() {
        List<XGBoostRequest> requests = GenerateInputSequence();

        foreach (ProceduralPlant p in generatedPlants)
            DestroyImmediate(p.gameObject);
        generatedPlants.Clear();
        heights.Clear();
        leafCounts.Clear();
        nodeCms.Clear();

        float currentHeight = 0f;
        int[] leafAppearDay = new int[] { -1, -1, -1, -1 };

        for (int i = 0; i < requests.Count; i++) {
            // 一次拿回「當天的高度增量」+「當天的葉片數」
            xg.XgPrediction pred = xg.instance.Predict(requests[i]);

            float growthCm  = Mathf.Max(0f, pred.GrowthCm);
            float nodeCm    = Mathf.Max(0f, pred.NodeCm);
            float leafFloat = Mathf.Max(0f, pred.LeafCount);
            if (leafCounts.Count > 0)
                leafFloat = Mathf.Max(leafFloat, leafCounts[leafCounts.Count - 1]);
            if (GUI.instance.selectedPlantType == 1)
            {
                leafFloat = Mathf.Clamp(leafFloat, 0f, 3f);
            }
            /*
            
            if (leafCounts.Count >= 2)
            {
                float yesterday = leafCounts[leafCounts.Count - 1];
                float dayBefore = leafCounts[leafCounts.Count - 2];
                
                // 只有連續兩天都比昨天高才升級，否則維持昨天
                if (leafFloat > yesterday && dayBefore >= yesterday)
                    leafFloat = leafFloat; // 升級
                else
                    leafFloat = yesterday; // 維持
            }
            else if (leafCounts.Count == 1)
            {
                float yesterday = leafCounts[leafCounts.Count - 1];
                if (leafFloat < yesterday)
                    leafFloat = yesterday;
            }
            */

            // 高度：累加
            currentHeight += growthCm;
            heights.Add(currentHeight);
            nodeCms.Add(nodeCm);

            // 葉子：存起來給 UI 用
            leafCounts.Add(leafFloat);

            // 把連續的葉子數轉成整數（0~4 片）
            int leafInt = Mathf.RoundToInt(leafFloat);
            // 小松菜最多允許 8 片，萵苣最多允許 3 片
            if (GUI.instance.selectedPlantType == 1)
            {
                leafInt = Mathf.Clamp(leafInt, 0, 3);
            }
            else
            {
                leafInt = Mathf.Clamp(leafInt, 0, 4);
            }

            GameObject newPlantObject = Instantiate(plantPrefab);
            newPlantObject.name = $"Plant {i}";
            newPlantObject.SetActive(i == 0);

            float growthProgress = Mathf.Max(0f, (float)(i - 4) / (requests.Count - 1));

            ProceduralPlant newPlant = newPlantObject.GetComponent<ProceduralPlant>();

            // ★★★ 關鍵：在 Generate 前設定葉片數 ★★★
            newPlant.subbranchCount = leafInt;
            // 記錄每片葉子第一次出現的天（i）
            for (int j = 0; j < leafInt && j < 4; j++)
                if (leafAppearDay[j] < 0) leafAppearDay[j] = i;

            // 計算每片葉子自己的成長進度（0→1）
            float[] leafAge = new float[leafInt];
            for (int j = 0; j < leafInt; j++)
                leafAge[j] = (leafAppearDay[j] < 0) ? 0f
                        : Mathf.Clamp01((float)(i - leafAppearDay[j]) / (requests.Count - 1));
            newPlant.leafAgeProgress = leafAge;
            // 依 GUI 選擇的植物種類設定外觀
            newPlant.plantType = (GUI.instance.selectedPlantType == 0)
                ? ProceduralPlant.PlantType.Komatsuna
                : ProceduralPlant.PlantType.Lettuce;
            newPlant.Generate(currentHeight, growthProgress,nodeCm);

            generatedPlants.Add(newPlant);
        }

        currentlyViewingDay = 0;
        GUI.instance.daySlider.value = 0;
        DisplayPlantAtDay(0);
        AdjustCamera();
        //StartCoroutine(UploadWateringScheduleToVGNMS());
    }

    private List<XGBoostRequest> GenerateInputSequence() {

        List<XGBoostRequest> requests = new List<XGBoostRequest>();

        int plantType = GUI.instance.selectedPlantType;
        
        // Days
        int dayCount        = int.Parse(GUI.instance.inputDayCount.text);
        GUI.instance.daySlider.maxValue = dayCount-1;

        // Temperature
        List<float> temperatures = new List<float>(0);
        string temperatureText = GUI.instance.inputTemperature.text;
        string[] subs = temperatureText.Split('\r','\n');
        foreach(string sub in subs)
        {
        string s = sub.Trim();

        // ★ Debug：看每一行實際長怎樣
        Debug.Log($"[Temperature] Parsing line: [{s}]");

        if (string.IsNullOrEmpty(s))
            continue;

        temperatures.Add(
            float.Parse(
                s,
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
        }

        // Light
        List<float> lights = new List<float>(0);
        string lightText = GUI.instance.inputLight.text;
        string[] LightSubs = lightText.Split('\r','\n');
        foreach(string sub in LightSubs)
            {
            string s = sub.Trim();

            // ★ Debug：看每一行實際長怎樣
            Debug.Log($"[Light] Parsing line: [{s}]");

            if (string.IsNullOrEmpty(s))
                continue;

            lights.Add(
                float.Parse(
                    s,
                    System.Globalization.CultureInfo.InvariantCulture
                )
            );
            }
        
        //humiduty
        List<float> humidities = new List<float>(0);
        string humidityText = GUI.instance.inputHumidity.text;
        string[] humiditySubs = humidityText.Split('\r','\n');
        foreach (string sub in humiditySubs)
        {
            string s = sub.Trim();

            // ★ Debug：看每一行實際長怎樣
            Debug.Log($"[humidities] Parsing line: [{s}]");

            if (string.IsNullOrEmpty(s))
                continue;

            humidities.Add(
                float.Parse(
                    s,
                    System.Globalization.CultureInfo.InvariantCulture
                )
            );
        }
        
        // ── 根據植物種類選擇澆水判斷邏輯 ──────────────────────────
        wateringDays.Clear();
        switch (plantType)
        {
            case 1:
                ComputeWateringDays_Plant1(dayCount, temperatures, lights, humidities);
                break;
            case 0:
            default:
                ComputeWateringDays_Plant0(dayCount, temperatures, lights, humidities);
                break;
        }
         // ── 逐天計算土壤水分並建立 request ──────────────────────────
        float remainingWaterAmount = 0f;
        float Wt_prev = 0f;

        for (int i = 0; i < dayCount; i++)
        {
            float temp     = temperatures[i];
            float hum      = humidities[i];
            float lightEff = lights[i];
            float Wt       = wateringDays.Contains(i) ? 1f : 0f;

            // 網室溫度修正
            float tempGreenhouse = 1.8808f * temp - 11.5245f;

            // ── 根據植物種類選擇土壤公式 ──────────────────────────────
            switch (plantType)
            {
                case 1:
                    remainingWaterAmount = UpdateSoilByFormula_Plant1(
                        remainingWaterAmount, tempGreenhouse, Wt_prev, hum);
                    break;
                case 0:
                default:
                    remainingWaterAmount = UpdateSoilByFormula_Plant0(
                        remainingWaterAmount, tempGreenhouse, Wt_prev, hum);
                    break;
            }

            requests.Add(new XGBoostRequest
            {
                Day         = i,
                Temperature = tempGreenhouse,
                Light       = lightEff,
                Humidity    = hum,
                Water       = remainingWaterAmount,
                PlantType   = plantType,
            });

            Wt_prev = Wt;
        }

        return requests;
    }
    // ════════════════════════════════════════════════════════════════
    //  澆水判斷 — Plant 0（小松菜，原有邏輯）
    // ════════════════════════════════════════════════════════════════
    private void ComputeWateringDays_Plant0(
        int dayCount,
        List<float> temperatures,
        List<float> lights,
        List<float> humidities)
    {
        float muT  = 23.7297f, sigT  = 4.5356f;
        float muS  = 7.9242f,  sigS  = 4.3978f;
        float muRH = 76.9036f, sigRH = 8.0764f;

        wateringDays.Add(0); // 第一天強制澆水
        float Sd = 0f;
        int da = 0;

        for (int i = 1; i < dayCount; i++)
        {
            float zT   = (temperatures[i] - muT)  / sigT;
            float zSun = (lights[i]        - muS)  / sigS;
            float zRH  = (humidities[i]    - muRH) / sigRH;

            float deltaSd = 0.356f * zT + 5.094f * zSun - 0.056f * zRH + 9.942f;
            Sd += deltaSd;

            int   d = i + 1;
            float C = 10f - 2.5f * da + 1.5f * deltaSd + 8f * (1f / d);

            if (Sd >= C)
            {
                wateringDays.Add(i);
                Sd = 0f;
                da = 0;
            }
            else
            {
                da++;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  澆水判斷 — Plant 1（新植物，請填入實際係數）
    // ════════════════════════════════════════════════════════════════
    private void ComputeWateringDays_Plant1(
        int dayCount,
        List<float> temperatures,
        List<float> lights,
        List<float> humidities)
    {
        // TODO：把下面的係數換成新植物的實際值
        float muT  = 17.35f, sigT  = 2.38673f;   // ← 替換
        float muS  = 6.94029f, sigS  = 3.31435f;   // ← 替換
        float muRH = 78.5667f, sigRH = 8.04439f;   // ← 替換

        wateringDays.Add(0);
        float Sd = 0f;
        int da = 0;

        for (int i = 1; i < dayCount; i++)
        {
            float zT   = (temperatures[i] - muT)  / sigT;
            float zSun = (lights[i]        - muS)  / sigS;
            float zRH  = (humidities[i]    - muRH) / sigRH;

            // TODO：把下面的係數換成新植物的實際值
            float deltaSd = 0.336f * zT - 3.584f * zSun + 2.728f * zRH + 4.886f; // ← 替換

            Sd += deltaSd;

            int   d = i + 1;
            // TODO：C 的計算邏輯若不同也在這裡替換
            float C = 7.12f - 1.95f * da + 1.08f * deltaSd + 6.35f * (1f / d);

            if (Sd >= C)
            {
                wateringDays.Add(i);
                Sd = 0f;
                da = 0;
            }
            else
            {
                da++;
            }
        }
    }
    // ════════════════════════════════════════════════════════════════
    //  土壤公式 — Plant 0（小松菜，原有公式）
    // ════════════════════════════════════════════════════════════════
    private float UpdateSoilByFormula_Plant0(
        float soilPrev, float temp, float Wt, float humidity)
    {
        float delta =
              31.67f
            + 2.07f  * temp
            - 83.11f * Wt
            - 0.97f  * humidity
            - 0.22f  * soilPrev;

        return Mathf.Clamp(soilPrev + delta, -200f, 500f);
    }

    // ════════════════════════════════════════════════════════════════
    //  土壤公式 — Plant 1（新植物，請填入實際係數）
    // ════════════════════════════════════════════════════════════════
    private float UpdateSoilByFormula_Plant1(
        float soilPrev, float temp, float Wt, float humidity)
    {
        // TODO：把下面的係數換成新植物的實際值
        float delta =
              29.98f          // 截距 ← 替換
            + 0.5f * temp
            - 6.89f * Wt
            - 0.298f * humidity
            - 0.31f * soilPrev;

        // TODO：Clamp 範圍也可視新植物情況調整
        return Mathf.Clamp(soilPrev + delta, -200f, 500f);
    }
        /*
        // 算哪一天要澆水
        float muT = 23.7297f, sigT = 4.5356f;
        float muS = 7.9242f,  sigS = 4.3978f;
        float muRH = 76.9036f, sigRH = 8.0764f;

        wateringDays.Clear();
        wateringDays.Add(0); // 第一天強制澆水
        float Sd = 0f;
        int da = 0; // 距上次澆水天數

        for (int i = 1; i < dayCount; i++)
        {
            float zT   = (temperatures[i] - muT) / sigT;
            float zSun = (lights[i]       - muS) / sigS;
            float zRH  = (humidities[i]   - muRH) / sigRH;

            float deltaSd = 0.356f * zT + 5.094f * zSun - 0.056f * zRH + 9.942f;
            Sd += deltaSd;

            int d = i + 1; // 第幾天（1-based）
            float C = 10f - 2.5f * da + 1.5f * deltaSd + 8f * (1f / d);

            if (Sd >= C)
            {
                wateringDays.Add(i);
                Sd = 0f;  // 澆水後 S_d 重置
                da = 0;
            }
            else
            {
                da++;
            }
        }
        */
        /*
        // Water
        int waterDuration       = int.Parse(GUI.instance.inputWaterDuration.text);
        int waterLevel          = int.Parse(GUI.instance.inputWaterLevel.text);
        int waterInterval1      = int.Parse(GUI.instance.inputWaterInterval1.text);
        int waterInterval2      = int.Parse(GUI.instance.inputWaterInterval2.text);

        // Generate the watering days
        List<int> wateringDays  = new List<int>(0);
        int next = 0;               // Day 1
        wateringDays.Add(next);

        next += waterInterval1;     // interval1 只用一次
        if (next < dayCount)
            wateringDays.Add(next);

        while (true)
        {
            next += waterInterval2;
            if (next >= dayCount) break;
            wateringDays.Add(next);
        }
        */
        /*
        // Temperorary variables
        float remainingWaterAmount = 0f;
        float Wt_prev = 0f;

        for (int i = 0; i < dayCount; i++)
        {
            float temp = temperatures[i];
            float hum  = humidities[i];
            float lightEff = lights[i];
            
            // Wt：看當天有無澆水（0或1）
            float Wt = wateringDays.Contains(i) ? 1f : 0f;

            /*
            // 澆水後加水量（固定值，你可以調整）
            if (wateringDays.Contains(i))
                remainingWaterAmount -= 55.39f;
            
            
            // 網室溫度修正
            float tempGreenhouse = 1.8808f * temp - 11.5245f;
            
            
            // 更新土壤水分
            remainingWaterAmount = UpdateSoilByFormula(
                remainingWaterAmount, tempGreenhouse, Wt_prev, hum
            );
            

            requests.Add(new XGBoostRequest
            {
                Day         = i,
                Temperature = tempGreenhouse,
                Light       = lightEff,
                Humidity    = hum,
                Water       = remainingWaterAmount,
                PlantType   = GUI.instance.selectedPlantType, 
            });
            Wt_prev=Wt;
        }
        
        return requests;

    }*/
    
    /*
    private float ComputeLight(float lightHours, float wx01, float rainPercent)
    {
        // lightEff = lightHours * (Wx - 0.02*rain)
        float factor = wx01 - 0.02f * rainPercent;

        // 保護：避免負光照
        if (factor < 0f) factor = 0f;

        return lightHours * factor;
    }
    */


/*    private float UpdateSoilByFormula(float soilPrev, float temp, float Wt, float humidity)
    {
        // Δsoil_t = 143.01 + 2.58*T - 5.42*UVI - 11.71*W - 0.87*Humidity - 0.6416*S_(t-1)
        // 這裡 W = Wx (0~1)
        float delta =
            31.67f
            + 2.07f   * temp
            - 83.11f  * Wt
            - 0.97f   * humidity
            - 0.22f   * soilPrev;

        float soilNow = soilPrev + delta;

        // 你的 water 範圍是 0~500
        return Mathf.Clamp(soilNow, -200f, 500f);
    }*/

    public void UpdateDaySlider() {
        currentlyViewingDay = (int)GUI.instance.daySlider.value;
        DisplayPlantAtDay(currentlyViewingDay);
    }

    public void DisplayPlantAtDay(int day0) {
        for (int i = 0; i < generatedPlants.Count; i++) {
            generatedPlants[i].gameObject.SetActive(i == day0);
        }

        Vector3 s = rulerTransform.localScale;
        s.y = heights[currentlyViewingDay];
        rulerTransform.localScale = s;

        Vector3 p = rulerTransform.position;
        p.y = s.y * .5f;
        rulerTransform.position = p;

        GUI.instance.textDay.text = $"Day {day0+1}";
        GUI.instance.textHeight.text = $"{GetHeightAtCurrentDay():F2} cm";
        GUI.instance.textNode.text = $"{GetNodeAtCurrentDay():F2} cm";
        GUI.instance.textWater.text = wateringDays.Contains(day0) ? "Water 1" : "Water 0";
        GUI.instance.textLeaf.text = $"{GetLeafAtCurrentDay():F0} leaves";
    }

    public float GetHeightAtCurrentDay() {
        return heights[currentlyViewingDay];
    }
    public float GetNodeAtCurrentDay() {
        return nodeCms[currentlyViewingDay];
    }
    

    public float GetLeafAtCurrentDay() {
    if (leafCounts == null || leafCounts.Count == 0)
        return 0f;

    int idx = Mathf.Clamp(currentlyViewingDay, 0, leafCounts.Count - 1);
    return leafCounts[idx];
    }

    void AdjustCamera() {
        float maxHeight = heights[heights.Count-1];
        float x = -25 - (maxHeight / 20f) * 8f;
        float y = maxHeight * .6f;
        cam.transform.position = new Vector3(x, y, 6f);
    }
    public void UploadSchedule() {
        if (wateringDays == null || wateringDays.Count == 0) {
            Debug.LogWarning("[VGNMS] 尚未執行預測，沒有澆水排程可上傳");
            return;
        }
        StartCoroutine(UploadWateringScheduleToVGNMS());
    }
    IEnumerator UploadWateringScheduleToVGNMS()
{
    string startDate = System.DateTime.Now.ToString("yyyy-MM-dd");
    int plantType    = GUI.instance.selectedPlantType;
    int dayCount     = int.Parse(GUI.instance.inputDayCount.text);
    string scheduleJson = "[" + string.Join(",", wateringDays) + "]";

    string json = "{"
        + $"\"start_date\":\"{startDate}\","
        + $"\"plant_type\":{plantType},"
        + $"\"day_count\":{dayCount},"
        + $"\"schedule\":{scheduleJson}"
        + "}";

    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
    string url     = $"{vgnmsBaseUrl}/devices/{vgnmsDeviceId}/watering-schedule";

    using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
    {
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(vgnmsJwtToken))
            req.SetRequestHeader("Authorization", "Bearer " + vgnmsJwtToken);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[VGNMS] 澆水排程上傳成功：{wateringDays.Count} 天，起始日 {startDate}");
        else
            Debug.LogError($"[VGNMS] 上傳失敗：{req.error}\n{req.downloadHandler.text}");
    }
}
    public void SetCameraZoom() {
        cam.fieldOfView = GUI.instance.fovSlider.value;
    }
}
