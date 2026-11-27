using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using VaroGO.Client;
using Object=UnityEngine.Object;
namespace HiddenPath.Stage2Z3.Editor
{
    public static class Z3PuzzleAudit
    {
        static Z3Puzzle puzzle;static PlayerController p;static PlayerInputReader input;
        static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static string report="";
        static void Log(string s){report+=s+"\n";System.IO.File.WriteAllText("Captures/Stage2_Z3/play-audit.txt",report);}
        static void Place(PlayerController actor,Vector3 pos){actor.Respawn(pos+Vector3.up*.03f,Quaternion.identity);Physics.SyncTransforms();}
        public static string Setup()
        {
            if(!Application.isPlaying)throw new InvalidOperationException();
            puzzle=Object.FindFirstObjectByType<Z3Puzzle>();puzzle.Begin();puzzle.enabled=false;
            foreach(var a in puzzle.Actors)a.GetComponent<PlayerInputReader>().enabled=false;
            report="";Log("Offline two-player prototype; actual PlayerController/CharacterController.");
            var large=puzzle.pieces.First(x=>x.fittingStep==0);Place(puzzle.Actors[0],large.home+Vector3.forward*2);Place(puzzle.Actors[1],new Vector3(-34,6,98));
            Log("Large piece solo rejected="+!puzzle.Pick(large));
            var wrong=puzzle.pieces.First(x=>x.fittingStep<0);Place(puzzle.Actors[0],wrong.home+Vector3.forward*2);puzzle.Pick(wrong);Place(puzzle.Actors[0],new Vector3(-55,6,96.5f));Log("Wrong size rejected="+!puzzle.Insert());puzzle.ResetPuzzle();
            for(int i=0;i<6;i++)
            {
                var piece=puzzle.pieces.First(x=>x.fittingStep==i);
                if(i==0)
                {
                    puzzle.Switch(0);Place(puzzle.Actors[0],puzzle.PushHandle(piece,0));Log("First pusher joins="+puzzle.JoinPush());var start=piece.transform.position;
                    puzzle.PushTick(1,true,false);Log("Single pusher cannot move="+(Vector3.Distance(start,piece.transform.position)<.001f));
                    puzzle.Switch(1);Place(puzzle.Actors[1],puzzle.PushHandle(piece,1));Log("Second pusher joins="+puzzle.JoinPush());
                    puzzle.PushTick(1,true,false);Log("Two grips but one force cannot move="+(Vector3.Distance(start,piece.transform.position)<.001f));
                    for(int n=0;n<1200&&puzzle.StepCount==0;n++)puzzle.PushTick(1f/60,true,true);
                    Log("Cooperative push reaches base socket="+(puzzle.StepCount==1));puzzle.Switch(0);continue;
                }
                if(piece.visibleRole>=0)
                {
                    puzzle.Switch(1-piece.visibleRole);Place(puzzle.Actors[puzzle.Active],piece.home+Vector3.forward*2);
                    Log("Special "+i+" hidden from other role="+piece.GetComponentsInChildren<Renderer>().All(r=>!r.enabled));
                    Log("Other role cannot pick special="+!puzzle.Pick(piece));puzzle.Switch(piece.visibleRole);
                    Log("Special visible to owner="+piece.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
                }
                Place(puzzle.Actors[puzzle.Active],piece.home+Vector3.forward*2);
                bool picked=puzzle.Pick(piece);Place(puzzle.Actors[puzzle.Active],puzzle.assemblyPoint);Log("Correct block "+i+" pick/place="+(picked&&puzzle.Insert()));
            }
            puzzle.Switch(0);
            p=puzzle.Actors[0];input=p.GetComponent<PlayerInputReader>();
            var axis=new GameObject("Z3_Audit_Axis");typeof(PlayerController).GetField("cameraTransform",F).SetValue(p,axis.transform);
            Place(puzzle.Actors[1],new Vector3(-60,6,96));
            Place(p,new Vector3(-55,6,96.5f));
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                for(int i=0;i<6;i++)Log("Stair jump "+(i+1)+"="+JumpTo(new Vector3(-55,7+i,98.85f+i*1.4f)));
                Log("Solo jump cannot reach crawl lip="+!JumpTo(new Vector3(-55,15.2f,108.9f)));
            }
            finally{Physics.simulationMode=mode;}
            Place(p,new Vector3(-55,12,106));Log("Partner braces="+puzzle.Brace());puzzle.Switch(1);
            p=puzzle.Actors[1];input=p.GetComponent<PlayerInputReader>();input.enabled=false;typeof(PlayerController).GetField("cameraTransform",F).SetValue(p,axis.transform);
            Place(p,new Vector3(-55,12,105));Log("Mount initiated="+puzzle.Mount());return report;
        }
        static void Tick(Vector2 move,bool jump=false,bool prone=false)
        {
            typeof(PlayerInputReader).GetProperty("Move").SetValue(input,move);
            typeof(PlayerInputReader).GetField("jumpPressed",F).SetValue(input,jump);
            typeof(PlayerInputReader).GetField("pronePressed",F).SetValue(input,prone);
            Physics.SyncTransforms();p.Simulate(1f/60);Physics.Simulate(1f/60);
        }
        static bool JumpTo(Vector3 end)
        {
            for(int i=0;i<10;i++)Tick(Vector2.zero);
            bool air=false;
            for(int i=0;i<120;i++)
            {
                var d=end-p.transform.position;d.y=0;d=d.magnitude<.1f?Vector3.zero:d.normalized;
                Tick(new Vector2(d.x,d.z),i==0);if(!p.IsGrounded)air=true;
                if(air&&p.IsGrounded&&i>8)return Vector3.Distance(p.transform.position,end)<.65f;
            }
            return false;
        }
        public static string AfterMount()
        {
            if(puzzle.Busy)return "Wait for mount";
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                Log("Shoulder height="+p.transform.position.y);
                Log("Shoulder to crawl lip jump="+JumpTo(new Vector3(-55,15.2f,108.9f)));
                for(int i=0;i<100;i++)Tick(Vector2.up);
                Log("Standing blocked by low tunnel="+(p.transform.position.z<110.5f));
                for(int i=0;i<200;i++)Tick(Vector2.up,false,i==0);
                Log("Prone traverses tunnel="+(p.transform.position.z>111.5f));
            }
            finally{Physics.simulationMode=mode;}
            Log("Gate locked before rescue="+puzzle.exitGate.activeSelf);
            Place(p,puzzle.ropeCoil.transform.position+Vector3.forward);Log("Rope found="+puzzle.CollectRope());
            Place(p,puzzle.ropeAnchor.position);Log("Rope installed="+puzzle.AttachRope());Log("Pull without partner at dock rejected="+!puzzle.Pull());
            Place(puzzle.Actors[0],puzzle.rescueBottom.position);Log("Pull started="+puzzle.Pull());return report;
        }
        public static string Finish()
        {
            if(puzzle.Busy)return "Wait for rope";
            Log("Rescue reaches upper dock="+(Vector3.Distance(puzzle.Actors[0].transform.position,puzzle.rescueTop.position)<.2f));
            Log("Exit unlocks after rescue="+(puzzle.Rescued&&!puzzle.exitGate.activeSelf));
            puzzle.ResetPuzzle();Log("Reset restores all="+(puzzle.StepCount==0&&!puzzle.RopeFound&&!puzzle.Rescued&&puzzle.exitGate.activeSelf&&puzzle.pieces.All(x=>!x.placed)));
            puzzle.enabled=true;return report;
        }
    }
}

