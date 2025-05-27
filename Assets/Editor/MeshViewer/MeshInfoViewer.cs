using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class MeshInfoViewer : EditorWindow
{
    private GameObject targetObject;
    private Vector2 scrollPosition;
    private bool showVertices = true;
    private bool showTriangles = true;
    private bool showUVs = true;
    private bool showNormals = true;
    private bool showTangents = true;
    private bool showColors = true;
    private bool showBounds = true;
    private bool showStatistics = true;
    
    private int selectedMeshIndex = 0;
    private List<Mesh> meshes = new List<Mesh>();
    private List<string> meshNames = new List<string>();
    
    private int vertexStartIndex = 0;
    private int vertexCount = 100;
    private int triangleStartIndex = 0;
    private int triangleCount = 100;
    
    private bool showUVSet1 = true;
    private bool showUVSet2 = false;
    private bool showUVSet3 = false;
    private bool showUVSet4 = false;
    
    private Color oddRowColor;
    private Color evenRowColor;
    
    // 顶点相关三角形查看功能
    private int selectedVertexIndex = -1;
    private bool showVertexTriangles = false;
    private List<int> relatedTriangles = new List<int>();
    private Vector2 vertexTrianglesScrollPosition;
    
    [MenuItem("工具/Mesh查看工具")]
    public static void ShowWindow()
    {
        GetWindow<MeshInfoViewer>("Mesh查看工具");
    }
    
    private void OnEnable()
    {
        oddRowColor = new Color(0.75f, 0.75f, 0.75f, 0.1f);
        evenRowColor = new Color(0.85f, 0.85f, 0.85f, 0.1f);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Mesh查看工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        GameObject newTarget = EditorGUILayout.ObjectField("目标对象", targetObject, typeof(GameObject), true) as GameObject;
        if (newTarget != targetObject)
        {
            targetObject = newTarget;
            RefreshMeshList();
        }
        
        // 添加重新加载按钮
        if (GUILayout.Button("重新加载", GUILayout.Width(80)))
        {
            RefreshMeshList();
        }
        EditorGUILayout.EndHorizontal();
        
        if (targetObject == null)
        {
            EditorGUILayout.HelpBox("请选择一个包含Mesh的GameObject", MessageType.Info);
            return;
        }
        
        if (meshes.Count == 0)
        {
            EditorGUILayout.HelpBox("所选对象不包含任何Mesh", MessageType.Warning);
            return;
        }
        
        // Mesh选择下拉菜单
        EditorGUILayout.BeginHorizontal();
        int newSelectedIndex = EditorGUILayout.Popup("选择Mesh", selectedMeshIndex, meshNames.ToArray());
        if (newSelectedIndex != selectedMeshIndex)
        {
            selectedMeshIndex = newSelectedIndex;
            // 重置顶点选择
            selectedVertexIndex = -1;
            showVertexTriangles = false;
            relatedTriangles.Clear();
        }
        EditorGUILayout.EndHorizontal();
        
        Mesh selectedMesh = meshes[selectedMeshIndex];
        
        // 显示选项
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(150));
        showStatistics = EditorGUILayout.ToggleLeft("统计信息", showStatistics);
        showBounds = EditorGUILayout.ToggleLeft("边界信息", showBounds);
        showVertices = EditorGUILayout.ToggleLeft("顶点", showVertices);
        showTriangles = EditorGUILayout.ToggleLeft("三角形", showTriangles);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(150));
        showUVs = EditorGUILayout.ToggleLeft("UV坐标", showUVs);
        showNormals = EditorGUILayout.ToggleLeft("法线", showNormals);
        showTangents = EditorGUILayout.ToggleLeft("切线", showTangents);
        showColors = EditorGUILayout.ToggleLeft("顶点颜色", showColors);
        EditorGUILayout.EndVertical();
        
        if (showUVs)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            showUVSet1 = EditorGUILayout.ToggleLeft("UV集 1", showUVSet1);
            showUVSet2 = EditorGUILayout.ToggleLeft("UV集 2", showUVSet2);
            showUVSet3 = EditorGUILayout.ToggleLeft("UV集 3", showUVSet3);
            showUVSet4 = EditorGUILayout.ToggleLeft("UV集 4", showUVSet4);
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 顶点相关三角形查询
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("顶点相关三角形查询", GUILayout.Width(150));
        int newVertexIndex = EditorGUILayout.IntField("顶点索引", selectedVertexIndex);
        
        if (newVertexIndex != selectedVertexIndex)
        {
            selectedVertexIndex = newVertexIndex;
            if (selectedVertexIndex >= 0 && selectedVertexIndex < selectedMesh.vertexCount)
            {
                FindRelatedTriangles(selectedMesh, selectedVertexIndex);
                showVertexTriangles = true;
            }
            else
            {
                showVertexTriangles = false;
                relatedTriangles.Clear();
            }
        }
        
        if (GUILayout.Button("查找", GUILayout.Width(60)))
        {
            if (selectedVertexIndex >= 0 && selectedVertexIndex < selectedMesh.vertexCount)
            {
                FindRelatedTriangles(selectedMesh, selectedVertexIndex);
                showVertexTriangles = true;
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "顶点索引无效，请输入0到" + (selectedMesh.vertexCount - 1) + "之间的值", "确定");
                showVertexTriangles = false;
                relatedTriangles.Clear();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 分页控制
        if (showVertices)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("顶点分页", GUILayout.Width(80));
            vertexStartIndex = EditorGUILayout.IntSlider("起始索引", vertexStartIndex, 0, Mathf.Max(0, selectedMesh.vertexCount - 1));
            vertexCount = EditorGUILayout.IntSlider("显示数量", vertexCount, 10, 1000);
            EditorGUILayout.EndHorizontal();
        }
        
        if (showTriangles)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("三角形分页", GUILayout.Width(80));
            triangleStartIndex = EditorGUILayout.IntSlider("起始索引", triangleStartIndex, 0, Mathf.Max(0, selectedMesh.triangles.Length / 3 - 1));
            triangleCount = EditorGUILayout.IntSlider("显示数量", triangleCount, 10, 1000);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space();
        
        // 显示顶点相关三角形信息
        if (showVertexTriangles && relatedTriangles.Count > 0)
        {
            DisplayVertexRelatedTriangles(selectedMesh);
        }
        
        // 滚动视图
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // 显示Mesh信息
        DisplayMeshInfo(selectedMesh);
        
        EditorGUILayout.EndScrollView();
    }
    
    private void FindRelatedTriangles(Mesh mesh, int vertexIndex)
    {
        relatedTriangles.Clear();
        
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (triangles[i] == vertexIndex || triangles[i + 1] == vertexIndex || triangles[i + 2] == vertexIndex)
            {
                relatedTriangles.Add(i / 3);
            }
        }
    }
    
    private void DisplayVertexRelatedTriangles(Mesh mesh)
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        
        EditorGUILayout.LabelField($"顶点 {selectedVertexIndex} 相关的三角形 ({relatedTriangles.Count}个)", headerStyle);
        
        // 获取顶点位置和UV
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        bool hasUVs = uvs != null && uvs.Length > 0;
        
        // 显示选中顶点的信息
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("选中顶点信息:", EditorStyles.boldLabel);
        
        if (selectedVertexIndex >= 0 && selectedVertexIndex < vertices.Length)
        {
            Vector3 vertexPos = vertices[selectedVertexIndex];
            EditorGUILayout.LabelField($"位置: ({vertexPos.x:F4}, {vertexPos.y:F4}, {vertexPos.z:F4})");
            
            if (hasUVs && selectedVertexIndex < uvs.Length)
            {
                Vector2 vertexUV = uvs[selectedVertexIndex];
                EditorGUILayout.LabelField($"UV: ({vertexUV.x:F4}, {vertexUV.y:F4})");
            }
        }
        
        EditorGUILayout.EndVertical();
        
        // 显示相关三角形列表
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("三角形索引", GUILayout.Width(80));
        EditorGUILayout.LabelField("顶点索引 (A, B, C)", GUILayout.Width(150));
        EditorGUILayout.LabelField("顶点位置", GUILayout.Width(200));
        
        if (hasUVs)
        {
            EditorGUILayout.LabelField("UV坐标", GUILayout.Width(150));
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 三角形数据
        vertexTrianglesScrollPosition = EditorGUILayout.BeginScrollView(vertexTrianglesScrollPosition, GUILayout.Height(200));
        
        int[] triangles = mesh.triangles;
        
        for (int i = 0; i < relatedTriangles.Count; i++)
        {
            int triangleIndex = relatedTriangles[i];
            int baseIdx = triangleIndex * 3;
            
            if (baseIdx + 2 < triangles.Length)
            {
                int idx1 = triangles[baseIdx];
                int idx2 = triangles[baseIdx + 1];
                int idx3 = triangles[baseIdx + 2];
                
                // 交替行颜色
                Rect rowRect = EditorGUILayout.BeginHorizontal();
                if (i % 2 == 0)
                    EditorGUI.DrawRect(rowRect, evenRowColor);
                else
                    EditorGUI.DrawRect(rowRect, oddRowColor);
                
                EditorGUILayout.LabelField(triangleIndex.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField($"({idx1}, {idx2}, {idx3})", GUILayout.Width(150));
                
                // 高亮显示选中的顶点
                string v1Display = idx1 == selectedVertexIndex ? $"<color=yellow>({vertices[idx1].x:F2}, {vertices[idx1].y:F2}, {vertices[idx1].z:F2})</color>" : 
                                                                $"({vertices[idx1].x:F2}, {vertices[idx1].y:F2}, {vertices[idx1].z:F2})";
                string v2Display = idx2 == selectedVertexIndex ? $"<color=yellow>({vertices[idx2].x:F2}, {vertices[idx2].y:F2}, {vertices[idx2].z:F2})</color>" : 
                                                                $"({vertices[idx2].x:F2}, {vertices[idx2].y:F2}, {vertices[idx2].z:F2})";
                string v3Display = idx3 == selectedVertexIndex ? $"<color=yellow>({vertices[idx3].x:F2}, {vertices[idx3].y:F2}, {vertices[idx3].z:F2})</color>" : 
                                                                $"({vertices[idx3].x:F2}, {vertices[idx3].y:F2}, {vertices[idx3].z:F2})";
                
                EditorGUILayout.LabelField($"{v1Display}, {v2Display}, {v3Display}", new GUIStyle(EditorStyles.label) { richText = true }, GUILayout.Width(400));
                
                if (hasUVs && idx1 < uvs.Length && idx2 < uvs.Length && idx3 < uvs.Length)
                {
                    // 高亮显示选中顶点的UV
                    string uv1Display = idx1 == selectedVertexIndex ? $"<color=yellow>({uvs[idx1].x:F2}, {uvs[idx1].y:F2})</color>" : 
                                                                    $"({uvs[idx1].x:F2}, {uvs[idx1].y:F2})";
                    string uv2Display = idx2 == selectedVertexIndex ? $"<color=yellow>({uvs[idx2].x:F2}, {uvs[idx2].y:F2})</color>" : 
                                                                    $"({uvs[idx2].x:F2}, {uvs[idx2].y:F2})";
                    string uv3Display = idx3 == selectedVertexIndex ? $"<color=yellow>({uvs[idx3].x:F2}, {uvs[idx3].y:F2})</color>" : 
                                                                    $"({uvs[idx3].x:F2}, {uvs[idx3].y:F2})";
                    
                    EditorGUILayout.LabelField($"{uv1Display}, {uv2Display}, {uv3Display}", new GUIStyle(EditorStyles.label) { richText = true }, GUILayout.Width(300));
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 添加一个按钮，可以跳转到三角形列表中的对应位置
                Rect buttonRect = GUILayoutUtility.GetLastRect();
                buttonRect.x = buttonRect.width - 60;
                buttonRect.width = 60;
                
                if (GUI.Button(buttonRect, "定位"))
                {
                    triangleStartIndex = Mathf.Max(0, triangleIndex - 5);
                    showTriangles = true;
                    scrollPosition = new Vector2(0, 1000); // 一个大的值，让它滚动到下面的三角形部分
                }
            }
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }
    
    private void RefreshMeshList()
    {
        meshes.Clear();
        meshNames.Clear();
        
        if (targetObject == null)
            return;
        
        // 重置索引和滚动位置
        selectedMeshIndex = 0;
        scrollPosition = Vector2.zero;
        vertexStartIndex = 0;
        triangleStartIndex = 0;
        
        // 重置顶点相关三角形查询
        selectedVertexIndex = -1;
        showVertexTriangles = false;
        relatedTriangles.Clear();
        
        // 获取MeshFilter组件
        MeshFilter[] meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter filter in meshFilters)
        {
            if (filter.sharedMesh != null)
            {
                meshes.Add(filter.sharedMesh);
                meshNames.Add($"{filter.gameObject.name} (MeshFilter)");
            }
        }
        
        // 获取SkinnedMeshRenderer组件
        SkinnedMeshRenderer[] skinnedMeshes = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedMeshes)
        {
            if (renderer.sharedMesh != null)
            {
                meshes.Add(renderer.sharedMesh);
                meshNames.Add($"{renderer.gameObject.name} (SkinnedMeshRenderer)");
            }
        }
        
        // 如果找到了Mesh，设置选中第一个
        selectedMeshIndex = meshes.Count > 0 ? 0 : -1;
        
        // 提示用户刷新完成
        if (meshes.Count > 0)
        {
            Debug.Log($"已重新加载 {meshes.Count} 个Mesh");
        }
        else if (targetObject != null)
        {
            Debug.LogWarning($"对象 '{targetObject.name}' 不包含任何Mesh");
        }
        
        // 强制重绘窗口
        Repaint();
    }
    
    private void DisplayMeshInfo(Mesh mesh)
    {
        if (mesh == null)
            return;
        
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        
        GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
        subHeaderStyle.fontSize = 12;
        
        // 统计信息 - 多列显示
        if (showStatistics)
        {
            EditorGUILayout.LabelField("统计信息", headerStyle);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 第一行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"名称: {mesh.name}", GUILayout.Width(300));
            EditorGUILayout.LabelField($"顶点数量: {mesh.vertexCount}", GUILayout.Width(200));
            EditorGUILayout.LabelField($"三角形数量: {mesh.triangles.Length / 3}", GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            // 第二行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"子网格数量: {mesh.subMeshCount}", GUILayout.Width(300));
            EditorGUILayout.LabelField($"顶点索引格式: {mesh.indexFormat}", GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            bool hasUV = mesh.uv != null && mesh.uv.Length > 0;
            bool hasUV2 = mesh.uv2 != null && mesh.uv2.Length > 0;
            bool hasUV3 = mesh.uv3 != null && mesh.uv3.Length > 0;
            bool hasUV4 = mesh.uv4 != null && mesh.uv4.Length > 0;
            bool hasNormals = mesh.normals != null && mesh.normals.Length > 0;
            bool hasTangents = mesh.tangents != null && mesh.tangents.Length > 0;
            bool hasColors = mesh.colors != null && mesh.colors.Length > 0;
            
            // 第三行 - UV信息
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"包含UV集1: {hasUV}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"包含UV集2: {hasUV2}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"包含UV集3: {hasUV3}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"包含UV集4: {hasUV4}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            // 第四行 - 其他顶点属性
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"包含法线: {hasNormals}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"包含切线: {hasTangents}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"包含顶点颜色: {hasColors}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        // 边界信息 - 多列显示
        if (showBounds)
        {
            EditorGUILayout.LabelField("边界信息", headerStyle);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            Bounds bounds = mesh.bounds;
            
            // 第一行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"中心点: ({bounds.center.x:F4}, {bounds.center.y:F4}, {bounds.center.z:F4})", GUILayout.Width(300));
            EditorGUILayout.LabelField($"大小: ({bounds.size.x:F4}, {bounds.size.y:F4}, {bounds.size.z:F4})", GUILayout.Width(300));
            EditorGUILayout.EndHorizontal();
            
            // 第二行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"最小点: ({bounds.min.x:F4}, {bounds.min.y:F4}, {bounds.min.z:F4})", GUILayout.Width(300));
            EditorGUILayout.LabelField($"最大点: ({bounds.max.x:F4}, {bounds.max.y:F4}, {bounds.max.z:F4})", GUILayout.Width(300));
            EditorGUILayout.EndHorizontal();
            
            // 第三行 - 体积和表面积
            EditorGUILayout.BeginHorizontal();
            float volume = bounds.size.x * bounds.size.y * bounds.size.z;
            float surfaceArea = 2 * (bounds.size.x * bounds.size.y + bounds.size.x * bounds.size.z + bounds.size.y * bounds.size.z);
            EditorGUILayout.LabelField($"体积: {volume:F4}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"表面积: {surfaceArea:F4}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        // 子网格信息 - 多列显示
        EditorGUILayout.LabelField("子网格信息", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("索引", GUILayout.Width(50));
        EditorGUILayout.LabelField("索引起始位置", GUILayout.Width(100));
        EditorGUILayout.LabelField("索引数量", GUILayout.Width(100));
        EditorGUILayout.LabelField("顶点起始位置", GUILayout.Width(100));
        EditorGUILayout.LabelField("顶点数量", GUILayout.Width(100));
        EditorGUILayout.LabelField("拓扑结构", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
        
        // 子网格数据
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            UnityEngine.Rendering.SubMeshDescriptor desc = mesh.GetSubMesh(i);
            
            // 交替行颜色
            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (i % 2 == 0)
                EditorGUI.DrawRect(rowRect, evenRowColor);
            else
                EditorGUI.DrawRect(rowRect, oddRowColor);
            
            EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(50));
            EditorGUILayout.LabelField(desc.indexStart.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField(desc.indexCount.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField(desc.baseVertex.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField(desc.vertexCount.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField(desc.topology.ToString(), GUILayout.Width(150));
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        
        // 顶点信息
        if (showVertices)
        {
            EditorGUILayout.LabelField("顶点信息", headerStyle);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Color[] colors = mesh.colors;
            Vector2[] uvs = mesh.uv;
            Vector2[] uvs2 = mesh.uv2;
            Vector2[] uvs3 = mesh.uv3;
            Vector2[] uvs4 = mesh.uv4;
            
            bool hasNormals = normals != null && normals.Length > 0;
            bool hasTangents = tangents != null && tangents.Length > 0;
            bool hasColors = colors != null && colors.Length > 0;
            bool hasUVs = uvs != null && uvs.Length > 0;
            bool hasUVs2 = uvs2 != null && uvs2.Length > 0;
            bool hasUVs3 = uvs3 != null && uvs3.Length > 0;
            bool hasUVs4 = uvs4 != null && uvs4.Length > 0;
            
            // 表头
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("索引", GUILayout.Width(50));
            EditorGUILayout.LabelField("位置 (X, Y, Z)", GUILayout.Width(200));
            
            if (showNormals && hasNormals)
                EditorGUILayout.LabelField("法线 (X, Y, Z)", GUILayout.Width(200));
                
            if (showTangents && hasTangents)
                EditorGUILayout.LabelField("切线 (X, Y, Z, W)", GUILayout.Width(200));
                
            if (showColors && hasColors)
                EditorGUILayout.LabelField("颜色 (R, G, B, A)", GUILayout.Width(200));
                
            if (showUVs)
            {
                if (showUVSet1 && hasUVs)
                    EditorGUILayout.LabelField("UV1 (U, V)", GUILayout.Width(150));
                    
                if (showUVSet2 && hasUVs2)
                    EditorGUILayout.LabelField("UV2 (U, V)", GUILayout.Width(150));
                    
                if (showUVSet3 && hasUVs3)
                    EditorGUILayout.LabelField("UV3 (U, V)", GUILayout.Width(150));
                    
                if (showUVSet4 && hasUVs4)
                    EditorGUILayout.LabelField("UV4 (U, V)", GUILayout.Width(150));
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 顶点数据
            int endIndex = Mathf.Min(vertexStartIndex + vertexCount, vertices.Length);
            for (int i = vertexStartIndex; i < endIndex; i++)
            {
                // 交替行颜色
                Rect rowRect = EditorGUILayout.BeginHorizontal();
                if (i % 2 == 0)
                    EditorGUI.DrawRect(rowRect, evenRowColor);
                else
                    EditorGUI.DrawRect(rowRect, oddRowColor);
                
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField($"({vertices[i].x:F4}, {vertices[i].y:F4}, {vertices[i].z:F4})", GUILayout.Width(200));
                
                if (showNormals && hasNormals && i < normals.Length)
                    EditorGUILayout.LabelField($"({normals[i].x:F4}, {normals[i].y:F4}, {normals[i].z:F4})", GUILayout.Width(200));
                    
                if (showTangents && hasTangents && i < tangents.Length)
                    EditorGUILayout.LabelField($"({tangents[i].x:F4}, {tangents[i].y:F4}, {tangents[i].z:F4}, {tangents[i].w:F4})", GUILayout.Width(200));
                    
                if (showColors && hasColors && i < colors.Length)
                    EditorGUILayout.LabelField($"({colors[i].r:F4}, {colors[i].g:F4}, {colors[i].b:F4}, {colors[i].a:F4})", GUILayout.Width(200));
                    
                if (showUVs)
                {
                    if (showUVSet1 && hasUVs && i < uvs.Length)
                        EditorGUILayout.LabelField($"({uvs[i].x:F4}, {uvs[i].y:F4})", GUILayout.Width(150));
                        
                    if (showUVSet2 && hasUVs2 && i < uvs2.Length)
                        EditorGUILayout.LabelField($"({uvs2[i].x:F4}, {uvs2[i].y:F4})", GUILayout.Width(150));
                        
                    if (showUVSet3 && hasUVs3 && i < uvs3.Length)
                        EditorGUILayout.LabelField($"({uvs3[i].x:F4}, {uvs3[i].y:F4})", GUILayout.Width(150));
                        
                    if (showUVSet4 && hasUVs4 && i < uvs4.Length)
                        EditorGUILayout.LabelField($"({uvs4[i].x:F4}, {uvs4[i].y:F4})", GUILayout.Width(150));
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        // 三角形信息
        if (showTriangles)
        {
            EditorGUILayout.LabelField("三角形信息", headerStyle);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            
            bool hasUVs = uvs != null && uvs.Length > 0;
            
            // 表头
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("索引", GUILayout.Width(50));
            EditorGUILayout.LabelField("顶点索引 (A, B, C)", GUILayout.Width(150));
            EditorGUILayout.LabelField("顶点位置 A", GUILayout.Width(200));
            EditorGUILayout.LabelField("顶点位置 B", GUILayout.Width(200));
            EditorGUILayout.LabelField("顶点位置 C", GUILayout.Width(200));
            
            if (showUVs && hasUVs)
            {
                EditorGUILayout.LabelField("UV A", GUILayout.Width(150));
                EditorGUILayout.LabelField("UV B", GUILayout.Width(150));
                EditorGUILayout.LabelField("UV C", GUILayout.Width(150));
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 三角形数据
            int triangleCount = triangles.Length / 3;
            int endIndex = Mathf.Min(triangleStartIndex + this.triangleCount, triangleCount);
            
            for (int i = triangleStartIndex; i < endIndex; i++)
            {
                int baseIdx = i * 3;
                int idx1 = triangles[baseIdx];
                int idx2 = triangles[baseIdx + 1];
                int idx3 = triangles[baseIdx + 2];
                
                // 交替行颜色
                Rect rowRect = EditorGUILayout.BeginHorizontal();
                if (i % 2 == 0)
                    EditorGUI.DrawRect(rowRect, evenRowColor);
                else
                    EditorGUI.DrawRect(rowRect, oddRowColor);
                
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField($"({idx1}, {idx2}, {idx3})", GUILayout.Width(150));
                
                if (idx1 < vertices.Length && idx2 < vertices.Length && idx3 < vertices.Length)
                {
                    Vector3 v1 = vertices[idx1];
                    Vector3 v2 = vertices[idx2];
                    Vector3 v3 = vertices[idx3];
                    
                    EditorGUILayout.LabelField($"({v1.x:F4}, {v1.y:F4}, {v1.z:F4})", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"({v2.x:F4}, {v2.y:F4}, {v2.z:F4})", GUILayout.Width(200));
                    EditorGUILayout.LabelField($"({v3.x:F4}, {v3.y:F4}, {v3.z:F4})", GUILayout.Width(200));
                    
                    if (showUVs && hasUVs && idx1 < uvs.Length && idx2 < uvs.Length && idx3 < uvs.Length)
                    {
                        Vector2 uv1 = uvs[idx1];
                        Vector2 uv2 = uvs[idx2];
                        Vector2 uv3 = uvs[idx3];
                        
                        EditorGUILayout.LabelField($"({uv1.x:F4}, {uv1.y:F4})", GUILayout.Width(150));
                        EditorGUILayout.LabelField($"({uv2.x:F4}, {uv2.y:F4})", GUILayout.Width(150));
                        EditorGUILayout.LabelField($"({uv3.x:F4}, {uv3.y:F4})", GUILayout.Width(150));
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}