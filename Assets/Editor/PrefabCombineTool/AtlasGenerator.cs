using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class AtlasGenerator
{
    public class AtlasRect
    {
        public int x, y, width, height;
        public Texture2D texture;
        public int textureIndex;
        
        public AtlasRect(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    public class PackResult
    {
        public Texture2D atlas;
        public AtlasRect[] rects;
        public Vector2[] uvOffsets;
        public Vector2[] uvScales;
    }

    public static PackResult PackTextures(List<Texture2D> textures, int atlasSize, int padding = 2)
    {
        if (textures == null || textures.Count == 0)
            return null;

        // 过滤掉null纹理
        var validTextures = textures.Where(t => t != null).ToList();
        if (validTextures.Count == 0)
            return null;

        // 准备纹理数据
        var textureInfos = new List<TextureInfo>();
        for (int i = 0; i < validTextures.Count; i++)
        {
            var tex = validTextures[i];
            textureInfos.Add(new TextureInfo
            {
                texture = tex,
                width = tex.width,
                height = tex.height,
                index = i
            });
        }

        // 按面积排序（从大到小）
        textureInfos.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));

        // 增强边缘扩展以应对Mipmap问题
        // 计算需要扩展的像素数：基于Atlas尺寸动态计算
        int edgePadding = Utility.CalculateEdgePaddingForMipmap(atlasSize);
        if(padding < edgePadding * 2)
        {
            padding = edgePadding * 2;
        }

        // 尝试打包
        var rects = PackRectangles(textureInfos, atlasSize, padding);
        if (rects == null)
        {
            Debug.LogError("无法将所有纹理打包到指定的atlas尺寸中");
            return null;
        }

        // 创建atlas纹理
        var atlas = CreateAtlas(rects, atlasSize);
        
        // 计算UV偏移和缩放
        var uvOffsets = new Vector2[validTextures.Count];
        var uvScales = new Vector2[validTextures.Count];
        
        foreach (var rect in rects)
        {
            uvOffsets[rect.textureIndex] = new Vector2((float)rect.x / atlasSize, (float)rect.y / atlasSize);
            uvScales[rect.textureIndex] = new Vector2((float)rect.width / atlasSize, (float)rect.height / atlasSize);
        }

        return new PackResult
        {
            atlas = atlas,
            rects = rects,
            uvOffsets = uvOffsets,
            uvScales = uvScales
        };
    }

    private class TextureInfo
    {
        public Texture2D texture;
        public int width, height;
        public int index;
    }

    private class Node
    {
        public int x, y, width, height;
        public bool used;
        public Node right, down;

        public Node(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.used = false;
        }

        public Node Insert(int width, int height)
        {
            if (used)
            {
                Node newNode = right?.Insert(width, height);
                return newNode ?? down?.Insert(width, height);
            }

            if (width > this.width || height > this.height)
                return null;

            if (width == this.width && height == this.height)
            {
                used = true;
                return this;
            }

            int dw = this.width - width;
            int dh = this.height - height;

            if (dw > dh)
            {
                right = new Node(x + width, y, dw, height);
                down = new Node(x, y + height, width, dh);
            }
            else
            {
                right = new Node(x + width, y, dw, this.height);
                down = new Node(x, y + height, this.width, dh);
            }

            used = true;
            return this;
        }
    }

    private static AtlasRect[] PackRectangles(List<TextureInfo> textures, int atlasSize, int padding)
    {
        var root = new Node(0, 0, atlasSize, atlasSize);
        var rects = new List<AtlasRect>();

        foreach (var texInfo in textures)
        {
            int paddedWidth = texInfo.width + padding * 2;
            int paddedHeight = texInfo.height + padding * 2;

            var node = root.Insert(paddedWidth, paddedHeight);
            if (node == null)
            {
                Debug.LogError($"无法为纹理 {texInfo.texture.name} 找到空间");
                return null;
            }

            var rect = new AtlasRect(node.x + padding, node.y + padding, texInfo.width, texInfo.height)
            {
                texture = texInfo.texture,
                textureIndex = texInfo.index
            };
            
            rects.Add(rect);
        }

        return rects.ToArray();
    }

    private static Texture2D CreateAtlas(AtlasRect[] rects, int atlasSize)
    {
        var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
        
        // 设置wrap mode为Clamp
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Bilinear;
        
        // 初始化为白色而不是透明，避免缝隙问题
        // 问题解释：如果初始化为透明色，当UV坐标有微小偏差时，
        // GPU的双线性过滤会将透明像素与纹理像素混合，导致边缘变暗或出现缝隙
        var clearPixels = new Color[atlasSize * atlasSize];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = Color.white; // 改为白色
        }
        atlas.SetPixels(clearPixels);

        foreach (var rect in rects)
        {
            if (rect.texture == null) continue;

            // 获取可读纹理
            var readableTexture = MakeTextureReadable(rect.texture);
            
            // 如果尺寸不匹配，需要缩放
            if (readableTexture.width != rect.width || readableTexture.height != rect.height)
            {
                readableTexture = Utility.CreateScaledCopy(readableTexture, rect.width, rect.height);
            }

            // 复制像素到atlas，并添加边缘扩展以防止缝隙
            var pixels = readableTexture.GetPixels();
            
            // 先复制主要区域
            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    int srcIndex = y * rect.width + x;
                    int dstX = rect.x + x;
                    int dstY = rect.y + y;
                    
                    if (dstX < atlasSize && dstY < atlasSize && srcIndex < pixels.Length)
                    {
                        atlas.SetPixel(dstX, dstY, pixels[srcIndex]);
                    }
                }
            }
            
            // 增强边缘扩展以应对Mipmap问题
            // 计算需要扩展的像素数：基于Atlas尺寸动态计算
            int edgePadding = Utility.CalculateEdgePaddingForMipmap(atlasSize);
            
            // 多层边缘扩展
            for (int layer = 1; layer <= edgePadding; layer++)
            {
                // 左边缘扩展
                if (rect.x - layer >= 0)
                {
                    for (int y = -layer; y < rect.height + layer; y++)
                    {
                        int srcY = Mathf.Clamp(y, 0, rect.height - 1);
                        Color edgeColor = pixels[srcY * rect.width]; // 取最左边的像素
                        int dstY = rect.y + y;
                        if (dstY >= 0 && dstY < atlasSize)
                        {
                            atlas.SetPixel(rect.x - layer, dstY, edgeColor);
                        }
                    }
                }
                
                // 右边缘扩展
                if (rect.x + rect.width + layer - 1 < atlasSize)
                {
                    for (int y = -layer; y < rect.height + layer; y++)
                    {
                        int srcY = Mathf.Clamp(y, 0, rect.height - 1);
                        Color edgeColor = pixels[srcY * rect.width + (rect.width - 1)]; // 取最右边的像素
                        int dstY = rect.y + y;
                        if (dstY >= 0 && dstY < atlasSize)
                        {
                            atlas.SetPixel(rect.x + rect.width + layer - 1, dstY, edgeColor);
                        }
                    }
                }
                
                // 上边缘扩展
                if (rect.y - layer >= 0)
                {
                    for (int x = -layer; x < rect.width + layer; x++)
                    {
                        int srcX = Mathf.Clamp(x, 0, rect.width - 1);
                        Color edgeColor = pixels[srcX]; // 取最上边的像素
                        int dstX = rect.x + x;
                        if (dstX >= 0 && dstX < atlasSize)
                        {
                            atlas.SetPixel(dstX, rect.y - layer, edgeColor);
                        }
                    }
                }
                
                // 下边缘扩展
                if (rect.y + rect.height + layer - 1 < atlasSize)
                {
                    for (int x = -layer; x < rect.width + layer; x++)
                    {
                        int srcX = Mathf.Clamp(x, 0, rect.width - 1);
                        Color edgeColor = pixels[(rect.height - 1) * rect.width + srcX]; // 取最下边的像素
                        int dstX = rect.x + x;
                        if (dstX >= 0 && dstX < atlasSize)
                        {
                            atlas.SetPixel(dstX, rect.y + rect.height + layer - 1, edgeColor);
                        }
                    }
                }
            }
        }

        atlas.Apply();
        return atlas;
    }

    private static Texture2D MakeTextureReadable(Texture2D texture)
    {
        // 检查纹理的颜色空间设置
        bool isLinear = false;
        string texturePath = AssetDatabase.GetAssetPath(texture);
        if (!string.IsNullOrEmpty(texturePath))
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                isLinear = importer.sRGBTexture == false;
            }
        }
        
        // 根据纹理的颜色空间设置选择正确的RenderTexture格式
        RenderTextureReadWrite readWrite = isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
        RenderTexture renderTex = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, readWrite);
        
        Graphics.Blit(texture, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        
        Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, isLinear);
        readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableTexture.Apply();
        
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        
        return readableTexture;
    }

    public static Vector2[] RemapUVs(Vector2[] originalUVs, int[] triangles, List<Material> materials, PackResult packResult)
    {
        if (packResult == null || originalUVs == null)
            return originalUVs;

        Vector2[] newUVs = new Vector2[originalUVs.Length];
        
        for (int i = 0; i < originalUVs.Length; i++)
        {
            // 简单实现：假设使用第一个纹理的映射
            // 实际项目中需要根据材质和submesh来确定使用哪个纹理的映射
            int textureIndex = 0; // 这里需要更复杂的逻辑来确定顶点对应的纹理
            
            if (textureIndex < packResult.uvOffsets.Length)
            {
                Vector2 originalUV = originalUVs[i];
                Vector2 offset = packResult.uvOffsets[textureIndex];
                Vector2 scale = packResult.uvScales[textureIndex];
                
                newUVs[i] = new Vector2(
                    offset.x + originalUV.x * scale.x,
                    offset.y + originalUV.y * scale.y
                );
            }
            else
            {
                newUVs[i] = originalUVs[i];
            }
        }
        
        return newUVs;
    }
} 