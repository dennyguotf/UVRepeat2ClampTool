using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public class TestTool : EditorWindow
    {

        [MenuItem("GameEditor/测试", false, 5)]
        static void MapTestssOne2()
        {
            GameObject go = Selection.activeGameObject;
            MeshFilter[] mfList = go.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter mf in mfList)
            {
                Mesh mesh = mf.sharedMesh;
                Vector2[] uvs = mesh.uv;
                for(int i = 0; i < uvs.Length; i++)  
                {
                    if (uvs[i].x < 0 || uvs[i].x > 1 || uvs[i].y < 0 || uvs[i].y > 1)
                    {
                        Debug.LogError(mf.gameObject.name + "==" + i + "==" + uvs[i]);
                    }
                }
            }

        } 
    }
}
