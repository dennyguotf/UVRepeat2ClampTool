using UnityEngine;

public class Utility
{
    /// <summary>
    /// 根据Atlas尺寸计算边缘扩展像素数，以应对Mipmap问题
    /// </summary>
    /// <param name="atlasSize">Atlas尺寸</param>
    /// <returns>边缘扩展像素数</returns>
    public static int CalculateEdgePaddingForMipmap(int atlasSize)
    {
        // 计算mipmap级别数
        int mipmapLevels = Mathf.FloorToInt(Mathf.Log(atlasSize, 2)) + 1;

        // 边缘扩展像素数应该至少覆盖最高几个mipmap级别的采样范围
        int padding = Mathf.Max(2, mipmapLevels);  
        Debug.Log($"Atlas尺寸: {atlasSize}, Mipmap级别: {mipmapLevels}, 边缘扩展: {padding}像素");
        return padding;
    }

    /// <summary>
    /// 计算Mipmap安全的UV收缩量
    /// </summary>
    /// <param name="atlasSize">Atlas尺寸</param>
    /// <returns>收缩量</returns>
    public static float CalculateMipmapSafePadding(int atlasSize)
    {
        // 计算边缘扩展像素数
        int edgePadding = CalculateEdgePaddingForMipmap(atlasSize);

        // UV收缩量应该略大于边缘扩展，确保不会采样到边界
        // 收缩 (edgePadding + 0.5) 个像素的距离
        float padding = (edgePadding + 0.5f) / atlasSize;

        Debug.Log($"Atlas尺寸: {atlasSize}, 边缘扩展: {edgePadding}像素, UV收缩量: {padding:F4}");
        return padding;
    }

    public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
    {
        Color[] pixels = tex.GetPixels();
        Color[] newPixels = new Color[newWidth * newHeight];
        
        float ratioX = (float)tex.width / newWidth;
        float ratioY = (float)tex.height / newHeight;
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float gx = x * ratioX;
                float gy = y * ratioY;
                
                int gxi = (int)gx;
                int gyi = (int)gy;
                
                float fx = gx - gxi;
                float fy = gy - gyi;
                
                int gxi1 = Mathf.Min(gxi + 1, tex.width - 1);
                int gyi1 = Mathf.Min(gyi + 1, tex.height - 1);
                
                Color c00 = pixels[gyi * tex.width + gxi];
                Color c10 = pixels[gyi * tex.width + gxi1];
                Color c01 = pixels[gyi1 * tex.width + gxi];
                Color c11 = pixels[gyi1 * tex.width + gxi1];
                
                Color c0 = Color.Lerp(c00, c10, fx);
                Color c1 = Color.Lerp(c01, c11, fx);
                Color c = Color.Lerp(c0, c1, fy);
                
                newPixels[y * newWidth + x] = c;
            }
        }
        
        tex.Reinitialize(newWidth, newHeight);
        tex.SetPixels(newPixels);
        tex.Apply();
    }

    public static void Point(Texture2D tex, int newWidth, int newHeight)
    {
        Color[] pixels = tex.GetPixels();
        Color[] newPixels = new Color[newWidth * newHeight];
        
        float ratioX = (float)tex.width / newWidth;
        float ratioY = (float)tex.height / newHeight;
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int px = Mathf.FloorToInt(x * ratioX);
                int py = Mathf.FloorToInt(y * ratioY);
                
                px = Mathf.Clamp(px, 0, tex.width - 1);
                py = Mathf.Clamp(py, 0, tex.height - 1);
                
                newPixels[y * newWidth + x] = pixels[py * tex.width + px];
            }
        }
        
        tex.Reinitialize(newWidth, newHeight);
        tex.SetPixels(newPixels);
        tex.Apply();
    }

    public static Texture2D CreateScaledCopy(Texture2D original, int newWidth, int newHeight, bool useBilinear = true)
    {
        Texture2D newTexture = new Texture2D(newWidth, newHeight, original.format, false);
        
        Color[] originalPixels = original.GetPixels();
        Color[] newPixels = new Color[newWidth * newHeight];
        
        if (useBilinear)
        {
            float ratioX = (float)original.width / newWidth;
            float ratioY = (float)original.height / newHeight;
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float gx = x * ratioX;
                    float gy = y * ratioY;
                    
                    int gxi = (int)gx;
                    int gyi = (int)gy;
                    
                    float fx = gx - gxi;
                    float fy = gy - gyi;
                    
                    int gxi1 = Mathf.Min(gxi + 1, original.width - 1);
                    int gyi1 = Mathf.Min(gyi + 1, original.height - 1);
                    
                    Color c00 = originalPixels[gyi * original.width + gxi];
                    Color c10 = originalPixels[gyi * original.width + gxi1];
                    Color c01 = originalPixels[gyi1 * original.width + gxi];
                    Color c11 = originalPixels[gyi1 * original.width + gxi1];
                    
                    Color c0 = Color.Lerp(c00, c10, fx);
                    Color c1 = Color.Lerp(c01, c11, fx);
                    Color c = Color.Lerp(c0, c1, fy);
                    
                    newPixels[y * newWidth + x] = c;
                }
            }
        }
        else
        {
            // Point filtering
            float ratioX = (float)original.width / newWidth;
            float ratioY = (float)original.height / newHeight;
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int px = Mathf.FloorToInt(x * ratioX);
                    int py = Mathf.FloorToInt(y * ratioY);
                    
                    px = Mathf.Clamp(px, 0, original.width - 1);
                    py = Mathf.Clamp(py, 0, original.height - 1);
                    
                    newPixels[y * newWidth + x] = originalPixels[py * original.width + px];
                }
            }
        }
        
        newTexture.SetPixels(newPixels);
        newTexture.Apply();
        
        return newTexture;
    }
} 