using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor.SceneManagement;

public class NormalSmoother : EditorWindow
{
    [MenuItem("Tools/NormalSmoother")]
    public static void ShowWindow() => EditorWindow.GetWindow(typeof(NormalSmoother));

    private string path = "/NormalSmoothed";
    private string fixedPath;

    private void OnGUI()
    {
        GUILayout.Space(30);
        GUILayout.Label("本工具将会对选择的模型法线进行平滑并输出到顶点色", EditorStyles.boldLabel);
        GUILayout.Space(30);
        
        GUILayout.Label("平滑法线预览（不会进行保存）：");
        if (GUILayout.Button("平滑法线预览")) PreviewSmoothNormal(true);
        GUILayout.Space(30);
        
        GUILayout.Label("使用单线程进行平滑法线预览（不会进行保存）（无Job优化）：");
        if (GUILayout.Button("单线程进行平滑法线预览")) PreviewSmoothNormal(false);
        GUILayout.Space(50);
        
        GUILayout.Space(30);
        GUILayout.Label("输出选项：",EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("输出路径(已在Unity的Assets下)：");
        path = GUILayout.TextField(path);
        fixedPath = path.StartsWith("/") ? path : "/" + path;
        fixedPath = fixedPath.EndsWith("/") ? fixedPath : fixedPath + "/";
        GUILayout.Space(10);
        GUILayout.Label("在路径下新建一个模型，并将平滑法线输出到新模型顶点色中：");
        if (GUILayout.Button("输出到路径")) ExportSmoothNormalMesh();
        GUILayout.Space(25);
    }

    //预览功能，将平滑法线直接写入原网格的顶点色，在重启unity之后会复原
    private void PreviewSmoothNormal(bool useJob)
    {
        Debug.Log("NormalSmoother: 平滑法线预览");

        //获取选中物体
        Object obj = Selection.activeObject;
        if (obj == null)
        {
            Debug.LogError("NormalSmoother: 未选中物体");
            return;
        }
        
        //平滑物体法线到顶点色
        if (useJob) NormalSmootherUtility.AverageObjectNormalsToMeshColorsByJob(obj);
        else NormalSmootherUtility.AverageObjectNormalsToMeshColors(obj);
        
        Debug.Log("NormalSmoother: 法线平滑成功!");
    }

    //在指定路径生成新模型，在AssetDatabase.ImportAsset后，NormalSmootherPostprocessor的OnPostprocessModel会进行下一步操作
    private void ExportSmoothNormalMesh()
    {
        Debug.Log("NormalSmoother: 输出平滑法线到新模型");
        
        //获取选中物体
        Object obj = Selection.activeObject;
        if (obj == null)
        {
            Debug.LogError("NormalSmoother: 未选中物体");
            return;
        }

        //获取选中物体的资源路径，并通过文本框输入的路径获得输出路径
        string srcPath = AssetDatabase.GetAssetPath(obj);//选中的物体在Project时，通过这个方法直接获取路径
        if (srcPath.Length < 1)
            srcPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj));//物体在Scene中，则需先通过GetCorrespondingObjectFromOriginalSource()获取对应预制体资源
        if (srcPath.Length < 1)
        {
            var prefabStage = PrefabStageUtility.GetPrefabStage((GameObject)obj);//物体在PrefabMode Scene中，则需通过GetPrefabStage获取对应预制体Stage再获取对应路径
            srcPath = prefabStage is null ? srcPath : prefabStage.assetPath;
        }
        if (srcPath.Length < 1)
        {
            Debug.LogError("NormalSmoother: 无法正确获取选中物体的资源路径（可能原因：物体为Unity自带模型，如立方体等。或者物体不是预制体。）");
            return;
        }
        Debug.Log("NormalSmoother: 选中物体的资源路径为 " + srcPath);
        string dstPath = "Assets" + fixedPath;
        if (!Directory.Exists(dstPath)) Directory.CreateDirectory(dstPath);
        AssetDatabase.Refresh();
        Debug.Log("NormalSmoother: 输出模型的路径为 " + dstPath + Path.GetFileName(srcPath));
        dstPath += "NormalSmoothed_" + Path.GetFileName(srcPath);

