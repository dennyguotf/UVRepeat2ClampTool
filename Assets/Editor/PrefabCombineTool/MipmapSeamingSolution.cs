using UnityEngine;
using UnityEditor;

/// <summary>
/// Mipmap缝隙问题详细解释和解决方案
/// 
/// 问题描述：
/// 当Atlas纹理开启Mipmap后，在拉远视角时，纹理边界处出现缝隙
/// 
/// 根本原因：
/// 1. Mipmap生成过程中，Unity会对整个Atlas进行下采样
/// 2. 不同纹理区域的像素会在低分辨率mipmap中混合
/// 3. GPU在远距离采样时使用低分辨率mipmap，导致边界混合
/// 
/// 示例分析：
/// 原始2048x2048 Atlas -> Level 1: 1024x1024 -> Level 2: 512x512 ...
/// 在Level 2时，原本相邻4个像素被合并为1个像素
/// 如果这4个像素来自不同纹理，结果就是混合色
/// </summary>
public class MipmapSeamingSolution : EditorWindow
{
    [MenuItem("工具/Mipmap缝隙问题说明")]
    public static void ShowWindow()
    {
        var window = GetWindow<MipmapSeamingSolution>("Mipmap缝隙解决方案");
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        GUILayout.Label("Mipmap缝隙问题解决方案", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("当Atlas纹理开启Mipmap后，在拉远视角时可能出现缝隙。这里解释原因和我们的解决方案。", MessageType.Info);
        
        GUILayout.Label("问题原因分析：", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            "1. Mipmap生成：Unity自动生成多级分辨率版本\n" +
            "   - Level 0: 2048x2048 (原始)\n" +
            "   - Level 1: 1024x1024 (1/2尺寸)\n" +
            "   - Level 2: 512x512 (1/4尺寸)\n" +
            "   - Level 3: 256x256 (1/8尺寸)\n\n" +
            "2. 像素混合：低级别mipmap中，相邻纹理的像素会混合\n\n" +
            "3. 远距离采样：GPU根据距离选择合适的mipmap级别\n" +
            "   距离越远，使用越低分辨率的mipmap，混合问题越严重",
            GUILayout.Height(120)
        );

        EditorGUILayout.Space();
        
        GUILayout.Label("我们的解决方案：", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            "1. 增强边缘扩展 (Enhanced Edge Padding)：\n" +
            "   - 根据Atlas尺寸动态计算扩展像素数\n" +
            "   - 2048x2048 Atlas: 扩展4像素\n" +
            "   - 1024x1024 Atlas: 扩展2像素\n" +
            "   - 多层扩展，覆盖角落和边缘\n\n" +
            "2. 智能UV收缩 (Smart UV Shrinking)：\n" +
            "   - 收缩量 = (边缘扩展 + 0.5) / Atlas尺寸\n" +
            "   - 确保采样点远离边界\n" +
            "   - 即使有浮点精度误差也安全\n\n" +
            "3. 优化的Mipmap设置：\n" +
            "   - 使用Kaiser过滤器减少混合\n" +
            "   - 启用边界mipmap\n" +
            "   - 高质量压缩设置",
            GUILayout.Height(140)
        );

        EditorGUILayout.Space();
        
        GUILayout.Label("技术细节：", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            "边缘扩展计算公式：\n" +
            "int padding = Max(2, Min(8, atlasSize / 512))\n\n" +
            "UV收缩计算公式：\n" +
            "float shrink = (padding + 0.5f) / atlasSize\n\n" +
            "这样确保了在所有mipmap级别都有足够的安全边距",
            GUILayout.Height(80)
        );

        EditorGUILayout.Space();
        
        if (GUILayout.Button("演示边缘扩展效果", GUILayout.Height(30)))
        {
            DemonstrateEdgePadding();
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox(
            "提示：如果仍然有缝隙问题，可以尝试：\n" +
            "1. 增大Atlas尺寸\n" +
            "2. 减少单个纹理的尺寸\n" +
            "3. 使用Point过滤模式（但会失去平滑效果）\n" +
            "4. 调整UV Padding参数",
            MessageType.Info
        );
    }

    private void DemonstrateEdgePadding()
    {
        Debug.Log("=== Mipmap边缘扩展演示 ===");
        
        int[] atlasSizes = { 512, 1024, 2048, 4096 };
        
        foreach (int size in atlasSizes)
        {
            int padding = Mathf.Max(2, Mathf.Min(8, size / 512));
            float uvShrink = (padding + 0.5f) / size;
            int mipmapLevels = Mathf.FloorToInt(Mathf.Log(size, 2)) + 1;
            
            Debug.Log($"Atlas {size}x{size}:");
            Debug.Log($"  - Mipmap级别: {mipmapLevels}");
            Debug.Log($"  - 边缘扩展: {padding} 像素");
            Debug.Log($"  - UV收缩: {uvShrink:F4} ({uvShrink * 100:F2}%)");
            Debug.Log($"  - 最小可安全纹理尺寸: {padding * 4}x{padding * 4}");
        }
        
        Debug.Log("边缘扩展演示完成，查看Console获取详细数据");
    }
}

/// <summary>
/// Mipmap质量评估工具
/// </summary>
public static class MipmapQualityAssessment
{
    /// <summary>
    /// 评估Atlas设置的Mipmap质量
    /// </summary>
    /// <param name="atlasSize">Atlas尺寸</param>
    /// <param name="smallestTextureSize">最小纹理尺寸</param>
    /// <param name="padding">边缘扩展像素数</param>
    /// <returns>质量评估结果</returns>
    public static string AssessQuality(int atlasSize, int smallestTextureSize, int padding)
    {
        float textureRatio = (float)smallestTextureSize / atlasSize;
        float paddingRatio = (float)padding / smallestTextureSize;
        
        string quality;
        if (paddingRatio >= 0.2f && textureRatio >= 0.1f)
        {
            quality = "优秀 - Mipmap缝隙风险很低";
        }
        else if (paddingRatio >= 0.1f && textureRatio >= 0.05f)
        {
            quality = "良好 - Mipmap缝隙风险较低";
        }
        else if (paddingRatio >= 0.05f)
        {
            quality = "一般 - 可能有轻微Mipmap缝隙";
        }
        else
        {
            quality = "较差 - 很可能出现Mipmap缝隙";
        }
        
        return $"{quality}\n" +
               $"纹理占比: {textureRatio * 100:F1}%\n" +
               $"边距占比: {paddingRatio * 100:F1}%\n" +
               $"推荐最小纹理尺寸: {padding * 10}x{padding * 10}";
    }
} 