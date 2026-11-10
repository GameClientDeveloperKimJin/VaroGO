using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenPath.Stage2.Editor
{
    [InitializeOnLoad]
    public static class Stage2EditorProbe
    {
        static Stage2EditorProbe() { EditorApplication.delayCall += Inspect; }
        static void Inspect()
        {
            var s = SceneManager.GetActiveScene();
            File.WriteAllText("Library/Stage2EditorProbe.txt", "active=" + s.path + "\ndirty=" + s.isDirty + "\nplaying=" + Application.isPlaying + "\n");
        }
    }
}