        //复制资源并导入，之后会转交给NormalSmootherPostprocess进行操作
        AssetDatabase.CopyAsset(srcPath, dstPath);
        AssetDatabase.ImportAsset(dstPath);
    }
}

//工具类
public static class NormalSmootherUtility
{
    public static void AverageObjectNormalsToMeshColors(Object obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            mesh.colors = AverageMeshNormalsToMeshColors(mesh);
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;
            mesh.colors = AverageMeshNormalsToMeshColors(mesh);
        }
    }
    
    public static Color[] AverageMeshNormalsToMeshColors(Mesh mesh)
    {
        Dictionary<Vector3, Vector3> averageNormalMap = new Dictionary<Vector3, Vector3>();
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Color[] colors = mesh.colors;

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            if (!averageNormalMap.TryAdd(vertices[i], normals[i]))
                averageNormalMap[vertices[i]] += normals[i];
        }

        if (colors.Length == 0) colors = new Color[vertices.Length];
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Vector3 normal = normals[i];
            Vector4 tangent = tangents[i];
            Vector3 bitangent = (Vector3.Cross(normal, new Vector3(tangent.x, tangent.y, tangent.z)) * tangent.w).normalized;
            Matrix4x4 TBN = new Matrix4x4(tangent, bitangent, normal, Vector3.zero);
            TBN = TBN.transpose;//由于脚本内矩阵构建和shader不一样，是一列一列构建的，所以要转置一下。
            
            Vector3 averageNormal = averageNormalMap[vertices[i]].normalized;
            averageNormal = TBN.MultiplyVector(averageNormal);
            
            Color temp = colors[i];
            temp.r = averageNormal.x * 0.5f + 0.5f;
            temp.g = averageNormal.y * 0.5f + 0.5f;
            colors[i] = temp;
        }

        return colors;
    }

    public static void CopyMesh(Mesh src, Mesh dest)
    {
        dest.Clear();
        dest.name = src.name;
        dest.vertices = src.vertices;
        dest.normals = src.normals;
        dest.tangents = src.tangents;
        dest.colors = src.colors;
        dest.colors32 = src.colors32;
        dest.boneWeights = src.boneWeights;
        dest.bindposes = src.bindposes;
    
        List<Vector4> uvs = new List<Vector4>();
        src.GetUVs(0, uvs); dest.SetUVs(0, uvs);
        src.GetUVs(1, uvs); dest.SetUVs(1, uvs);
        src.GetUVs(2, uvs); dest.SetUVs(2, uvs);
        src.GetUVs(3, uvs); dest.SetUVs(3, uvs);
        src.GetUVs(4, uvs); dest.SetUVs(4, uvs);
        src.GetUVs(5, uvs); dest.SetUVs(5, uvs);
        src.GetUVs(6, uvs); dest.SetUVs(6, uvs);
        src.GetUVs(7, uvs); dest.SetUVs(7, uvs);
    
        dest.subMeshCount = src.subMeshCount;
        for (int i = 0; i < dest.subMeshCount; i++)
            dest.SetIndices(src.GetIndices(i), src.GetTopology(i), i);
    }

    public static void AverageObjectNormalsToMeshColorsByJob(Object obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            mesh.colors = AverageMeshNormalsToMeshColorsByJob(mesh);
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;
            mesh.colors = AverageMeshNormalsToMeshColorsByJob(mesh);
        }
    }
    
    public static Color[] AverageMeshNormalsToMeshColorsByJob(Mesh mesh)
    {
        NativeArray<Vector3> vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.Persistent);
        int arrayLength = vertices.Length;
        NativeArray<Vector3> normals = new NativeArray<Vector3>(mesh.normals, Allocator.Persistent);
        NativeArray<Vector4> tangents = new NativeArray<Vector4>(mesh.tangents, Allocator.Persistent);
        NativeMultiHashMap<Vector3, Vector3> multiHashMap =
            new NativeMultiHashMap<Vector3, Vector3>(arrayLength, Allocator.Persistent);
        NativeArray<Color> colors = new NativeArray<Color>(mesh.colors.Length == 0 ? new Color[arrayLength] : mesh.colors, Allocator.Persistent);

        HashmapNormalsJob hashmapNormalsJob = new HashmapNormalsJob(vertices, normals, multiHashMap.AsParallelWriter());
        ComputeSmoothedNormalJob computeSmoothedNormalJob =
            new ComputeSmoothedNormalJob(vertices, normals, tangents, multiHashMap, colors);

        JobHandle handle = computeSmoothedNormalJob.Schedule(arrayLength, 128, hashmapNormalsJob.Schedule(arrayLength, 128));
        handle.Complete();
        Color[] finalColor = colors.ToArray();
        
        //释放内存
        vertices.Dispose();
        normals.Dispose();
        tangents.Dispose();
        multiHashMap.Dispose();
        colors.Dispose();

        return finalColor;
    }

    [BurstCompile]
    public struct HashmapNormalsJob : IJobParallelFor
    {
        [ReadOnly] 
        public NativeArray<Vector3> vertexs, normals;
        public NativeMultiHashMap<Vector3, Vector3>.ParallelWriter multiHashMap;

        public HashmapNormalsJob(NativeArray<Vector3> vertexs, NativeArray<Vector3> normals,
            NativeMultiHashMap<Vector3, Vector3>.ParallelWriter multiHashMap)
        {
            this.vertexs = vertexs;
            this.normals = normals;
            this.multiHashMap = multiHashMap;
        }

        public void Execute(int index) => multiHashMap.Add(vertexs[index], normals[index]);
    }
    
    [BurstCompile]
    public struct ComputeSmoothedNormalJob : IJobParallelFor
    {
        [ReadOnly] 
        public NativeArray<Vector3> vertexs, normals;
        [ReadOnly]
        public NativeArray<Vector4> tangents;
        [ReadOnly] 
        public NativeMultiHashMap<Vector3, Vector3> multiHashMap;
        public NativeArray<Color> colors;

        public ComputeSmoothedNormalJob(NativeArray<Vector3> vertexs, NativeArray<Vector3> normals,
            NativeArray<Vector4> tangents,
            NativeMultiHashMap<Vector3, Vector3> multiHashMap, NativeArray<Color> colors)
        {
            this.vertexs = vertexs;
            this.normals = normals;
            this.tangents = tangents;
            this.multiHashMap = multiHashMap;
            this.colors = colors;
        }

        public void Execute(int index)
        {
            Vector3 smoothedNormal = Vector3.zero;
            NativeMultiHashMap<Vector3,Vector3>.Enumerator enumerator = multiHashMap.GetValuesForKey(vertexs[index]);
            while (enumerator.MoveNext()) smoothedNormal += enumerator.Current;
            smoothedNormal = smoothedNormal.normalized;

            Vector3 normal = normals[index];
            Vector4 tangent = tangents[index];
            Vector3 bitangent = (Vector3.Cross(normal, tangent) * tangent.w).normalized;
            Matrix4x4 TBN = new Matrix4x4(tangent, bitangent, normal, Vector3.zero);
            TBN = TBN.transpose;
            smoothedNormal = TBN.MultiplyVector(smoothedNormal);

            Color color = colors[index];
            color.r = smoothedNormal.x * 0.5f + 0.5f;
            color.g = smoothedNormal.y * 0.5f + 0.5f;
            colors[index] = color;
        }
    }
}