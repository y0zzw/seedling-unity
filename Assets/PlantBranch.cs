using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PlantBranch : MonoBehaviour
{
    public ProceduralPlant parentPlant;

    public MeshFilter mf;
    public MeshFilter mf2;

    public float semiMajorAxis = 10f;
    public float semiMinorAxis = 3f;
    [Range(0.001f, 5f)] public float branchRadius = .2f;

    [HideInInspector] public float baseMajorAxis;
    [HideInInspector] public float baseMinorAxis;
    [HideInInspector] public float baseBranchRadius;

    public float fatnessFactor = 2f;
    public float maxBending = .05f;
    [Range(0f, .999f)] public float closureFactor = 1;
    [Range(0.001f, 20f)] public float branchLength = 2f;
    [Range(3, 12)] public int radialSegments = 6;
    [Range(1, 20)] public int heightSegments = 5;
    [Range(0.1f, 100f)] public float branchBendFactor = 1f;
    [Range(-1f, 1f)] public float branchYOffset = 0f;

    // 植物種類（由 ProceduralPlant 設定）
    [HideInInspector] public ProceduralPlant.PlantType plantType = ProceduralPlant.PlantType.Komatsuna;

    [HideInInspector] public float leafTiltX = 20f; // 葉片仰角（由 ProceduralPlant 設定）
    [SerializeField] private Vector3 growthCenter;

    private Vector3[] leafVertices, branchVertices;
    private int[] leafTriangles, branchTriangles;

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        baseMajorAxis    = semiMajorAxis;
        baseMinorAxis    = semiMinorAxis;
        baseBranchRadius = branchRadius;
    }

    public void Generate()
    {
        if (plantType == ProceduralPlant.PlantType.Komatsuna)
            GenerateKomatsunaLeaf();
        else
            GenerateLettuceLeaf();

        GenerateBranch();
        AdjustToGrowthCenter();
        ApplyMeshes();
    }

    // ===================== 小松菜葉片 =====================
    // 倒卵形，葉尖明顯，輕微鋸齒，向上翹曲
    void GenerateKomatsunaLeaf()
    {
        int seg = 24;
        float leafThickness = 0.09f;
        float length = semiMajorAxis;
        float width  = semiMinorAxis * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float y = t * length;

            // 倒卵形：峰值偏前段，尾端收尖
            float shape = Mathf.Sin(t * Mathf.PI);
            float skew  = Mathf.Pow(t, 0.55f);
            shape = Mathf.Lerp(shape, skew, 0.45f);
            shape = Mathf.Clamp01(shape);

            // 輕微鋸齒葉緣
            float edge = 1f + 0.06f * Mathf.Sin(t * Mathf.PI * 5f);
            float w = width * shape * edge;

            // 向上翹曲（外高內低）
            float bend = 0.18f * t * t * length;
            float half = leafThickness * 0.5f;

            verts.Add(new Vector3(-w, bend + half, y)); // 上左
            verts.Add(new Vector3( w, bend + half, y)); // 上右
            verts.Add(new Vector3( w, bend - half, y)); // 下右
            verts.Add(new Vector3(-w, bend - half, y)); // 下左
        }

        BuildLeafTriangles(seg, tris);
        leafVertices  = verts.ToArray();
        leafTriangles = tris.ToArray();
    }

    // ===================== 萵苣葉片 =====================
    // 接近圓形，幾乎無尖，大波浪葉緣，葉面較平略內凹
    void GenerateLettuceLeaf()
    {
        int seg = 32;
        float leafThickness = 0.09f;
        float length = semiMajorAxis;
        float width  = semiMinorAxis * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float y = t * length;

            // 近圓形：基部較寬，兩端都圓弧收
            float shape = Mathf.Sin(t * Mathf.PI);
            shape = Mathf.Lerp(0.4f, 1.0f, shape); // 基部不收太細
            shape = Mathf.Clamp01(shape);

            // 大波浪葉緣（萵苣特徵）
            float edge = 1f + 0.14f * Mathf.Sin(t * Mathf.PI * 4f);
            float w = width * shape * edge;

            // 輕微內凹（中央稍低，邊緣翹起）
            float bend = -0.04f * Mathf.Sin(t * Mathf.PI) * length;
            float half = leafThickness * 0.5f;

            verts.Add(new Vector3(-w, bend + half, y)); // 上左
            verts.Add(new Vector3( w, bend + half, y)); // 上右
            verts.Add(new Vector3( w, bend - half, y)); // 下右
            verts.Add(new Vector3(-w, bend - half, y)); // 下左
        }

        BuildLeafTriangles(seg, tris);
        leafVertices  = verts.ToArray();
        leafTriangles = tris.ToArray();
    }

    // ===================== 共用三角形建構 =====================
    void BuildLeafTriangles(int seg, List<int> tris)
    {
        // 上表面
        for (int i = 0; i < seg; i++)
        {
            int b = i * 4, n = b + 4;
            tris.Add(b);   tris.Add(n);   tris.Add(b+1);
            tris.Add(b+1); tris.Add(n);   tris.Add(n+1);
        }
        // 下表面
        for (int i = 0; i < seg; i++)
        {
            int b = i * 4, n = b + 4;
            tris.Add(b+3); tris.Add(b+2); tris.Add(n+2);
            tris.Add(b+3); tris.Add(n+2); tris.Add(n+3);
        }
        // 左側邊
        for (int i = 0; i < seg; i++)
        {
            int b = i * 4, n = b + 4;
            tris.Add(b);   tris.Add(b+3); tris.Add(n);
            tris.Add(n);   tris.Add(b+3); tris.Add(n+3);
        }
        // 右側邊
        for (int i = 0; i < seg; i++)
        {
            int b = i * 4, n = b + 4;
            tris.Add(b+1); tris.Add(n+1); tris.Add(b+2);
            tris.Add(b+2); tris.Add(n+1); tris.Add(n+2);
        }
        // 葉基端蓋
        tris.Add(0); tris.Add(1); tris.Add(3);
        tris.Add(1); tris.Add(2); tris.Add(3);
        // 葉尖端蓋
        int eb = seg * 4;
        tris.Add(eb);   tris.Add(eb+3); tris.Add(eb+1);
        tris.Add(eb+1); tris.Add(eb+3); tris.Add(eb+2);
    }

    // ===================== 葉柄生成 =====================
    void GenerateBranch()
    {
    int cylVertCount = (radialSegments + 1) * (heightSegments + 1);

    // 多 1 個底蓋中心頂點
    branchVertices  = new Vector3[cylVertCount + 1];
    branchTriangles = new int[radialSegments * heightSegments * 6 + radialSegments * 3];

    Vector3 prevReferencePoint = Vector3.zero;

    for (int h = 0; h <= heightSegments; h++)
    {
        float _z = -branchLength / heightSegments * h;
        float yOffset = (Mathf.Exp(-_z) - 1f) / branchBendFactor;
        float yPos    = branchYOffset + yOffset;

        Vector3 newReferencePoint = new Vector3(0f, yPos, _z);

        if (h == heightSegments)
            growthCenter = newReferencePoint;

        float bendAngle = Vector3.Angle(newReferencePoint - prevReferencePoint, Vector3.down);

        for (int r = 0; r <= radialSegments; r++)
        {
            float angle = r * Mathf.PI * 2f / radialSegments;
            float x = branchRadius * Mathf.Cos(angle);
            float z = branchRadius * Mathf.Sin(angle);
            Quaternion q = Quaternion.AngleAxis(bendAngle, Vector3.right);
            branchVertices[h * (radialSegments + 1) + r] = newReferencePoint + q * new Vector3(x, 0f, z);
        }

        prevReferencePoint = newReferencePoint;
    }

    // 底蓋中心頂點（h=0 的中心，就是葉柄根部）
    int capCenterIdx = cylVertCount;
    branchVertices[capCenterIdx] = new Vector3(0f, branchYOffset, 0f);

    int idx = 0;

    // 圓柱側面（原本）
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

    // 底蓋（朝下，三角形順序與頂蓋相反）
    for (int r = 0; r < radialSegments; r++)
    {
        branchTriangles[idx++] = capCenterIdx;
        branchTriangles[idx++] = r;          // h=0 這圈
        branchTriangles[idx++] = r + 1;
    }
    }

    // ===================== 對齊葉片到葉柄頂端 =====================
    /*void AdjustToGrowthCenter()
    {
        if (leafVertices == null || leafVertices.Length == 0) return;

        // 萵苣葉片較直立；小松菜較展開
        float tiltX = (plantType == ProceduralPlant.PlantType.Lettuce) ? 20f : 10f;
        Quaternion flipRotation = Quaternion.Euler(tiltX, 180f, 0f);

        for (int i = 0; i < leafVertices.Length; i++)
            leafVertices[i] = flipRotation * leafVertices[i];

        Vector3 finalOffset = growthCenter - leafVertices[0];
        for (int i = 0; i < leafVertices.Length; i++)
            leafVertices[i] += finalOffset;
    }*/
    void AdjustToGrowthCenter()
{
    if (leafVertices == null || leafVertices.Length == 0)
        return;

    // 葉片展開角度
    float tiltX = leafTiltX;

    // 葉柄方向（朝外）
    Vector3 outwardDir = growthCenter.normalized;

    if (outwardDir == Vector3.zero)
        outwardDir = Vector3.forward;

    // 讓葉片正面朝 outwardDir
    Quaternion faceOutward =
        Quaternion.LookRotation(outwardDir, Vector3.up);

    // 葉片微微上抬
    Quaternion tiltRotation =
        Quaternion.Euler(tiltX, 0f, 0f);

    // 最終旋轉
    Quaternion finalRotation =
        faceOutward * tiltRotation;

    // 套用旋轉
    for (int i = 0; i < leafVertices.Length; i++)
    {
        leafVertices[i] =
            finalRotation * leafVertices[i];
    }

    // 接到葉柄頂端
    Vector3 finalOffset =
        growthCenter - leafVertices[0];

    for (int i = 0; i < leafVertices.Length; i++)
    {
        leafVertices[i] += finalOffset;
    }
}


    // ===================== 套用 Mesh + 法線 + 顏色 =====================
    void ApplyMeshes()
    {
        // ── 葉片 ──
        Mesh mesh = new Mesh();
        mesh.vertices  = leafVertices;
        mesh.triangles = leafTriangles;

        // 法線跟葉片旋轉方向一致
        //float tiltX = (plantType == ProceduralPlant.PlantType.Lettuce) ? 15f : 20f;
        float tiltX = leafTiltX;
        Vector3 outwardDir = growthCenter.normalized;
        if (outwardDir == Vector3.zero) outwardDir = Vector3.forward;
        Quaternion faceOutward   = Quaternion.LookRotation(outwardDir, Vector3.up);
        Quaternion tiltRotation  = Quaternion.Euler(tiltX, 0f, 0f);
        Quaternion flipRotation  = faceOutward * tiltRotation;
        Vector3 leafUp   = flipRotation * Vector3.up;
        Vector3 leafDown = flipRotation * Vector3.down;

        int seg = (plantType == ProceduralPlant.PlantType.Lettuce) ? 32 : 24;
        Vector3[] normals = new Vector3[leafVertices.Length];
        for (int i = 0; i <= seg; i++)
        {
            normals[i * 4 + 0] = leafUp;
            normals[i * 4 + 1] = leafUp;
            normals[i * 4 + 2] = leafDown;
            normals[i * 4 + 3] = leafDown;
        }
        mesh.normals = normals;
        mf.mesh = mesh;

        // 葉片顏色：小松菜深綠、萵苣淺黃綠
        var leafRenderer = GetComponent<MeshRenderer>();
        if (leafRenderer != null)
        {
            leafRenderer.material.color = (plantType == ProceduralPlant.PlantType.Komatsuna)
                ? new Color(0.13f, 0.45f, 0.15f)
                : new Color(0.60f, 0.80f, 0.30f);
        }

        // ── 葉柄 ──
        mesh = new Mesh();
        mesh.vertices  = branchVertices;
        mesh.triangles = branchTriangles;
        mesh.RecalculateNormals();
        mf2.mesh = mesh;

        // 葉柄顏色：小松菜深綠莖、萵苣淺綠莖
        var stemRenderer = mf2.GetComponent<MeshRenderer>();
        if (stemRenderer != null)
        {
            stemRenderer.material.color = (plantType == ProceduralPlant.PlantType.Komatsuna)
                ? new Color(0.20f, 0.55f, 0.20f)
                : new Color(0.70f, 0.85f, 0.40f);
        }
    }
}