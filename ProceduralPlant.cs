using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ProceduralPlant : MonoBehaviour
{
    // ── 植物種類 ──
    public enum PlantType { Komatsuna, Lettuce }
    public PlantType plantType = PlantType.Komatsuna;
    public float[] leafAgeProgress;

    public MeshFilter mf;
    public GameObject subbranchPrefab;

    [Range(0.001f, 5f)]  public float branchRadius = .2f;
    [Range(0.001f, 50f)] public float branchLength = 2f;
    [Range(3, 12)]       public int radialSegments = 6;
    [Range(1, 20)]       public int heightSegments = 5;
    [Range(1, 8)]        public int subbranchCount = 2;

    private float baseBranchRadius;
    private float nodeHeight;
    private Vector3[] branchVertices;
    private int[] branchTriangles;
    private Vector3 subbranchRoot;
    public List<PlantBranch> subbranches;

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        subbranches = new List<PlantBranch>();
        baseBranchRadius = branchRadius;
    }

    public void Generate(float targetHeight, float growthProgress,float nodeCm)
    {
        transform.localScale = Vector3.one;
        growthProgress = Mathf.Clamp01(growthProgress);

        //branchLength = Mathf.Max(0.01f, targetHeight * 0.55f);
        nodeHeight   = nodeCm;
        branchLength = Mathf.Max(0.01f, nodeCm);

        float radiusScale = Mathf.Lerp(0.1f, 0.40f, growthProgress);
        branchRadius = baseBranchRadius * radiusScale;

        GenerateBranch();
        GenerateSubbranches(targetHeight, growthProgress);
        AdjustToHeight(targetHeight);
    }

    // ========= 主幹 =========
    void GenerateBranch()
    {
        int cylVertCount = (radialSegments + 1) * (heightSegments + 1);

        // 頂蓋需要多 1 個中心頂點
        branchVertices  = new Vector3[cylVertCount + 1];
        // 圓柱側面 + 頂蓋扇形三角形
        branchTriangles = new int[radialSegments * heightSegments * 6 + radialSegments * 3];

        for (int h = 0; h <= heightSegments; h++)
        {
            float y = branchLength / heightSegments * h;

            if (h == heightSegments)
                subbranchRoot = Vector3.up * y;

            for (int r = 0; r <= radialSegments; r++)
            {
                float angle = r * Mathf.PI * 2f / radialSegments;
                float x = branchRadius * Mathf.Cos(angle);
                float z = branchRadius * Mathf.Sin(angle);
                branchVertices[h * (radialSegments + 1) + r] = new Vector3(x, y, z);
            }
        }
        // 頂蓋中心頂點（主幹頂端中心）
        int capCenterIdx = cylVertCount;
        branchVertices[capCenterIdx] = subbranchRoot;
        int idx = 0;
        for (int h = 0; h < heightSegments; h++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int current = h * (radialSegments + 1) + r;
                int next    = current + radialSegments + 1;
                branchTriangles[idx++] = current;
                branchTriangles[idx++] = next;
                branchTriangles[idx++] = current + 1;
                branchTriangles[idx++] = next;
                branchTriangles[idx++] = next + 1;
                branchTriangles[idx++] = current + 1;
            }
        }
        // ── 頂蓋（扇形，朝上）──
        int topRingStart = heightSegments * (radialSegments + 1);
        for (int r = 0; r < radialSegments; r++)
        {
            branchTriangles[idx++] = capCenterIdx;
            branchTriangles[idx++] = topRingStart + r + 1; // r+1（最後一圈與 r=0 同位置）
            branchTriangles[idx++] = topRingStart + r;
        }

        Mesh mesh = new Mesh();
        mesh.vertices  = branchVertices;
        mesh.triangles = branchTriangles;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        // 主幹顏色
        var stemRenderer = GetComponent<MeshRenderer>();
        if (stemRenderer != null)
        {
            stemRenderer.material.color = (plantType == PlantType.Komatsuna)
                ? new Color(0.20f, 0.55f, 0.20f)   // 深綠
                : new Color(0.65f, 0.82f, 0.35f);   // 淺黃綠
        }
    }

    // ========= 子分支（葉柄＋葉片）=========
    /*void GenerateSubbranches(float targetHeight, float growthProgress)
    {
        foreach (var sb in subbranches)
            if (sb != null) Destroy(sb.gameObject);
        subbranches.Clear();

        float g = Mathf.Clamp01(growthProgress);
        float leafHeightLower = Mathf.Max(0.01f, nodeHeight*1.5f);               // 第一二片，固定參考主幹
        float leafHeightUpper = Mathf.Max(0.01f, (targetHeight - nodeHeight) * 0.6f); // 第三四片，隨總高成長

        for (int i = 0; i < subbranchCount; i++)
        {
            GameObject obj = Instantiate(subbranchPrefab);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = subbranchRoot - Vector3.up * (branchRadius * 0.5f);

            PlantBranch subbranch = obj.GetComponent<PlantBranch>();
            subbranch.parentPlant = this;
            subbranch.plantType   = plantType; // ← 傳遞種類

            float yAngle;
            if (subbranchCount == 3)
            {
                float[] angles3 = { 0f, 90f, 180f };
                yAngle = angles3[i];
            }
            else if (subbranchCount == 4)
            {
                // i=0,1 小葉 → 0,180；i=2,3 大葉 → 90,270
                float[] angles4 = { 0f, 180f, 90f, 270f };
                yAngle = angles4[i];
            }
            else
            {
                yAngle = 360f / subbranchCount * i;
            }
            obj.transform.localEulerAngles = new Vector3(0f, yAngle, 0f);
            float leafHeight = (subbranchCount >= 3 && i >= 2) ? leafHeightUpper : leafHeightLower;
            
            if (plantType == PlantType.Komatsuna)
            {
                // 小松菜：葉柄較長，向外展開
                float stemLen = Mathf.Clamp(leafHeight * 0.30f,
                                leafHeight * 0.05f,   // ← 改為相對比例
                                leafHeight * 0.50f);
                subbranch.branchLength     = stemLen;
                subbranch.branchBendFactor = 4.5f;
                subbranch.branchRadius = subbranch.baseBranchRadius * Mathf.Lerp(0.08f, 0.45f, g);
                float leafLen = stemLen * Mathf.Lerp(0.5f, 1.1f, g);
                subbranch.semiMajorAxis = leafLen;
                subbranch.semiMinorAxis = leafLen * 0.8f * 2f;
            }
            else
            {
                // 萵苣：葉柄極短，葉片較大且直立    // 葉柄很短
                float stemLen = Mathf.Clamp(leafHeight * 0.12f,
                                leafHeight * 0.03f,   // ← 改為相對比例
                                leafHeight * 0.25f);
                subbranch.branchLength     = stemLen;
                subbranch.branchBendFactor = 8.0f; // 葉柄近乎直
                subbranch.branchRadius = subbranch.baseBranchRadius * Mathf.Lerp(0.06f, 0.35f, g);
                // 萵苣葉片更大、更圓
                float leafLen = stemLen * Mathf.Lerp(1.2f, 2.5f, g);
                subbranch.semiMajorAxis = leafLen;
                subbranch.semiMinorAxis = leafLen * 1.0f * 2f; // 近圓形（長寬相近）
            }

            subbranches.Add(subbranch);
        }

        foreach (var sb in subbranches)
            sb.Generate();
    }*/
    void GenerateSubbranches(float targetHeight, float growthProgress)
{
    foreach (var sb in subbranches)
        if (sb != null) Destroy(sb.gameObject);
    subbranches.Clear();

    float g    = Mathf.Clamp01(growthProgress);
    float rise = Mathf.Max(0f, targetHeight - nodeHeight); // 需要爬升的高度

    for (int i = 0; i < subbranchCount; i++)
    {
        GameObject obj = Instantiate(subbranchPrefab);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = subbranchRoot - Vector3.up * (branchRadius * 0.5f);

        PlantBranch subbranch = obj.GetComponent<PlantBranch>();
        subbranch.parentPlant = this;
        subbranch.plantType   = plantType;
        
        // ── Y 軸旋轉角（與原本相同）──
        float yAngle;
        if (subbranchCount == 3)
        {
            float[] angles3 = { 0f, 180f, 90f };
            yAngle = angles3[i];
        }
        else if (subbranchCount == 4)
        {
            float[] angles4 = { 0f, 180f, 90f, 270f };
            yAngle = angles4[i];
        }
        else
        {
            yAngle = 360f / subbranchCount * i;
        }

        // ── 前兩片（i=0,1）略短略近；後兩片（i=2,3）完整高度 ──
        bool isSmallLeaf = (subbranchCount >= 3 && i < 2);
        float leafRise = isSmallLeaf ? nodeHeight * 1.2f : rise*0.55f;
        subbranch.leafTiltX = isSmallLeaf ? 4f : 20f;

        // ── 水平展開距離（可調整係數改變張開程度）──
        float spreadFactor = (plantType == PlantType.Komatsuna) ? 0.04f : 0.022f;
        float spreadBase = isSmallLeaf ? nodeHeight : targetHeight;
        float spread = spreadBase * spreadFactor * (isSmallLeaf ? 0.70f : 1.0f);
        // ── 從幾何關係推算葉柄長度與仰角 ──
        float stemLen = Mathf.Sqrt(leafRise * leafRise + spread * spread);
        float tiltX   = Mathf.Atan2(leafRise, spread) * Mathf.Rad2Deg;

        obj.transform.localEulerAngles = new Vector3(tiltX, yAngle, 0f);

        if (plantType == PlantType.Komatsuna)
        {
            float lg = (leafAgeProgress != null && i < leafAgeProgress.Length)
                ? leafAgeProgress[i] : g;
            float lgEff = (i == 3) ? Mathf.Pow(lg, 0.55f) : lg;  // 第四片長大速度加快
            float activeStemLen = isSmallLeaf ? stemLen : stemLen * Mathf.Lerp(0.1f, 1.0f, lgEff);
            subbranch.branchLength     = activeStemLen;
            subbranch.branchBendFactor = 4.5f;
            subbranch.branchRadius     = subbranch.baseBranchRadius * Mathf.Lerp(0.4f, 1.0f, g);
            float leafLen = isSmallLeaf
                ? stemLen       * Mathf.Lerp(0.9f, 1.2f, lg)
                : activeStemLen * Mathf.Lerp(0.4f, 1.0f, lgEff);
            subbranch.semiMajorAxis = leafLen;
            subbranch.semiMinorAxis = leafLen * 0.8f * 2f;
        }
        else
        {
            subbranch.branchLength     = stemLen;
            subbranch.branchBendFactor = 8.0f;
            subbranch.branchRadius     = subbranch.baseBranchRadius * Mathf.Lerp(0.3f, 0.8f, g);
            float leafLen = isSmallLeaf
                ? stemLen * Mathf.Lerp(0.4f, 0.8f, g)
                : stemLen * Mathf.Lerp(0.3f, 0.6f, g);
            subbranch.semiMajorAxis = leafLen;
            subbranch.semiMinorAxis = leafLen * 1.0f * 2f;
        }

        subbranches.Add(subbranch);
    }

    foreach (var sb in subbranches)
        sb.Generate();
}


    // ========= 依高度縮放整株 =========
    public void AdjustToHeight(float targetY)
    {
    if (targetY <= 0f) { transform.localScale = Vector3.one; return; }

    float maxY = 0f;
    foreach (PlantBranch sub in subbranches)
    {
        if (sub == null) continue;

        // 葉片
        var r1 = sub.GetComponent<Renderer>();
        if (r1 != null) maxY = Mathf.Max(maxY, r1.bounds.max.y);

        // 葉柄
        var r2 = sub.mf2 != null ? sub.mf2.GetComponent<Renderer>() : null;
        if (r2 != null) maxY = Mathf.Max(maxY, r2.bounds.max.y);
    }

    if (maxY <= 1e-4f) { transform.localScale = Vector3.one; return; }
    transform.localScale = Vector3.one * (targetY / maxY);
    }
}