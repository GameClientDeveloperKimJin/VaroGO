using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Client;

namespace HiddenPath.Stage2.Editor
{
    [InitializeOnLoad]
    public static class Stage2PlayCheck
    {
        static double started;
        static Stage2PlayCheck(){EditorApplication.update+=Tick;}
        public static void Begin()
        {
            if(SceneManager.GetActiveScene().path!="Assets/01_Scenes/MapDesign_Stage2.unity")throw new System.Exception("Scene guard failed.");
            SessionState.SetBool("HiddenPath.Stage2.PlayCheck",true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool("HiddenPath.Stage2.PlayCheck",false))return;
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            if(started==0){started=EditorApplication.timeSinceStartup;return;}
            if(EditorApplication.timeSinceStartup-started<5)return;
            var p=Object.FindFirstObjectByType<PlayerController>();var preview=Object.FindFirstObjectByType<Stage2DungeonPreview>();
            string report="Scene="+SceneManager.GetActiveScene().name+"\nPlayer exists="+(p!=null)+"\n";
            if(p!=null)report+="Player local="+p.IsLocal+"\nPlayer grounded="+p.IsGrounded+"\nSpawn position="+p.transform.position+"\n";
            if(preview!=null)report+="Preview running="+preview.enabled+"\nMirror door heights="+preview.mirrorDoors[0].position.y+","+preview.mirrorDoors[1].position.y+"\nSwing count="+Object.FindObjectsByType<Stage2SwingPlatform>(FindObjectsSortMode.None).Length+"\nOptional acorns="+preview.rewardAcorns.Length+"\n";
            File.WriteAllText("Captures/Stage2_Dungeon/playcheck-report.txt",report);SessionState.SetBool("HiddenPath.Stage2.PlayCheck",false);EditorApplication.isPlaying=false;started=0;
        }
    }
}
