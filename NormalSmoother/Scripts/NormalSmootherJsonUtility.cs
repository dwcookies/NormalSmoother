#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NormalSmootherJsonUtility
{
    public static string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(Activator.CreateInstance(typeof(NormalSmootherJsonUtility)) as ScriptableObject))) + "identification.json";
    public static string advancePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(Activator.CreateInstance(typeof(NormalSmootherJsonUtility)) as ScriptableObject))) + "advanceIdentification.json";

    public static bool CheckIdentification(string name) => GetHashSetFromJson().Contains(name);
    public static bool SaveIdentification(string name)
    {
        var hashSet = GetHashSetFromJson();
        var res = hashSet.Add(name);
        SetHashSetToJson(hashSet);
        Debug.Log("NormalSmoother: " + name + "已写入identification.json");
        return res;
    }
    public static bool CheckAdvanceIdentification(string name)
    {
        var list = GetListFromAdvanceJson();
        foreach (string element in list)
            if (name.Contains(element)) return true;
        return false;
    }
    public static void SaveAdvanceIdentification(string name)
    {
        var list = GetListFromAdvanceJson();
        list.Add(name);
        SetListToAdvanceJson(list);
        Debug.Log("NormalSmoother: " + name + "已写入advanceIdentification.json");
    }

    public static void SetHashSetToJson(HashSet<string> hashSet) => SaveJson(SerializeHashSet(hashSet));
    public static HashSet<string> GetHashSetFromJson() => DeserializeHashSet(LoadJson());
    public static void SetListToAdvanceJson(List<string> list) => SaveAdvanceJson(JsonUtility.ToJson(list));
    public static List<string> GetListFromAdvanceJson() => JsonUtility.FromJson<List<string>>(LoadAdvanceJson());

    public static void SaveJson(string json) => File.WriteAllText(path, json);
    public static string LoadJson() => File.Exists(path) ? File.ReadAllText(path) : null;
    public static void SaveAdvanceJson(string json) => File.WriteAllText(advancePath, json);
    public static string LoadAdvanceJson() => File.Exists(advancePath) ? File.ReadAllText(advancePath) : null;
    
    public static string SerializeHashSet(HashSet<string> hashSet) => JsonUtility.ToJson(new HashSetWrapper<string>(hashSet));
    public static HashSet<string> DeserializeHashSet(string json) => JsonUtility.FromJson<HashSetWrapper<string>>(json).ToHashSet();
}

[System.Serializable]
public class HashSetWrapper<T>
{
    public List<T> items;
    public HashSetWrapper(HashSet<T> hashSet) => items = new List<T>(hashSet);
    public HashSet<T> ToHashSet() => new HashSet<T>(items);
}
#endif