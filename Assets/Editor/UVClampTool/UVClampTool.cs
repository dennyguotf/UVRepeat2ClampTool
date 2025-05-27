using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class UVClampTool : EditorWindow
{
    private GameObject modelObject;
    private string exportPath = "Assets/ExportedModels";
    private bool showAdvancedOptions = false;
    private float uvThreshold = 1.0f;
    private bool preserveOriginalMesh = true;

    [MenuItem("工具/UV重构工具")]
    public static void ShowWindow()
    {
        GetWindow<UVClampTool>("UV重构工具");
    }

    private void OnGUI()
    {
        GUILayout.Label("FBX模型UV重构工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("此工具可以导出FBX模型并重构其Mesh，将UV超过1的三角形细分成多个UV在1以内的三角形。", MessageType.Info);
        EditorGUILayout.Space();

        // 模型选择
        modelObject = EditorGUILayout.ObjectField("选择FBX模型", modelObject, typeof(GameObject), false) as GameObject;

        // 导出路径
        EditorGUILayout.BeginHorizontal();
        exportPath = EditorGUILayout.TextField("导出路径", exportPath);
        if (GUILayout.Button("浏览...", GUILayout.Width(80)))
        {
            string path = EditorUtility.SaveFolderPanel("选择导出文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                exportPath = path.Replace(Application.dataPath, "Assets");
            }
        }
        EditorGUILayout.EndHorizontal();

        // 高级选项
        showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "高级选项");
        if (showAdvancedOptions)
        {
            EditorGUI.indentLevel++;
            uvThreshold = EditorGUILayout.Slider("UV阈值", uvThreshold, 0.01f, 1.0f);
            preserveOriginalMesh = EditorGUILayout.Toggle("保留原始Mesh", preserveOriginalMesh);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 处理按钮
        GUI.enabled = modelObject != null;
        if (GUILayout.Button("处理并导出模型"))
        {
            ProcessAndExportModel();
        }
        GUI.enabled = true;
    }

    private void ProcessAndExportModel()
    {
        if (modelObject == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个FBX模型！", "确定");
            return;
        }

        // 确保导出目录存在
        if (!Directory.Exists(Path.Combine(Application.dataPath, exportPath.Replace("Assets/", ""))))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, exportPath.Replace("Assets/", "")));
        }

        // 创建模型副本
        GameObject processedModel = Instantiate(modelObject);
        processedModel.name = modelObject.name + "_Processed";

        // 处理所有Mesh
        MeshFilter[] meshFilters = processedModel.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshes = processedModel.GetComponentsInChildren<SkinnedMeshRenderer>();

        int processedMeshCount = 0;

        // 创建mesh资产的目录
        string meshFolderPath = Path.Combine(exportPath, "Meshes");
        if (!Directory.Exists(Path.Combine(Application.dataPath, meshFolderPath.Replace("Assets/", ""))))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, meshFolderPath.Replace("Assets/", "")));
        }

        // 处理MeshFilter组件
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh != null)
            {
                Mesh processedMesh = ProcessMesh(meshFilter.sharedMesh);
                
                if(processedMesh == null) 
                {
                    Debug.LogError("处理Mesh失败：" + meshFilter.gameObject.name);
                    continue;
                }

                // 保存处理后的mesh为资产
                string meshAssetPath = Path.Combine(meshFolderPath, meshFilter.gameObject.name + "_" + processedMesh.name + ".asset");
                AssetDatabase.CreateAsset(processedMesh, meshAssetPath);
                
                meshFilter.sharedMesh = processedMesh;
                processedMeshCount++;
            }
        }

        // 处理SkinnedMeshRenderer组件
        foreach (SkinnedMeshRenderer skinnedMesh in skinnedMeshes)
        {
            if (skinnedMesh.sharedMesh != null)
            {
                Mesh processedMesh = ProcessMesh(skinnedMesh.sharedMesh);
                
                // 保存处理后的mesh为资产
                string meshAssetPath = Path.Combine(meshFolderPath, skinnedMesh.gameObject.name + "_" + processedMesh.name + ".asset");
                AssetDatabase.CreateAsset(processedMesh, meshAssetPath);
                
                skinnedMesh.sharedMesh = processedMesh;
                processedMeshCount++;
            }
        }

        // 保存处理后的模型
        string prefabPath = Path.Combine(exportPath, processedModel.name + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(processedModel, prefabPath);
        DestroyImmediate(processedModel);

        EditorUtility.DisplayDialog("完成", $"已成功处理 {processedMeshCount} 个Mesh并导出到 {prefabPath}", "确定");
        AssetDatabase.Refresh();
    }

    private Mesh ProcessMesh(Mesh originalMesh)
    {
        //if(!originalMesh.name.Contains("Object11255"))
        //   return null;

        Mesh newMesh = new Mesh();
        if (preserveOriginalMesh)
        {
            // 创建原始网格的副本
            newMesh.name = originalMesh.name + "_Subdivided";
        }
        else
        {
            // 直接使用原始网格名称
            newMesh.name = originalMesh.name;
        }

        // 获取原始网格数据
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        Vector4[] tangents = originalMesh.tangents;
        Color[] colors = originalMesh.colors;
        Vector2[] uvs = originalMesh.uv;
        int[] triangles = originalMesh.triangles;

        // 用于存储新的网格数据
        List<Vector3> newVertices = new List<Vector3>(vertices);
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector4> newTangents = new List<Vector4>();
        List<Color> newColors = new List<Color>();
        List<Vector2> newUVs = new List<Vector2>();
        List<int> newTriangles = new List<int>();

        // 复制原始顶点数据
        if (normals.Length > 0) newNormals.AddRange(normals);
        if (tangents.Length > 0) newTangents.AddRange(tangents);
        if (colors.Length > 0) newColors.AddRange(colors);
        newUVs.AddRange(uvs);

        // 处理每个三角形
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int idx1 = triangles[i];
            int idx2 = triangles[i + 1];
            int idx3 = triangles[i + 2];

            Vector2 uv1 = uvs[idx1];
            Vector2 uv2 = uvs[idx2];
            Vector2 uv3 = uvs[idx3];

            // 检查是否有UV坐标超出范围
            bool needsSubdivision = uv1.x > uvThreshold || uv1.y > uvThreshold ||
                                   uv2.x > uvThreshold || uv2.y > uvThreshold ||
                                   uv3.x > uvThreshold || uv3.y > uvThreshold ||
                                   uv1.x < 0 || uv1.y < 0 ||
                                   uv2.x < 0 || uv2.y < 0 ||
                                   uv3.x < 0 || uv3.y < 0;

            if (needsSubdivision)
            {
                // 细分三角形
                SubdivideTriangle(
                    idx1, idx2, idx3,
                    normals.Length > 0 ? normals[idx1] : Vector3.zero,
                    normals.Length > 0 ? normals[idx2] : Vector3.zero,
                    normals.Length > 0 ? normals[idx3] : Vector3.zero,
                    tangents.Length > 0 ? tangents[idx1] : Vector4.zero,
                    tangents.Length > 0 ? tangents[idx2] : Vector4.zero,
                    tangents.Length > 0 ? tangents[idx3] : Vector4.zero,
                    colors.Length > 0 ? colors[idx1] : Color.white,
                    colors.Length > 0 ? colors[idx2] : Color.white,
                    colors.Length > 0 ? colors[idx3] : Color.white,
                    uv1, uv2, uv3,
                    newVertices, newNormals, newTangents, newColors, newUVs, newTriangles
                );
            }
            else
            {               
                // 保持原始三角形
                newTriangles.Add(idx1);
                newTriangles.Add(idx2);
                newTriangles.Add(idx3);
            }
        }

        FormatUV(newVertices, newNormals, newTangents, newColors, newUVs, newTriangles);

        // 设置新网格数据
        newMesh.SetVertices(newVertices);
        if (normals.Length > 0) newMesh.SetNormals(newNormals);
        if (tangents.Length > 0) newMesh.SetTangents(newTangents);
        if (colors.Length > 0) newMesh.SetColors(newColors);
        newMesh.SetUVs(0, newUVs);
        newMesh.SetTriangles(newTriangles, 0);

        // 重新计算边界
        newMesh.RecalculateBounds();
        
        // 如果原始网格没有法线或切线，则重新计算
        if (normals.Length == 0) newMesh.RecalculateNormals();
        if (tangents.Length == 0) newMesh.RecalculateTangents();

        return newMesh;
    }

    // 检查三个值是否差在1及以内
    public static bool AreValuesClose(float a, float b, float c)
    {
        float max = Mathf.Max(a, Mathf.Max(b, c));
        float min = Mathf.Min(a, Mathf.Min(b, c));

        return !TryFindIntegerBetween(min, max, out int result);
    }

    //尝试获取两个数之间的整数
    public static bool TryFindIntegerBetween(float a, float b, out int result)
    {
        float min = Mathf.Min(a, b);
        float max = Mathf.Max(a, b);

        // 计算潜在整数的边界
        int start = (int)Mathf.Floor(min) + 1;  // 比min大的最小整数
        int end = (int)Mathf.Ceil(max) - 1;  // 比max小的最大整数

        if (start <= end)
        {
            result = start;  // 返回第一个符合条件的整数
            return true;
        }

        result = 0;
        return false;
    }
    
    /// <summary>
    /// 拆分三角形
    /// </summary>
    /// <param name="idx1"></param>
    /// <param name="idx2"></param>
    /// <param name="idx3"></param>
    /// <param name="n1"></param>
    /// <param name="n2"></param>
    /// <param name="n3"></param>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <param name="t3"></param>
    /// <param name="c1"></param>
    /// <param name="c2"></param>
    /// <param name="c3"></param>
    /// <param name="uv1"></param>
    /// <param name="uv2"></param>
    /// <param name="uv3"></param>
    /// <param name="vertices"></param>
    /// <param name="normals"></param>
    /// <param name="tangents"></param>
    /// <param name="colors"></param>
    /// <param name="uvs"></param>
    /// <param name="triangles"></param>
    private void SubdivideTriangle(
        int idx1, int idx2, int idx3,
        Vector3 n1, Vector3 n2, Vector3 n3,
        Vector4 t1, Vector4 t2, Vector4 t3,
        Color c1, Color c2, Color c3,
        Vector2 uv1, Vector2 uv2, Vector2 uv3,
        List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents,
        List<Color> colors, List<Vector2> uvs, List<int> triangles)
    {
        //UnityEngine.Debug.Log($"{idx1}, {idx2} {idx3}: ({uv1.x:F8}, {uv1.y:F8}), ({uv2.x:F8}, {uv2.y:F8}), ({uv3.x:F8}, {uv3.y:F8})");

        // 获取原始顶点
        Vector3 v1 = vertices[idx1];
        Vector3 v2 = vertices[idx2];
        Vector3 v3 = vertices[idx3];

        Vector2 uv12 = Vector2.zero;
        Vector2 uv23 = Vector2.zero;
        Vector2 uv31 = Vector2.zero;

        //UV比例
        double uv12_x_ratio, uv12_y_ratio;
        double uv23_x_ratio, uv23_y_ratio;
        double uv31_x_ratio, uv31_y_ratio;

        double uv12_ratio = 0.0f;
        double uv23_ratio = 0.0f;
        double uv31_ratio = 0.0f;

        bool is_uv12 = false;
        bool is_uv23 = false;
        bool is_uv31 = false;

        int tmpValue;
        //处理12边
        if(TryFindIntegerBetween(uv1.x, uv2.x, out tmpValue))
        {
            is_uv12 = true;
            uv12.x = tmpValue;
            uv12_ratio = uv12_x_ratio = (uv12.x - uv1.x) / (uv2.x - uv1.x) ; 
            uv12.y = uv1.y + (float)((uv2.y - uv1.y) * uv12_ratio);     
        }
        else if(TryFindIntegerBetween(uv1.y, uv2.y, out tmpValue))
        {
            is_uv12 = true;
            uv12.y = tmpValue;
            uv12_ratio = uv12_y_ratio = (uv12.y - uv1.y) / (uv2.y - uv1.y) ;
            uv12.x = uv1.x + (float)((uv2.x - uv1.x) * uv12_ratio);  
        }        

        //处理23边
        if(TryFindIntegerBetween(uv2.x, uv3.x, out tmpValue))
        {
            is_uv23 = true;
            uv23.x = tmpValue;
            uv23_ratio = uv23_x_ratio = (uv23.x - uv2.x) / (uv3.x - uv2.x) ;
            uv23.y = uv2.y + (float)((uv3.y - uv2.y) * uv23_ratio);
        }
        else if(TryFindIntegerBetween(uv2.y, uv3.y, out tmpValue))
        {
            is_uv23 = true;
            uv23.y = tmpValue;
            uv23_ratio = uv23_y_ratio = (uv23.y - uv2.y) / (uv3.y - uv2.y) ;
            uv23.x = uv2.x + (float)((uv3.x - uv2.x) * uv23_ratio);
        }

        //处理31边
        if(TryFindIntegerBetween(uv3.x, uv1.x, out tmpValue))
        {
            is_uv31 = true;
            uv31.x = tmpValue;
            uv31_ratio = uv31_x_ratio = (uv31.x - uv3.x) / (uv1.x - uv3.x) ;
            uv31.y = uv3.y + (float)((uv1.y - uv3.y) * uv31_ratio);
        }
        else if(TryFindIntegerBetween(uv3.y, uv1.y, out tmpValue))
        {
            is_uv31 = true;
            uv31.y = tmpValue;
            uv31_ratio = uv31_y_ratio = (uv31.y - uv3.y) / (uv1.y - uv3.y) ;
            uv31.x = uv3.x + (float)((uv1.x - uv3.x) * uv31_ratio);
        }

        // 计算三角形各边的中点
        Vector3 v12 = v1 + new Vector3((float)((v2 - v1).x * uv12_ratio), (float)((v2 - v1).y * uv12_ratio), (float)((v2 - v1).z * uv12_ratio));
        Vector3 v23 = v2 + new Vector3((float)((v3 - v2).x * uv23_ratio), (float)((v3 - v2).y * uv23_ratio), (float)((v3 - v2).z * uv23_ratio));
        Vector3 v31 = v3 + new Vector3((float)((v1 - v3).x * uv31_ratio), (float)((v1 - v3).y * uv31_ratio), (float)((v1 - v3).z * uv31_ratio));

        // 计算中点的法线、切线、颜色和UV
        Vector3 n12 = Vector3.Normalize(n1 + new Vector3((float)((n2 - n1).x * uv12_ratio), (float)((n2 - n1).y * uv12_ratio), (float)((n2 - n1).z * uv12_ratio)));
        Vector3 n23 = Vector3.Normalize(n2 + new Vector3((float)((n3 - n2).x * uv23_ratio), (float)((n3 - n2).y * uv23_ratio), (float)((n3 - n2).z * uv23_ratio)));
        Vector3 n31 = Vector3.Normalize(n3 + new Vector3((float)((n1 - n3).x * uv31_ratio), (float)((n1 - n3).y * uv31_ratio), (float)((n1 - n3).z * uv31_ratio)));

        Vector4 t12 = t1 + new Vector4((float)((t2 - t1).x * uv12_ratio), (float)((t2 - t1).y * uv12_ratio), (float)((t2 - t1).z * uv12_ratio), (float)((t2 - t1).w * uv12_ratio));
        Vector4 t23 = t2 + new Vector4((float)((t3 - t2).x * uv23_ratio), (float)((t3 - t2).y * uv23_ratio), (float)((t3 - t2).z * uv23_ratio), (float)((t3 - t2).w * uv23_ratio));
        Vector4 t31 = t3 + new Vector4((float)((t1 - t3).x * uv31_ratio), (float)((t1 - t3).y * uv31_ratio), (float)((t1 - t3).z * uv31_ratio), (float)((t1 - t3).w * uv31_ratio));

        Color c12 = c1 + (c2 - c1) * (float)uv12_ratio;
        Color c23 = c2 + (c3 - c2) * (float)uv23_ratio;
        Color c31 = c3 + (c1 - c3) * (float)uv31_ratio;

        if (is_uv12 && is_uv23 && is_uv31)
        {
            //这里添加9个顶点，分给4个三角形
            // 三角形1: v1, v12, v31
            // 三角形2: v2, v23, v12
            // 三角形3: v3, v31, v23
            // 三角形4: v12, v23, v31 (中心三角形) 

            // 添加中点顶点
            int idx12_0 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_1 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_2 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx23_0 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_1 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_2 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx31_0 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_1 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_2 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            //判断4个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv12.x, uv31.x) && AreValuesClose(uv1.y, uv12.y, uv31.y);
            bool isV2Close = AreValuesClose(uv2.x, uv23.x, uv12.x) && AreValuesClose(uv2.y, uv23.y, uv12.y);
            bool isV3Close = AreValuesClose(uv3.x, uv31.x, uv23.x) && AreValuesClose(uv3.y, uv31.y, uv23.y);
            bool isCenterClose = AreValuesClose(uv12.x, uv23.x, uv31.x) && AreValuesClose(uv12.y, uv23.y, uv31.y);
            
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx12_1, idx31_1, n1, n12, n31, t1, t12, t31, c1, c12, c31, uv1, uv12, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v12, v31
                triangles.Add(idx1);
                triangles.Add(idx12_1);
                triangles.Add(idx31_1);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx23_1, idx12_2, n2, n23, n12, t2, t23, t12, c2, c23, c12, uv2, uv23, uv12, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v23, v12
                triangles.Add(idx2);
                triangles.Add(idx23_1);
                triangles.Add(idx12_2);
            }

            if (!isV3Close)
                SubdivideTriangle(idx3, idx31_2, idx23_2, n3, n31, n23, t3, t31, t23, c3, c31, c23, uv3, uv31, uv23, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形3: v3, v31, v23
                triangles.Add(idx3);
                triangles.Add(idx31_2);
                triangles.Add(idx23_2);
            }

            if (!isCenterClose)
                SubdivideTriangle(idx12_0, idx23_0, idx31_0, n12, n23, n31, t12, t23, t31, c12, c23, c31, uv12, uv23, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形4: v12, v23, v31 (中心三角形)
                triangles.Add(idx12_0);
                triangles.Add(idx23_0);
                triangles.Add(idx31_0);
            }
        }
        else if (is_uv12 && is_uv23 && !is_uv31)
        {
            //31边没有拆
            //这里添加5个顶点，分给3个三角形
            // 三角形1: v1, v12, v23
            // 三角形2: v2, v23, v12
            // 三角形3: v3, v1,  v23

            // 添加顶点
            int idx12_0 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_1 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);          

            int idx23_0 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_1 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_2 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);
            
            //判断3个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv12.x, uv23.x) && AreValuesClose(uv1.y, uv12.y, uv23.y);
            bool isV2Close = AreValuesClose(uv2.x, uv23.x, uv12.x) && AreValuesClose(uv2.y, uv23.y, uv12.y);
            bool isV3Close = AreValuesClose(uv3.x, uv1.x, uv23.x) && AreValuesClose(uv3.y, uv1.y, uv23.y);
            
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx12_0, idx23_0, n1, n12, n23, t1, t12, t23, c1, c12, c23, uv1, uv12, uv23, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v12, v23
                triangles.Add(idx1);
                triangles.Add(idx12_0);
                triangles.Add(idx23_0);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx23_1, idx12_1, n2, n23, n12, t2, t23, t12, c2, c23, c12, uv2, uv23, uv12, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v23, v12
                triangles.Add(idx2);
                triangles.Add(idx23_1);
                triangles.Add(idx12_1);
            }

            if (!isV3Close)
                SubdivideTriangle(idx3, idx1, idx23_2, n3, n1, n23, t3, t1, t23, c3, c1, c23, uv3, uv1, uv23, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形3: v3, v1,  v23
                triangles.Add(idx3);
                triangles.Add(idx1);
                triangles.Add(idx23_2);
            }
        }
        else if (is_uv12 && !is_uv23 && is_uv31)
        {
            //23边没有拆
            //这里添加5个顶点，分给3个三角形
            // 三角形1: v1, v12, v31
            // 三角形2: v2, v3, v12
            // 三角形3: v3, v31, v12

            // 添加中点顶点
            int idx12_0 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_1 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_2 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);            

            int idx31_0 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_1 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            //判断3个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv12.x, uv31.x) && AreValuesClose(uv1.y, uv12.y, uv31.y);
            bool isV2Close = AreValuesClose(uv2.x, uv3.x, uv12.x) && AreValuesClose(uv2.y, uv3.y, uv12.y);
            bool isV3Close = AreValuesClose(uv3.x, uv31.x, uv12.x) && AreValuesClose(uv3.y, uv31.y, uv12.y);
            
            // 如果3个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx12_0, idx31_0, n1, n12, n31, t1, t12, t31, c1, c12, c31, uv1, uv12, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v12, v31
                triangles.Add(idx1);
                triangles.Add(idx12_0);
                triangles.Add(idx31_0);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx3, idx12_1, n2, n3, n12, t2, t3, t12, c2, c3, c12, uv2, uv3, uv12, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v3, v12
                triangles.Add(idx2);
                triangles.Add(idx3);
                triangles.Add(idx12_1);
            }

            if (!isV3Close)
                SubdivideTriangle(idx3, idx31_1, idx12_2, n3, n31, n12, t3, t31, t12, c3, c31, c12, uv3, uv31, uv12, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形3: v3, v31, v12
                triangles.Add(idx3);
                triangles.Add(idx31_1);
                triangles.Add(idx12_2);
            }            
        }
        else if (!is_uv12 && is_uv23 && is_uv31)
        {
            //12边没有拆
            //这里添加5个顶点，分给3个三角形
            // 三角形1: v1, v2,  v31
            // 三角形2: v2, v23, v31
            // 三角形3: v3, v31, v23

            // 添加顶点         
            int idx23_0 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_1 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx31_0 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_1 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_2 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);
               
            //判断3个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv2.x, uv31.x) && AreValuesClose(uv1.y, uv2.y, uv31.y);
            bool isV2Close = AreValuesClose(uv2.x, uv23.x, uv31.x) && AreValuesClose(uv2.y, uv23.y, uv31.y);
            bool isV3Close = AreValuesClose(uv3.x, uv31.x, uv23.x) && AreValuesClose(uv3.y, uv31.y, uv23.y);
           
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx2, idx31_0, n1, n2, n31, t1, t2, t31, c1, c2, c31, uv1, uv2, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v2,  v31
                triangles.Add(idx1);
                triangles.Add(idx2);
                triangles.Add(idx31_0);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx23_0, idx31_1, n2, n23, n31, t2, t23, t12, c2, c23, c31, uv2, uv23, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v23, v31
                triangles.Add(idx2);
                triangles.Add(idx23_0);
                triangles.Add(idx31_1);
            }

            if (!isV3Close)
                SubdivideTriangle(idx3, idx31_2, idx23_1, n3, n31, n23, t3, t31, t23, c3, c31, c23, uv3, uv31, uv23, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形3: v3, v31, v23
                triangles.Add(idx3);
                triangles.Add(idx31_2);
                triangles.Add(idx23_1);
            }
            
        }
        else if (!is_uv12 && !is_uv23 && is_uv31)
        {
            //这里添加2个顶点，分给2个三角形
            // 三角形1: v1, v2, v31
            // 三角形2: v2, v3, v31

            // 添加中点顶点 
            int idx31_0 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);

            int idx31_1 = vertices.Count;
            vertices.Add(v31);
            normals.Add(n31);
            tangents.Add(t31);
            colors.Add(c31);
            uvs.Add(uv31);            

            //判断2个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv2.x, uv31.x) && AreValuesClose(uv1.y, uv2.y, uv31.y);
            bool isV2Close = AreValuesClose(uv2.x, uv3.x, uv31.x) && AreValuesClose(uv2.y, uv3.y, uv31.y);
           
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx2, idx31_0, n1, n2, n31, t1, t2, t31, c1, c2, c31, uv1, uv2, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v2, v31
                triangles.Add(idx1);
                triangles.Add(idx2);
                triangles.Add(idx31_0);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx3, idx31_1, n2, n3, n31, t2, t3, t31, c2, c3, c31, uv2, uv3, uv31, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v3, v31
                triangles.Add(idx2);
                triangles.Add(idx3);
                triangles.Add(idx31_1);
            }
           
        }
        else if (!is_uv12 && is_uv23 && !is_uv31)
        {
            //这里添加2个顶点，分给2个三角形
            // 三角形1: v1, v2, v23
            // 三角形2: v1, v23, v3

            // 添加中点顶点 
            int idx23_0 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);

            int idx23_1 = vertices.Count;
            vertices.Add(v23);
            normals.Add(n23);
            tangents.Add(t23);
            colors.Add(c23);
            uvs.Add(uv23);            

            //判断2个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv2.x, uv23.x) && AreValuesClose(uv1.y, uv2.y, uv23.y);
            bool isV2Close = AreValuesClose(uv1.x, uv23.x, uv3.x) && AreValuesClose(uv1.y, uv23.y, uv3.y);
           
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx2, idx23_0, n1, n2, n23, t1, t2, t23, c1, c2, c23, uv1, uv2, uv23, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v2, v23
                triangles.Add(idx1);
                triangles.Add(idx2);
                triangles.Add(idx23_0);
            }

            if (!isV2Close)
                SubdivideTriangle(idx1, idx23_1, idx3, n1, n23, n3, t1, t23, t3, c1, c23, c3, uv1, uv23, uv3, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v1, v23, v3
                triangles.Add(idx1);
                triangles.Add(idx23_1);
                triangles.Add(idx3);
            }          
        }
        else if (is_uv12 && !is_uv23 && !is_uv31)
        {
            //这里添加2个顶点，分给2个三角形
            // 三角形1: v1, v12, v3
            // 三角形2: v2, v3, v12

            // 添加中点顶点 
            int idx12_0 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);

            int idx12_1 = vertices.Count;
            vertices.Add(v12);
            normals.Add(n12);
            tangents.Add(t12);
            colors.Add(c12);
            uvs.Add(uv12);
          
            //判断2个三角形UV是不是可以格式化到1里面
            bool isV1Close = AreValuesClose(uv1.x, uv12.x, uv3.x) && AreValuesClose(uv1.y, uv12.y, uv3.y);
            bool isV2Close = AreValuesClose(uv2.x, uv3.x, uv12.x) && AreValuesClose(uv2.y, uv3.y, uv12.y);
          
            // 如果4个三角形的UV都不可以格式化到1里面，就继续细分
            if (!isV1Close)
                SubdivideTriangle(idx1, idx12_0, idx3, n1, n12, n3, t1, t12, t3, c1, c12, c3, uv1, uv12, uv3, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形1: v1, v12, v3
                triangles.Add(idx1);
                triangles.Add(idx12_0);
                triangles.Add(idx3);
            }

            if (!isV2Close)
                SubdivideTriangle(idx2, idx3, idx12_1, n2, n3, n12, t2, t3, t12, c2, c3, c12, uv2, uv3, uv12, vertices, normals, tangents, colors, uvs, triangles);
            else
            {
                // 三角形2: v2, v3, v12
                triangles.Add(idx2);
                triangles.Add(idx3);
                triangles.Add(idx12_1);
            }
        }
        else
        {
            //原三角形加回去
            triangles.Add(idx1);
            triangles.Add(idx2);
            triangles.Add(idx3);
        }
    }

    private void FormatUV(List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents,
        List<Color> colors, List<Vector2> uvs, List<int> triangles)
    {
        //遍历新三角形，如果顶有初多个三角形引用，则复制一个顶，并修改格式化UV
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int idx1 = triangles[i];
            int idx2 = triangles[i + 1];
            int idx3 = triangles[i + 2];
            Vector2 uv1 = uvs[idx1];
            Vector2 uv2 = uvs[idx2];
            Vector2 uv3 = uvs[idx3];

            // 检查是否有UV坐标超出范围
            bool needsFix = uv1.x > uvThreshold || uv1.y > uvThreshold ||
                                   uv2.x > uvThreshold || uv2.y > uvThreshold ||
                                   uv3.x > uvThreshold || uv3.y > uvThreshold ||
                                   uv1.x < 0 || uv1.y < 0 ||
                                   uv2.x < 0 || uv2.y < 0 ||
                                   uv3.x < 0 || uv3.y < 0;
            if (needsFix)
            {
                int idx1UseCount = 0;
                int idx2UseCount = 0;
                int idx3UseCount = 0;
                for(int j = 0; j < triangles.Count; j++)
                {
                    if(triangles[j] == idx1)
                        idx1UseCount++;
                    if(triangles[j] == idx2)
                        idx2UseCount++;
                    if(triangles[j] == idx3)
                        idx3UseCount++;
                }

                // 复制顶点
                if(idx1UseCount > 1)
                {
                    int idx_new = vertices.Count;
                    vertices.Add(vertices[idx1]);
                    normals.Add(normals[idx1]);
                    tangents.Add(tangents[idx1]);
                    colors.Add(colors[idx1]);
                    uvs.Add(uvs[idx1]);

                    triangles[i] = idx_new;
                }

                if(idx2UseCount > 1)
                {
                    int idx_new = vertices.Count;
                    vertices.Add(vertices[idx2]);
                    normals.Add(normals[idx2]);
                    tangents.Add(tangents[idx2]);
                    colors.Add(colors[idx2]);
                    uvs.Add(uvs[idx2]);

                    triangles[i + 1] = idx_new;
                }

                if(idx3UseCount > 1)
                {
                    int idx_new = vertices.Count;
                    vertices.Add(vertices[idx3]);
                    normals.Add(normals[idx3]);
                    tangents.Add(tangents[idx3]);
                    colors.Add(colors[idx3]);
                    uvs.Add(uvs[idx3]);
                    triangles[i + 2] = idx_new;
                }

                //正式开始修改UV
                idx1 = triangles[i];
                idx2 = triangles[i + 1];
                idx3 = triangles[i + 2];

                uv1 = uvs[idx1];
                uv2 = uvs[idx2];
                uv3 = uvs[idx3];
       
                if (uv1.x < 0 || uv2.x < 0 || uv3.x < 0)
                {
                    float min = Mathf.Min(uv1.x, Mathf.Min(uv2.x, uv3.x));
                    float minAbs = Mathf.Ceil(Mathf.Abs(min));
                    uv1.x += minAbs;
                    uv2.x += minAbs;
                    uv3.x += minAbs;
                } 
                else if(uv1.x > 0 || uv2.x > 0 || uv3.x > 0)
                {
                    float min = Mathf.Min(uv1.x, Mathf.Min(uv2.x, uv3.x));
                    float minAbs = Mathf.Floor(min);
                    uv1.x -= minAbs;
                    uv2.x -= minAbs;
                    uv3.x -= minAbs;
                } 

                if(uv1.y < 0 || uv2.y < 0 || uv3.y < 0)
                {
                    float min = Mathf.Min(uv1.y, Mathf.Min(uv2.y, uv3.y));
                    float minAbs = Mathf.Ceil(Mathf.Abs(min));
                    uv1.y += minAbs;
                    uv2.y += minAbs;
                    uv3.y += minAbs;
                } 
                else if(uv1.y > 0 || uv2.y > 0 || uv3.y > 0)
                {
                    float min = Mathf.Min(uv1.y, Mathf.Min(uv2.y, uv3.y));
                    float minAbs = Mathf.Floor(min);
                    uv1.y -= minAbs;
                    uv2.y -= minAbs;
                    uv3.y -= minAbs;
                }

                uvs[idx1] = uv1;
                uvs[idx2] = uv2;
                uvs[idx3] = uv3;
            }
        }
    }
}