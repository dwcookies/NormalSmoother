#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;

public class NormalSmoother : EditorWindow
{
    [MenuItem("Tools/NormalSmoother")]
    public static void ShowWindow() => GetWindow(typeof(NormalSmoother));

    private string path = "/NormalSmoothed";
    private string fixedPath;

    private void OnGUI()
    {
        GUILayout.Space(30);
        GUILayout.Label("本工具将会对选择的模型法线进行平滑并输出到顶点色", EditorStyles.boldLabel);
        GUILayout.Label("版本：v2.0");
        GUILayout.Space(30);
        
        GUILayout.Label("平滑法线预览（不会进行保存）：");
        if (GUILayout.Button("平滑法线预览")) PreviewSmoothNormal(true);
        GUILayout.Space(30);
        
        GUILayout.Label("使用单线程进行平滑法线预览（不会进行保存）（无Job优化）：");
        if (GUILayout.Button("单线程进行平滑法线预览")) PreviewSmoothNormal(false);
        GUILayout.Space(50);
        
        GUILayout.Space(30);
        GUILayout.Label("保存选项：",EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("保存路径(已在Unity的Assets下)：");
        path = GUILayout.TextField(path);
        fixedPath = path.StartsWith("/") ? path : "/" + path;
        fixedPath = fixedPath.EndsWith("/") ? fixedPath : fixedPath + "/";
        GUILayout.Space(10);
        GUILayout.Label("在路径下新建一个资源，并将平滑法线输出到新网格顶点色中：");
        if (GUILayout.Button("输出到路径")) ExportSmoothNormalMesh();
        GUILayout.Space(10);
        GUILayout.Label("如果对象的对应模型已生成过网格，则可以直接给他们使用而不需要再次生成：");
        if (GUILayout.Button("使用对应的已生成的网格")) SetSmoothNormalToMesh();
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
        Debug.Log("NormalSmoother: 平滑法线并保存到网格资源文件");
        
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
        Debug.Log("NormalSmoother: 输出网格的路径为 " + dstPath + Path.GetFileName(srcPath).Replace(".fbx", "") + "/");
        dstPath += "NormalSmoothed_" + Path.GetFileName(srcPath);

        //复制资源并导入，之后会转交给NormalSmootherPostprocess进行生成网格资源的操作
        AssetDatabase.CopyAsset(srcPath, dstPath);
        AssetDatabase.ImportAsset(dstPath);
        
        //对对象的网格进行赋值
        SetSmoothNormalToMesh();
    }

    public void SetSmoothNormalToMesh()
    {
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
        string dstPath = "Assets" + fixedPath;
        if (!Directory.Exists(dstPath))
        {
            Debug.LogError("NormalSmoother: 未找到平滑法线网格，请先生成网格");
            return;
        }
        
        //将生成网格的赋给物体
        if (!AssetDatabase.IsValidFolder(dstPath))
        {
            Debug.LogError("NormalSmoother: 未找到平滑法线网格，请先生成网格");
            return;
        }
        dstPath += Path.GetFileName(srcPath).Replace(".fbx", "") + "/";
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(dstPath + meshFilter.sharedMesh.name + ".asset");
            if (mesh == null)
            {
                Debug.LogError("NormalSmoother: 未找到平滑法线网格，请先生成网格");
                return;
            }
            meshFilter.sharedMesh = mesh;
        }
        SkinnedMeshRenderer[] skinnedMeshRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(dstPath + skinnedMeshRenderer.sharedMesh.name + ".asset");
            if (mesh == null)
            {
                Debug.LogError("NormalSmoother: 未找到平滑法线网格，请先生成网格");
                return;
            }
            skinnedMeshRenderer.sharedMesh = mesh;
        }
    }
}
#endif