#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class NormalSmootherPostprocessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject g)
    {
        if (!g.name.Contains("NormalSmoothed_")) return;
        
        Debug.Log("NormalSmoother: 已进入NormalSmootherPostprocessor");
        
        //平滑物体法线到顶点色
        //NormalSmootherUtility.AverageObjectNormalsToMeshColors(g);//单线程方法，7代i7，处理39147个三角形模型时，1000次测试平均耗时27ms
        NormalSmootherUtility.AverageObjectNormalsToMeshColorsByJob(g);//7代i7，处理39147个三角形模型时，1000次测试平均耗时4ms (在Job类前去掉[BurstCompile]后为8ms)
        
        ModelImporter modelImporter = assetImporter as ModelImporter;
        string meshPath = Path.GetDirectoryName(modelImporter.assetPath) + "\\" + g.name.Remove(0, 15) + "\\";
        if (!AssetDatabase.IsValidFolder(meshPath)) AssetDatabase.CreateFolder(Path.GetDirectoryName(modelImporter.assetPath), g.name.Remove(0, 15));
        MeshFilter[] meshFilters = g.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = new Mesh();
            NormalSmootherUtility.CopyMesh(meshFilter.sharedMesh, mesh);
            AssetDatabase.CreateAsset(mesh, meshPath + mesh.name + ".asset");
            meshFilter.sharedMesh = mesh;
        }
        SkinnedMeshRenderer[] skinnedMeshRenderers = g.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            Mesh mesh = new Mesh();
            NormalSmootherUtility.CopyMesh(skinnedMeshRenderer.sharedMesh, mesh);
            AssetDatabase.CreateAsset(mesh, meshPath + mesh.name + ".asset");
            skinnedMeshRenderer.sharedMesh = mesh;
        }

        AssetDatabase.DeleteAsset(modelImporter.assetPath);
        
        Debug.Log("NormalSmoother: 输出成功!");
    }
}
#endif