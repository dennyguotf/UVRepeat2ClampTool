using UnityEngine;

/// <summary>
/// 边界采样问题详细解释
/// 
/// 问题场景：
/// 当多个纹理合并到一个Atlas中时，在纹理边界处会出现采样问题
/// 
/// 原因分析：
/// 1. GPU双线性过滤：采样时会混合相邻4个像素的颜色
/// 2. UV坐标精度：浮点数精度导致的微小偏差
/// 3. 纹理边界：相邻像素可能是其他纹理或背景色
/// 
/// 示例：
/// 假设有一个2x2的Atlas，包含两个1x1的纹理：
/// 
/// 原始Atlas（无边缘扩展）：
/// ┌─────┬─────┐
/// │ R   │ G   │  R=红色纹理，G=绿色纹理
/// │     │     │  T=透明背景，B=蓝色纹理
/// ├─────┼─────┤
/// │ T   │ B   │
/// │     │     │
/// └─────┴─────┘
/// 
/// 问题：当采样红色纹理边缘时，双线性过滤会混合：
/// - 红色像素 (1.0, 0.0, 0.0)
/// - 绿色像素 (0.0, 1.0, 0.0)  ← 相邻纹理！
/// - 透明像素 (0.0, 0.0, 0.0)  ← 背景色！
/// - 蓝色像素 (0.0, 0.0, 1.0)  ← 其他纹理！
/// 
/// 结果：边缘出现不期望的混合色
/// 
/// 解决方案 - 边缘扩展：
/// ┌─────┬─────┬─────┐
/// │ R   │ R   │ G   │  扩展红色纹理的右边缘
/// ├─────┼─────┼─────┤
/// │ R   │ R   │ G   │  扩展红色纹理的下边缘
/// ├─────┼─────┼─────┤
/// │ T   │ B   │ B   │  扩展蓝色纹理的左边缘
/// └─────┴─────┴─────┘
/// 
/// 现在采样红色纹理边缘时，混合的都是红色像素，保持颜色一致性
/// </summary>
public class EdgePaddingExplanation
{
    /// <summary>
    /// 演示双线性过滤的采样计算
    /// </summary>
    /// <param name="uv">UV坐标</param>
    /// <param name="textureSize">纹理尺寸</param>
    /// <returns>采样结果</returns>
    public static Color BilinearSample(Vector2 uv, int textureSize, Color[,] pixels)
    {
        // 将UV坐标转换为像素坐标
        float x = uv.x * (textureSize - 1);
        float y = uv.y * (textureSize - 1);
        
        // 获取四个相邻像素的坐标
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, textureSize - 1);
        int y1 = Mathf.Min(y0 + 1, textureSize - 1);
        
        // 计算插值权重
        float fx = x - x0;
        float fy = y - y0;
        
        // 获取四个像素的颜色
        Color c00 = pixels[x0, y0]; // 左下
        Color c10 = pixels[x1, y0]; // 右下
        Color c01 = pixels[x0, y1]; // 左上
        Color c11 = pixels[x1, y1]; // 右上
        
        // 双线性插值计算
        Color c0 = Color.Lerp(c00, c10, fx); // 下边插值
        Color c1 = Color.Lerp(c01, c11, fx); // 上边插值
        Color result = Color.Lerp(c0, c1, fy); // 最终插值
        
        return result;
    }
    
    /// <summary>
    /// 演示边缘扩展的效果
    /// </summary>
    public static void DemonstrateEdgePadding()
    {
        Debug.Log("=== 边界采样问题演示 ===");
        
        // 模拟一个简单的2x2 Atlas
        Color[,] atlasWithoutPadding = new Color[2, 2]
        {
            { Color.red, Color.green },      // 第一行：红色和绿色纹理
            { Color.clear, Color.blue }      // 第二行：透明和蓝色纹理
        };
        
        Color[,] atlasWithPadding = new Color[3, 3]
        {
            { Color.red, Color.red, Color.green },    // 扩展红色到右边
            { Color.red, Color.red, Color.green },    // 扩展红色到下边
            { Color.clear, Color.blue, Color.blue }   // 扩展蓝色到左边
        };
        
        // 测试边界处的采样
        Vector2 edgeUV = new Vector2(0.49f, 0.49f); // 红色纹理的右下角附近
        
        Color sampledWithoutPadding = BilinearSample(edgeUV, 2, atlasWithoutPadding);
        Color sampledWithPadding = BilinearSample(edgeUV, 3, atlasWithPadding);
        
        Debug.Log($"无边缘扩展的采样结果: {sampledWithoutPadding}");
        Debug.Log($"有边缘扩展的采样结果: {sampledWithPadding}");
        Debug.Log("可以看到，边缘扩展避免了不同纹理间的颜色混合");
    }
}

/// <summary>
/// UV收缩技术的解释
/// 
/// 除了边缘扩展，我们还使用UV收缩来进一步避免采样问题：
/// 
/// 原理：将UV坐标向内收缩一小段距离，确保采样点远离边界
/// 
/// 示例：
/// 原始UV范围：[0.0, 1.0]
/// 收缩后范围：[0.001, 0.999]  // 收缩了0.001
/// 
/// 这样即使有浮点精度误差，也不会采样到边界外的像素
/// </summary>
public class UVShrinkingExplanation
{
    /// <summary>
    /// 计算UV收缩参数
    /// </summary>
    /// <param name="atlasSize">Atlas尺寸</param>
    /// <param name="textureSize">单个纹理尺寸</param>
    /// <returns>收缩量</returns>
    public static float CalculateShrinkAmount(int atlasSize, int textureSize)
    {
        // 收缩半个像素的距离
        return 0.5f / atlasSize;
    }
    
    /// <summary>
    /// 应用UV收缩
    /// </summary>
    /// <param name="originalUV">原始UV坐标</param>
    /// <param name="offset">Atlas中的偏移</param>
    /// <param name="scale">Atlas中的缩放</param>
    /// <param name="shrinkAmount">收缩量</param>
    /// <returns>收缩后的UV坐标</returns>
    public static Vector2 ApplyUVShrinking(Vector2 originalUV, Vector2 offset, Vector2 scale, float shrinkAmount)
    {
        // 计算收缩后的参数
        Vector2 shrinkOffset = new Vector2(shrinkAmount, shrinkAmount);
        Vector2 shrinkScale = scale - shrinkOffset * 2;
        Vector2 adjustedOffset = offset + shrinkOffset;
        
        // 应用变换
        return new Vector2(
            adjustedOffset.x + originalUV.x * shrinkScale.x,
            adjustedOffset.y + originalUV.y * shrinkScale.y
        );
    }
} 