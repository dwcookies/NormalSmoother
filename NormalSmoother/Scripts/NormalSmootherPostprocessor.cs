using System.IO;
using UnityEditor;
using UnityEngine;

public class NormalSmootherPostprocessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject g)
    {
        if (!g.name.Contains("CK_NormalSmoothed_")) return;
        
        //平滑物体法线到顶点色
        //NormalSmootherUtility.AverageObjectNormalsToMeshColors(g);//单线程方法，7代i7，处理39147个三角形模型时，1000次测试平均耗时27ms
        NormalSmootherUtility.AverageObjectNormalsToMeshColorsByJob(g);//7代i7，处理39147个三角形模型时，1000次测试平均耗时4ms (在Job类前去掉[BurstCompile]后为8ms)

        //让其变回原来的名字
        ModelImporter modelImporter = assetImporter as ModelImporter;
        string path = modelImporter.assetPath;
        string newName = g.name.Remove(0, 18);
        string message = AssetDatabase.RenameAsset(path, newName);
        if (message.Length > 0)
        {
            AssetDatabase.DeleteAsset(Path.GetDirectoryName(path) + "\\" + newName + Path.GetExtension(path));
            AssetDatabase.RenameAsset(path, newName);
        }
        
        Debug.Log("NormalSmoother: 输出成功!");
    }
}
