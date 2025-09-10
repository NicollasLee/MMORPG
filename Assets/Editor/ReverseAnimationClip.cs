// Assets/Editor/ReverseAnimationClip.cs
using UnityEditor;
using UnityEngine;

public static class ReverseAnimationClip
{
    [MenuItem("Tools/Animation/Create Reversed Copy", priority = 100)]
    public static void CreateReversed()
    {
        var obj = Selection.activeObject as AnimationClip;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("Reverse Animation", "Selecione um AnimationClip no Project.", "Ok");
            return;
        }

        // Duplicar o clipe
        string path = AssetDatabase.GetAssetPath(obj);
        string newPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(path),
            System.IO.Path.GetFileNameWithoutExtension(path) + "_Reversed.anim"
        ).Replace("\\", "/");

        var newClip = new AnimationClip();
        EditorUtility.CopySerialized(obj, newClip);
        AssetDatabase.CreateAsset(newClip, newPath);

        // Inverter todas as curvas
        float length = obj.length;
        foreach (var binding in AnimationUtility.GetCurveBindings(newClip))
        {
            var curve = AnimationUtility.GetEditorCurve(newClip, binding);
            for (int i = 0; i < curve.keys.Length; i++)
            {
                var k = curve.keys[i];
                k.time = length - k.time;
                // tangentes invertidas (troca in/out)
                float inT = -k.inTangent;
                k.inTangent = -k.outTangent;
                k.outTangent = inT;
                curve.keys[i] = k;
            }
            // reordena por tempo (já que invertimos)
            System.Array.Sort(curve.keys, (a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetEditorCurve(newClip, binding, curve);
        }

        // Inverter eventos também (se houver)
        var events = AnimationUtility.GetAnimationEvents(newClip);
        for (int i = 0; i < events.Length; i++)
        {
            events[i].time = Mathf.Max(0f, length - events[i].time);
        }
        AnimationUtility.SetAnimationEvents(newClip, events);

        EditorUtility.SetDirty(newClip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Reverse Animation", $"Criado: {newPath}", "Ok");
        Selection.activeObject = newClip;
    }
}
