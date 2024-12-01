#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;

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
#endif