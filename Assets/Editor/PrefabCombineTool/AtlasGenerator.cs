using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        
        // 初始化为透明
        var clearPixels = new Color[atlasSize * atlasSize];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = Color.clear;
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
                readableTexture = TextureScaleUtility.CreateScaledCopy(readableTexture, rect.width, rect.height);
            }

            // 复制像素到atlas
            var pixels = readableTexture.GetPixels();
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
        }

        atlas.Apply();
        return atlas;
    }

    private static Texture2D MakeTextureReadable(Texture2D texture)
    {
        // 创建可读纹理副本
        RenderTexture renderTex = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        
        Graphics.Blit(texture, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        
        Texture2D readableTexture = new Texture2D(texture.width, texture.height);
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