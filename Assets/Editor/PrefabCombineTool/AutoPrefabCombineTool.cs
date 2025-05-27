using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AutoPrefabCombineTool : EditorWindow
{
    private GameObject selectedPrefab;
    private string outputName = "";
    private string outputPath = "Assets/CombinedPrefabs";
    private int atlasSize = 2048;
    private bool autoNaming = true;
    private bool showAdvancedSettings = false;
    
    // 自动配置的设置
    private bool combineTextures = true;
    private bool optimizeMesh = true;
    private bool generateLightmapUVs = true;
    private float uvPadding = 2f;

    [MenuItem("工具/一键Prefab合屏工具")]
    public static void ShowWindow()
    {
        AutoPrefabCombineTool window = GetWindow<AutoPrefabCombineTool>("一键Prefab合屏工具");
        window.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        // 检查当前选中的对象是否为prefab
        if (Selection.activeObject != null && PrefabUtility.GetPrefabAssetType(Selection.activeObject) != PrefabAssetType.NotAPrefab)
        {
            selectedPrefab = Selection.activeObject as GameObject;
            if (autoNaming && selectedPrefab != null)
            {
                outputName = selectedPrefab.name + "_Combined";
            }
        }
    }

    private void OnSelectionChange()
    {
        // 当选择改变时自动更新
        if (Selection.activeObject != null && PrefabUtility.GetPrefabAssetType(Selection.activeObject) != PrefabAssetType.NotAPrefab)
        {
            selectedPrefab = Selection.activeObject as GameObject;
            if (autoNaming && selectedPrefab != null)
            {
                outputName = selectedPrefab.name + "_Combined";
            }
            Repaint();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("一键Prefab合屏工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("选择一个Prefab，工具将自动合并其所有子对象的网格和纹理。", MessageType.Info);
        EditorGUILayout.Space();

        // Prefab选择
        EditorGUILayout.LabelField("Prefab选择", EditorStyles.boldLabel);
        GameObject newPrefab = EditorGUILayout.ObjectField("目标Prefab", selectedPrefab, typeof(GameObject), false) as GameObject;
        
        if (newPrefab != selectedPrefab)
        {
            selectedPrefab = newPrefab;
            if (autoNaming && selectedPrefab != null)
            {
                outputName = selectedPrefab.name + "_Combined";
            }
        }

        // 验证选择的对象是否为prefab
        if (selectedPrefab != null && PrefabUtility.GetPrefabAssetType(selectedPrefab) == PrefabAssetType.NotAPrefab)
        {
            EditorGUILayout.HelpBox("请选择一个Prefab资产，而不是场景中的GameObject。", MessageType.Warning);
            selectedPrefab = null;
        }

        EditorGUILayout.Space();

        // 基本设置
        EditorGUILayout.LabelField("基本设置", EditorStyles.boldLabel);
        
        autoNaming = EditorGUILayout.Toggle("自动命名", autoNaming);
        
        EditorGUI.BeginDisabledGroup(autoNaming);
        outputName = EditorGUILayout.TextField("输出名称", outputName);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("输出路径", outputPath);
        if (GUILayout.Button("浏览...", GUILayout.Width(80)))
        {
            string path = EditorUtility.SaveFolderPanel("选择输出文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                outputPath = path.Replace(Application.dataPath, "Assets");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        atlasSize = EditorGUILayout.IntPopup("Atlas尺寸", atlasSize, 
            new string[] { "512", "1024", "2048", "4096" },
            new int[] { 512, 1024, 2048, 4096 });

        EditorGUILayout.Space();

        // 高级设置（可折叠）
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级设置");
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            combineTextures = EditorGUILayout.Toggle("合并纹理", combineTextures);
            optimizeMesh = EditorGUILayout.Toggle("优化网格", optimizeMesh);
            generateLightmapUVs = EditorGUILayout.Toggle("生成光照贴图UV", generateLightmapUVs);
            uvPadding = EditorGUILayout.FloatField("UV边距", uvPadding);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 预览信息
        if (selectedPrefab != null)
        {
            ShowPrefabInfo();
        }

        EditorGUILayout.Space();

        // 执行按钮
        GUI.enabled = selectedPrefab != null && !string.IsNullOrEmpty(outputName);
        
        if (GUILayout.Button("一键执行合并", GUILayout.Height(40)))
        {
            ExecuteAutoCombine();
        }
        
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("提示：合并操作会自动处理所有子对象，无需手动配置。", MessageType.Info);
    }

    private void ShowPrefabInfo()
    {
        EditorGUILayout.LabelField("Prefab信息", EditorStyles.boldLabel);
        
        // 实例化prefab进行分析
        GameObject tempInstance = PrefabUtility.InstantiatePrefab(selectedPrefab) as GameObject;
        if (tempInstance == null) return;

        try
        {
            var renderers = tempInstance.GetComponentsInChildren<MeshRenderer>();
            var skinnedRenderers = tempInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture2D>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        materials.Add(mat);
                        if (mat.HasProperty("_MainTex"))
                        {
                            var tex = mat.GetTexture("_MainTex") as Texture2D;
                            if (tex != null) textures.Add(tex);
                        }
                    }
                }
            }

            foreach (var renderer in skinnedRenderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        materials.Add(mat);
                        if (mat.HasProperty("_MainTex"))
                        {
                            var tex = mat.GetTexture("_MainTex") as Texture2D;
                            if (tex != null) textures.Add(tex);
                        }
                    }
                }
            }

            EditorGUILayout.LabelField($"• 网格渲染器: {renderers.Length}");
            EditorGUILayout.LabelField($"• 骨骼网格渲染器: {skinnedRenderers.Length}");
            EditorGUILayout.LabelField($"• 材质数量: {materials.Count}");
            EditorGUILayout.LabelField($"• 纹理数量: {textures.Count}");

            if (materials.Count > 1 || textures.Count > 1)
            {
                EditorGUILayout.HelpBox("此Prefab适合进行合并优化。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("此Prefab的材质和纹理已经较少，合并效果可能不明显。", MessageType.Warning);
            }
        }
        finally
        {
            Object.DestroyImmediate(tempInstance);
        }
    }

    private void ExecuteAutoCombine()
    {
        if (selectedPrefab == null)
        {
            EditorUtility.DisplayDialog("错误", "请选择一个Prefab", "确定");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("一键合并", "准备中...", 0f);

            // 创建输出目录
            CreateOutputDirectory();

            // 实例化prefab
            GameObject prefabInstance = PrefabUtility.InstantiatePrefab(selectedPrefab) as GameObject;
            if (prefabInstance == null)
            {
                throw new System.Exception("无法实例化Prefab");
            }

            EditorUtility.DisplayProgressBar("一键合并", "分析Prefab结构...", 0.1f);

            // 自动收集所有网格和材质
            var combineData = CollectAllMeshesAndMaterials(prefabInstance);

            EditorUtility.DisplayProgressBar("一键合并", "合并网格...", 0.3f);

            // 合并网格
            Mesh combinedMesh = CombineMeshes(combineData.combineInstances);

            EditorUtility.DisplayProgressBar("一键合并", "处理纹理...", 0.6f);

            // 创建合并材质和重映射UV
            Material combinedMaterial = null;
            AtlasGenerator.PackResult packResult = null;
            
            if (combineTextures && combineData.textures.Count > 0)
            {
                combinedMaterial = CreateCombinedMaterial(combineData.textures, out packResult);
                if (combineData.textures.Count > 1 && packResult != null)
                {
                    combinedMesh = RemapUVsForAtlas(combinedMesh, combineData, packResult);
                }
            }
            else
            {
                combinedMaterial = CreateDefaultMaterial();
            }

            EditorUtility.DisplayProgressBar("一键合并", "生成最终资产...", 0.8f);

            // 创建最终prefab
            CreateFinalPrefab(combinedMesh, combinedMaterial);

            // 清理临时对象
            Object.DestroyImmediate(prefabInstance);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("完成", $"Prefab合并完成！\n输出文件：{outputName}.prefab", "确定");

            Debug.Log($"一键合并完成：{selectedPrefab.name} -> {outputName}");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("错误", "合并过程中发生错误：" + e.Message, "确定");
            Debug.LogError("一键合并错误：" + e.ToString());
        }
    }

    private struct CombineData
    {
        public List<CombineInstance> combineInstances;
        public List<Material> materials;
        public List<Texture2D> textures;
        public List<MeshTextureMapping> meshTextureMappings;
    }

    private struct MeshTextureMapping
    {
        public int meshIndex;
        public int textureIndex;
        public int vertexStart;
        public int vertexCount;
    }

    private CombineData CollectAllMeshesAndMaterials(GameObject prefabInstance)
    {
        var data = new CombineData
        {
            combineInstances = new List<CombineInstance>(),
            materials = new List<Material>(),
            textures = new List<Texture2D>(),
            meshTextureMappings = new List<MeshTextureMapping>()
        };

        // 处理MeshRenderer
        var meshRenderers = prefabInstance.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in meshRenderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var combine = new CombineInstance
                {
                    mesh = meshFilter.sharedMesh,
                    transform = renderer.transform.localToWorldMatrix
                };
                data.combineInstances.Add(combine);

                CollectMaterialsAndTextures(renderer.sharedMaterials, data.materials, data.textures,
                    data.meshTextureMappings, data.combineInstances.Count - 1, meshFilter.sharedMesh.vertexCount);
            }
        }

        // 处理SkinnedMeshRenderer
        var skinnedRenderers = prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var renderer in skinnedRenderers)
        {
            if (renderer.sharedMesh != null)
            {
                var combine = new CombineInstance
                {
                    mesh = renderer.sharedMesh,
                    transform = renderer.transform.localToWorldMatrix
                };
                data.combineInstances.Add(combine);

                CollectMaterialsAndTextures(renderer.sharedMaterials, data.materials, data.textures,
                    data.meshTextureMappings, data.combineInstances.Count - 1, renderer.sharedMesh.vertexCount);
            }
        }

        return data;
    }

    private void CollectMaterialsAndTextures(Material[] materials, List<Material> materialList, 
        List<Texture2D> textureList, List<MeshTextureMapping> mappings, int meshIndex, int vertexCount)
    {
        Debug.Log($"开始收集材质和纹理，材质数组长度: {materials.Length}，网格索引: {meshIndex}");
        
        foreach (var mat in materials)
        {
            if (mat != null && !materialList.Contains(mat))
            {
                materialList.Add(mat);
                Debug.Log($"添加材质: {mat.name}");

                // 查找主纹理
                Texture2D mainTexture = null;

                if (combineTextures && mat.HasProperty("_MainTex"))
                {
                    mainTexture = mat.GetTexture("_MainTex") as Texture2D;
                    if (mainTexture != null)
                    {
                        Debug.Log($"找到主纹理: {mainTexture.name} ({mainTexture.width}x{mainTexture.height})");
                    }
                }

                // 检查其他常见纹理属性
                if (combineTextures && mainTexture == null)
                {
                    string[] textureProperties = { "_AlbedoMap", "_BaseMap", "_DiffuseMap", "_Texture" };
                    foreach (string prop in textureProperties)
                    {
                        if (mat.HasProperty(prop))
                        {
                            mainTexture = mat.GetTexture(prop) as Texture2D;
                            if (mainTexture != null)
                            {
                                Debug.Log($"找到纹理 {prop}: {mainTexture.name} ({mainTexture.width}x{mainTexture.height})");
                                break;
                            }
                        }
                    }
                }

                // 添加纹理并建立映射
                int textureIndex = -1;
                if (mainTexture != null)
                {
                    textureIndex = textureList.IndexOf(mainTexture);
                    if (textureIndex == -1)
                    {
                        textureIndex = textureList.Count;
                        textureList.Add(mainTexture);
                        Debug.Log($"添加新纹理到列表，索引: {textureIndex}");
                    }
                    else
                    {
                        Debug.Log($"纹理已存在，索引: {textureIndex}");
                    }
                }

                // 建立网格-纹理映射
                var mapping = new MeshTextureMapping
                {
                    meshIndex = meshIndex,
                    textureIndex = textureIndex,
                    vertexStart = 0, // 将在合并后计算
                    vertexCount = vertexCount
                };
                mappings.Add(mapping);
                
                Debug.Log($"建立映射：网格{meshIndex} -> 纹理{textureIndex}，顶点数: {vertexCount}");
            }
        }
        
        Debug.Log($"收集完成 - 材质: {materialList.Count}, 纹理: {textureList.Count}, 映射: {mappings.Count}");
    }

    private Mesh CombineMeshes(List<CombineInstance> combineInstances)
    {
        // 计算总顶点数
        int totalVertices = 0;
        foreach (var instance in combineInstances)
        {
            if (instance.mesh != null)
            {
                totalVertices += instance.mesh.vertexCount;
            }
        }

        Debug.Log($"合并网格信息: {combineInstances.Count} 个网格, 总顶点数: {totalVertices}");

        // 如果顶点数超过UInt16限制，使用智能分割或UInt32格式
        if (totalVertices > 65535)
        {
            Debug.LogWarning($"顶点数 ({totalVertices}) 超过UInt16限制 (65535)，启用大型网格支持");
            return CombineLargeMeshes(combineInstances, totalVertices);
        }

        // 标准合并流程
        var combinedMesh = new Mesh();
        combinedMesh.name = outputName + "_Mesh";
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

        if (optimizeMesh)
        {
            combinedMesh.Optimize();
        }

        if (generateLightmapUVs)
        {
            Unwrapping.GenerateSecondaryUVSet(combinedMesh);
        }

        return combinedMesh;
    }

    private Mesh CombineLargeMeshes(List<CombineInstance> combineInstances, int totalVertices)
    {
        var combinedMesh = new Mesh();
        combinedMesh.name = outputName + "_Mesh";
        
        // 设置为32位索引格式以支持更多顶点
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        try
        {
            // 尝试直接合并
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
            
            Debug.Log($"成功使用UInt32格式合并大型网格，顶点数: {combinedMesh.vertexCount}");
            
            // 优化设置
            if (optimizeMesh)
            {
                combinedMesh.Optimize();
            }
            
            // 对于大型网格，可能需要跳过二级UV生成以避免性能问题
            if (generateLightmapUVs)
            {
                if (totalVertices < 200000)
                {
                    Unwrapping.GenerateSecondaryUVSet(combinedMesh);
                }
                else
                {
                    Debug.LogWarning("网格过大，跳过光照贴图UV生成以提高性能");
                }
            }
            
            return combinedMesh;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"大型网格合并失败: {e.Message}");
            
            // 如果还是失败，尝试分组合并
            return CombineMeshesInGroups(combineInstances);
        }
    }

    private Mesh CombineMeshesInGroups(List<CombineInstance> combineInstances)
    {
        Debug.Log("尝试分组合并网格...");
        
        const int maxVerticesPerGroup = 50000; // 安全的顶点数限制
        var groups = new List<List<CombineInstance>>();
        var currentGroup = new List<CombineInstance>();
        int currentVertexCount = 0;

        // 将网格分组
        foreach (var instance in combineInstances)
        {
            int vertexCount = instance.mesh != null ? instance.mesh.vertexCount : 0;
            
            if (currentVertexCount + vertexCount > maxVerticesPerGroup && currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
                currentGroup = new List<CombineInstance>();
                currentVertexCount = 0;
            }
            
            currentGroup.Add(instance);
            currentVertexCount += vertexCount;
        }
        
        if (currentGroup.Count > 0)
        {
            groups.Add(currentGroup);
        }

        Debug.Log($"网格分为 {groups.Count} 组进行合并");

        // 合并每组网格
        var groupMeshes = new List<CombineInstance>();
        for (int i = 0; i < groups.Count; i++)
        {
            var groupMesh = new Mesh();
            groupMesh.name = $"{outputName}_Group{i}";
            groupMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            
            try
            {
                groupMesh.CombineMeshes(groups[i].ToArray(), true, true);
                
                if (optimizeMesh)
                {
                    groupMesh.Optimize();
                }
                
                var groupInstance = new CombineInstance
                {
                    mesh = groupMesh,
                    transform = Matrix4x4.identity
                };
                groupMeshes.Add(groupInstance);
                
                Debug.Log($"组 {i + 1} 合并完成，顶点数: {groupMesh.vertexCount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"组 {i + 1} 合并失败: {e.Message}");
            }
        }

        // 最终合并所有组
        var finalMesh = new Mesh();
        finalMesh.name = outputName + "_Mesh";
        finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        try
        {
            finalMesh.CombineMeshes(groupMeshes.ToArray(), true, true);
            
            if (optimizeMesh)
            {
                finalMesh.Optimize();
            }
            
            Debug.Log($"分组合并完成，最终顶点数: {finalMesh.vertexCount}");
            
            // 清理临时网格
            foreach (var instance in groupMeshes)
            {
                if (instance.mesh != null)
                {
                    Object.DestroyImmediate(instance.mesh);
                }
            }
            
            return finalMesh;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"最终合并失败: {e.Message}");
            
            // 清理临时网格
            foreach (var instance in groupMeshes)
            {
                if (instance.mesh != null)
                {
                    Object.DestroyImmediate(instance.mesh);
                }
            }
            
            // 返回第一个组作为备用方案
            if (groupMeshes.Count > 0)
            {
                var backupMesh = new Mesh();
                backupMesh.name = outputName + "_Mesh_Partial";
                backupMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                
                try
                {
                    backupMesh.CombineMeshes(new CombineInstance[] { groupMeshes[0] }, true, true);
                    Debug.LogWarning("使用部分合并结果作为备用方案");
                    return backupMesh;
                }
                catch
                {
                    // 如果连这个都失败了，返回一个空网格
                    Debug.LogError("所有合并尝试都失败，返回空网格");
                    return new Mesh() { name = outputName + "_Empty" };
                }
            }
            
            return new Mesh() { name = outputName + "_Empty" };
        }
    }

    private Material CreateCombinedMaterial(List<Texture2D> textures, out AtlasGenerator.PackResult packResult)
    {
        Debug.Log($"开始创建合并材质，纹理数量: {textures.Count}");
        
        // 初始化packResult
        packResult = null;
        
        var combinedMaterial = new Material(Shader.Find("Standard"));
        combinedMaterial.name = outputName + "_Material";

        if (textures.Count > 0)
        {
            Debug.Log("开始处理纹理合并...");
            
            // 打印所有纹理信息
            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                Debug.Log($"纹理 {i}: {tex.name} ({tex.width}x{tex.height}) 格式: {tex.format}");
            }

            if (textures.Count == 1)
            {
                // 如果只有一个纹理，直接使用
                Debug.Log("只有一个纹理，直接使用");
                combinedMaterial.SetTexture("_MainTex", textures[0]);
                combinedMaterial.SetTexture("_AlbedoMap", textures[0]); // URP兼容
            }
            else
            {
                // 使用Atlas生成器创建合并纹理
                Debug.Log("多个纹理，开始创建Atlas...");
                packResult = AtlasGenerator.PackTextures(textures, atlasSize, (int)uvPadding);
                
                if (packResult?.atlas != null)
                {
                    Debug.Log($"Atlas创建成功，尺寸: {packResult.atlas.width}x{packResult.atlas.height}");
                    combinedMaterial.SetTexture("_MainTex", packResult.atlas);
                    combinedMaterial.SetTexture("_AlbedoMap", packResult.atlas); // URP兼容
                    
                    // 保存atlas纹理
                    SaveAtlasTexture(packResult.atlas);
                    Debug.Log("Atlas纹理已保存");
                }
                else
                {
                    Debug.LogError("Atlas创建失败，使用第一个纹理作为备用");
                    combinedMaterial.SetTexture("_MainTex", textures[0]);
                    combinedMaterial.SetTexture("_AlbedoMap", textures[0]); // URP兼容
                }
            }
        }
        else
        {
            Debug.LogWarning("没有找到纹理，创建白色材质");
            // 创建一个白色纹理作为默认
            var whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.wrapMode = TextureWrapMode.Clamp;
            whiteTexture.Apply();
            combinedMaterial.SetTexture("_MainTex", whiteTexture);
            combinedMaterial.SetTexture("_AlbedoMap", whiteTexture); // URP兼容
        }

        // 设置材质的其他属性，确保正确的渲染
        combinedMaterial.SetFloat("_Metallic", 0f);
        combinedMaterial.SetFloat("_Smoothness", 0.5f);
        combinedMaterial.SetColor("_Color", Color.white); // 确保颜色为白色，不影响纹理
        
        // 设置渲染模式为不透明
        combinedMaterial.SetFloat("_Mode", 0); // Opaque
        combinedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        combinedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        combinedMaterial.SetInt("_ZWrite", 1);
        combinedMaterial.DisableKeyword("_ALPHATEST_ON");
        combinedMaterial.DisableKeyword("_ALPHABLEND_ON");
        combinedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        combinedMaterial.renderQueue = -1;
        
        Debug.Log($"材质创建完成: {combinedMaterial.name}");
        return combinedMaterial;
    }

    private void SaveAtlasTexture(Texture2D atlas)
    {
        try
        {
            string atlasPath = Path.Combine(outputPath, outputName + "_Atlas.png");
            Debug.Log($"保存Atlas纹理到: {atlasPath}");
            
            byte[] pngData = atlas.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                Debug.LogError("Atlas纹理编码为PNG失败");
                return;
            }
            
            string fullPath = Path.Combine(Application.dataPath, atlasPath.Replace("Assets/", ""));
            Debug.Log($"完整路径: {fullPath}");
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"创建目录: {directory}");
            }
            
            File.WriteAllBytes(fullPath, pngData);
            Debug.Log($"Atlas纹理文件写入成功，大小: {pngData.Length} 字节");
            
            AssetDatabase.Refresh();
            
            // 验证文件是否成功创建
            if (File.Exists(fullPath))
            {
                Debug.Log("Atlas纹理文件验证成功");
                
                // 设置纹理导入设置
                TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = true;
                    // 设置wrap mode为Clamp
                    importer.wrapMode = TextureWrapMode.Clamp;
                    // 设置过滤模式为双线性，减少缝隙
                    importer.filterMode = FilterMode.Bilinear;
                    // 确保sRGB设置正确
                    importer.sRGBTexture = true; // 大多数Albedo纹理应该是sRGB
                    
                    // 重新启用mipmap，但使用高质量设置
                    importer.mipmapEnabled = true;
                    // 使用Kaiser过滤器，减少mipmap缝隙
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    // 保持边缘锐利度
                    importer.borderMipmap = true; // 边界mipmap，有助于减少边缘问题
                    
                    // 设置最大纹理尺寸
                    importer.maxTextureSize = atlasSize;

                    // 设置压缩格式
                    importer.textureCompression = TextureImporterCompression.Uncompressed; // 避免压缩导致的颜色失真

                    importer.SaveAndReimport();
                    Debug.Log("Atlas纹理导入设置已配置：启用Mipmap，Wrap Mode=Clamp，使用Kaiser过滤器");
                }
            }
            else
            {
                Debug.LogError("Atlas纹理文件创建失败");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存Atlas纹理时发生错误: {e.Message}");
        }
    }

    private Mesh RemapUVsForAtlas(Mesh mesh, CombineData combineData, AtlasGenerator.PackResult packResult)
    {
        if (combineData.textures.Count <= 1 || packResult == null)
        {
            Debug.Log("单纹理或无PackResult，跳过UV重映射");
            return mesh;
        }

        Debug.Log($"开始精确UV重映射，纹理数量: {combineData.textures.Count}，映射数量: {combineData.meshTextureMappings.Count}");

        var uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0)
        {
            Debug.LogWarning("网格没有UV坐标，跳过重映射");
            return mesh;
        }

        // 创建新的UV数组
        Vector2[] newUVs = new Vector2[uvs.Length];
        System.Array.Copy(uvs, newUVs, uvs.Length);

        // 重新构建网格-纹理映射，基于实际的合并结果
        var meshVertexRanges = CalculateActualVertexRanges(combineData.combineInstances);
        
        Debug.Log($"计算得到 {meshVertexRanges.Count} 个网格的顶点范围");

        // 为每个网格范围重映射UV
        for (int meshIdx = 0; meshIdx < meshVertexRanges.Count && meshIdx < combineData.meshTextureMappings.Count; meshIdx++)
        {
            var vertexRange = meshVertexRanges[meshIdx];
            var mapping = combineData.meshTextureMappings[meshIdx];
            
            Debug.Log($"处理网格 {meshIdx}: 顶点范围 {vertexRange.start}-{vertexRange.end}, 纹理索引 {mapping.textureIndex}");

            if (mapping.textureIndex >= 0 && mapping.textureIndex < packResult.uvOffsets.Length)
            {
                Vector2 offset = packResult.uvOffsets[mapping.textureIndex];
                Vector2 scale = packResult.uvScales[mapping.textureIndex];
                
                // 为Mipmap支持增加更大的边缘收缩
                // 根据Atlas尺寸动态计算收缩量
                float mipmapSafePadding = Utility.CalculateMipmapSafePadding(atlasSize);
                Vector2 shrinkOffset = new Vector2(mipmapSafePadding, mipmapSafePadding);
                Vector2 shrinkScale = scale - shrinkOffset * 2;
                Vector2 adjustedOffset = offset + shrinkOffset;
                
                // 确保收缩后的尺寸仍然有效
                if (shrinkScale.x <= 0 || shrinkScale.y <= 0)
                {
                    Debug.LogWarning($"网格 {meshIdx} 的纹理区域太小，无法进行安全的UV收缩，跳过收缩");
                    shrinkScale = scale * 0.9f; // 使用90%的尺寸作为备用方案
                    adjustedOffset = offset + (scale - shrinkScale) * 0.5f;
                }

                //Vector2 shrinkScale = scale;
                //Vector2 adjustedOffset = offset;

                Debug.Log($"应用UV变换: offset({adjustedOffset.x:F3}, {adjustedOffset.y:F3}), scale({shrinkScale.x:F3}, {shrinkScale.y:F3}), 收缩量({mipmapSafePadding:F3})");

                // 重映射该网格范围内的所有UV坐标
                for (int i = vertexRange.start; i < vertexRange.end && i < newUVs.Length; i++)
                {
                    Vector2 originalUV = uvs[i];
                    
                    // 确保原始UV在[0,1]范围内
                    originalUV.x = Mathf.Clamp01(originalUV.x);
                    originalUV.y = Mathf.Clamp01(originalUV.y);
                    
                    newUVs[i] = new Vector2(
                        adjustedOffset.x + originalUV.x * shrinkScale.x,
                        adjustedOffset.y + originalUV.y * shrinkScale.y
                    );
                    
                    // 确保新UV在有效范围内
                    newUVs[i].x = Mathf.Clamp(newUVs[i].x, offset.x, offset.x + scale.x);
                    newUVs[i].y = Mathf.Clamp(newUVs[i].y, offset.y, offset.y + scale.y);
                }
                
                Debug.Log($"网格 {meshIdx} UV重映射完成，处理了 {vertexRange.end - vertexRange.start} 个顶点");
            }
            else
            {
                Debug.LogWarning($"网格 {meshIdx} 的纹理索引 {mapping.textureIndex} 无效，保持原始UV");
            }
        }

        // 验证UV坐标范围
        int outOfRangeCount = 0;
        for (int i = 0; i < newUVs.Length; i++)
        {
            if (newUVs[i].x < 0 || newUVs[i].x > 1 || newUVs[i].y < 0 || newUVs[i].y > 1)
            {
                outOfRangeCount++;
            }
        }
        
        if (outOfRangeCount > 0)
        {
            Debug.LogWarning($"发现 {outOfRangeCount} 个UV坐标超出[0,1]范围，已进行修正");
        }

        mesh.uv = newUVs;
        Debug.Log("精确UV重映射完成");
        return mesh;
    }
    
   

    private struct VertexRange
    {
        public int start;
        public int end;
        public int meshIndex;
    }

    private List<VertexRange> CalculateActualVertexRanges(List<CombineInstance> combineInstances)
    {
        var ranges = new List<VertexRange>();
        int currentStart = 0;
        
        for (int i = 0; i < combineInstances.Count; i++)
        {
            var instance = combineInstances[i];
            if (instance.mesh != null)
            {
                int vertexCount = instance.mesh.vertexCount;
                ranges.Add(new VertexRange
                {
                    start = currentStart,
                    end = currentStart + vertexCount,
                    meshIndex = i
                });
                
                Debug.Log($"网格 {i} ({instance.mesh.name}): 顶点范围 {currentStart}-{currentStart + vertexCount}");
                currentStart += vertexCount;
            }
        }
        
        return ranges;
    }

    private Material CreateDefaultMaterial()
    {
        var material = new Material(Shader.Find("Standard"));
        material.name = outputName + "_Material";
        material.color = Color.white;
        return material;
    }

    private void CreateOutputDirectory()
    {
        string fullPath = Path.Combine(Application.dataPath, outputPath.Replace("Assets/", ""));
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }
    }

    private void CreateFinalPrefab(Mesh combinedMesh, Material combinedMaterial)
    {
        // 保存网格资产
        string meshPath = Path.Combine(outputPath, outputName + "_Mesh.asset");
        AssetDatabase.CreateAsset(combinedMesh, meshPath);

        // 保存材质资产
        string materialPath = Path.Combine(outputPath, outputName + "_Material.mat");
        AssetDatabase.CreateAsset(combinedMaterial, materialPath);

        // 创建合并后的GameObject
        GameObject combinedObject = new GameObject(outputName);
        var meshFilter = combinedObject.AddComponent<MeshFilter>();
        var meshRenderer = combinedObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = combinedMesh;
        meshRenderer.material = combinedMaterial;

        // 保存为prefab
        string prefabPath = Path.Combine(outputPath, outputName + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(combinedObject, prefabPath);

        Object.DestroyImmediate(combinedObject);
        AssetDatabase.Refresh();

        // 选中生成的prefab
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
} 