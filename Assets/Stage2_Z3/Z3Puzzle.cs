using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Client;
using VaroGO.Shared;
namespace HiddenPath.Stage2Z3
{
    // Isolated offline playable blockout. No shared player assets or other scenes are changed.
    public class Z3Puzzle:MonoBehaviour
    {
        public GameObject playerPrefab,exitGate,rescueGate,ropeCoil,ropeLine;
        public Transform[] slots;
        public Transform bracePoint,ropeAnchor,rescueBottom,rescueTop;
        public Z3Piece[] pieces;
        public PlayerController[] Actors {get;private set;}
        public int StepCount {get;private set;}
        public bool RopeFound {get;private set;}
        public bool RopeReady {get;private set;}
        public bool Rescued {get;private set;}
        public bool Running=>Actors!=null;
        public int Active {get;private set;}
        public Z3Piece Held {get;private set;}
        public int Braced {get;private set;}=-1;
        public bool Busy {get;private set;}
        public Vector3 assemblyPoint=new Vector3(-55,6,96.5f);
        public Z3Piece Pushing {get;private set;}
        readonly bool[] pushGrip=new bool[2];
        public Vector3 PushHandle(Z3Piece piece,int actor)=>piece.transform.position+new Vector3(actor==0?-1.1f:1.1f,-.5f,-piece.transform.localScale.z*.5f-.85f);
        public bool JoinPush()
        {
            if(!Running||Busy||Held||Braced>=0)return false;
            if(pushGrip[Active]){ReleasePush();return true;}
            var piece=pieces.FirstOrDefault(x=>x.needsTwo&&!x.placed);
            if(!piece||StepCount!=piece.fittingStep||!Near(PushHandle(piece,Active),1.8f))return false;
            Pushing=piece;pushGrip[Active]=true;Actors[Active].Respawn(PushHandle(piece,Active)+Vector3.up*.03f,Quaternion.identity);Actors[Active].InputLocked=true;
            status="두 캐릭터가 각각 뒤쪽 손잡이에서 E로 밀기 참여. Tab 전환 후 함께 W로 밀기 · E 해제";return true;
        }
        public void ReleasePush()
        {for(int i=0;i<2;i++){pushGrip[i]=false;if(Running)Actors[i].InputLocked=false;}Pushing=null;}
        public bool PushTick(float dt,bool redForce,bool blueForce)
        {
            if(!Pushing||!pushGrip[0]||!pushGrip[1]||!redForce||!blueForce||dt<=0)return false;
            for(int i=0;i<2;i++)if(Vector3.Distance(Actors[i].transform.position,PushHandle(Pushing,i))>1.8f){ReleasePush();return false;}
            var destination=slots[StepCount].position;var delta=Vector3.ClampMagnitude(destination-Pushing.transform.position,1.6f*dt);delta.y=0;
            var next=Pushing.transform.position+delta;
            var size=Pushing.transform.localScale*.5f-new Vector3(.06f,.12f,.06f);
            if(Physics.OverlapBox(next,size,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(Pushing.transform)&&!c.GetComponentInParent<PlayerController>())){status="앞이 막혀 있습니다. 통로의 물체를 먼저 치우세요.";return false;}
            var previous=new[]{Actors[0].transform.position,Actors[1].transform.position};
            for(int i=0;i<2;i++)Actors[i].GetComponent<CharacterController>().Move(delta);
            if(Enumerable.Range(0,2).Any(i=>(Actors[i].transform.position-previous[i]-delta).magnitude>.12f))
            {for(int i=0;i<2;i++)Actors[i].Respawn(previous[i],Quaternion.identity);return false;}
            Pushing.transform.position=next;Physics.SyncTransforms();
            if(Vector3.Distance(next,destination)<.035f){Pushing.transform.position=destination;Pushing.placed=true;StepCount++;ReleasePush();status="대형 받침 설치 완료. 나머지 5개 부품을 조합하세요.";}
            return true;
        }
        GameObject[] oldPlayers;GameObject shoulder;PlayerTuning[] tunings;
        string status="F6: Z3 오프라인 2인 전환 테스트 시작";
        public void Begin()
        {
            if(GameServices.IsOnline){status="이 테스트는 오프라인 전용입니다.";return;}
            if(Running){ResetPuzzle();return;}
            oldPlayers=PlayerController.All.Select(p=>p.gameObject).ToArray();foreach(var p in oldPlayers)p.SetActive(false);
            Actors=new PlayerController[2];tunings=new PlayerTuning[2];
            for(int i=0;i<2;i++)
            {
                var go=Instantiate(playerPrefab,new Vector3(-34-i*2,6.05f,98),Quaternion.Euler(0,270,0));go.name="Z3_Test_"+(i==0?"Red":"Blue");
                foreach(var d in go.GetComponents<OfflinePlayerDriver>())d.enabled=false;
                Actors[i]=go.GetComponent<PlayerController>();
                tunings[i]=Instantiate(Actors[i].Tuning);tunings[i].ledgeGrabEnabled=false;tunings[i].ledgeAssist=false;
                typeof(PlayerController).GetField("tuning",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Actors[i],tunings[i]);
                Actors[i].SetLocal(false);Actors[i].SetSimulateWithoutInput(true);
            }
            shoulder=GameObject.CreatePrimitive(PrimitiveType.Cube);shoulder.name="Partner_Shoulder_Support";shoulder.transform.localScale=new Vector3(1.1f,.14f,1.1f);shoulder.GetComponent<Renderer>().enabled=false;shoulder.SetActive(false);
            Switch(0);ResetPuzzle();
        }
        public void Switch(int index)
        {
            if(!Running||Busy||Held)return;
            Active=index;
            for(int i=0;i<2;i++){Actors[i].SetLocal(i==Active);Actors[i].SetSimulateWithoutInput(i!=Active);}
            if(GameServices.Role is OfflineRoleProvider role)role.SetRole(index==0?PlayerRole.A_Red:PlayerRole.B_Blue);
            foreach(var piece in pieces)piece.ViewAs(index);
        }
        public void ResetPuzzle()
        {
            if(!Running)return;StopAllCoroutines();ReleasePush();Busy=false;Held=null;StepCount=0;Braced=-1;RopeFound=RopeReady=Rescued=false;
            foreach(var p in pieces){p.placed=false;p.transform.SetPositionAndRotation(p.home,p.homeRotation);p.Collision(true);}
            exitGate.SetActive(true);rescueGate.SetActive(true);ropeCoil.SetActive(true);ropeLine.SetActive(false);shoulder.SetActive(false);
            for(int i=0;i<2;i++){Actors[i].InputLocked=false;Actors[i].GetComponent<CharacterController>().enabled=true;Actors[i].Respawn(new Vector3(-34-i*2,6.05f,98),Quaternion.Euler(0,270,0));}
            status="대형 받침은 두 명이 함께 밀고, 특수 부품을 포함해 6단 계단을 완성하세요.";
        }
        bool Near(Vector3 p,float range)=>Vector3.Distance(Actors[Active].transform.position,p)<range;
        public bool Pick(Z3Piece piece)
        {
            if(!Running||Busy||Held||Pushing||piece.placed||!Near(piece.transform.position,4)||(piece.visibleRole>=0&&piece.visibleRole!=Active))return false;
            if(piece.needsTwo){status="이 오브젝트는 들 수 없습니다. 두 명이 뒤쪽 손잡이에서 함께 밀어야 합니다.";return false;}
            Held=piece;piece.Collision(false);if(piece.needsTwo)Actors[1-Active].InputLocked=true;status="계단 작업대에서 E로 배치. 다른 곳에서 E: 원위치에 내려놓기";return true;
        }
        public bool Insert()
        {
            if(!Held||StepCount>=slots.Length||!Near(assemblyPoint,4.5f))return false;
            if(Held.fittingStep!=StepCount){status="이 크기는 현재 단에 맞지 않습니다. 다른 오브젝트를 찾아보세요.";return false;}
            Held.transform.position=slots[StepCount].position;Held.transform.rotation=Quaternion.identity;Held.placed=true;Held.Collision(true);if(Held.needsTwo)Actors[1-Active].InputLocked=false;Held=null;StepCount++;
            status=StepCount==slots.Length?"계단 완성. 마지막 단에서 F로 파트너를 받쳐주세요.":"맞는 크기입니다. 다음 단에 사용할 오브젝트를 찾으세요.";return true;
        }
        public bool Brace()
        {
            if(!Running||Busy||Held||Pushing||StepCount!=slots.Length||!Near(bracePoint.position,1.5f))return false;
            if(Braced==Active){Actors[Active].InputLocked=false;Braced=-1;shoulder.SetActive(false);return true;}
            if(Braced>=0)return false;
            Braced=Active;Actors[Active].Respawn(bracePoint.position,Quaternion.identity);Actors[Active].InputLocked=true;
            shoulder.transform.position=bracePoint.position+Vector3.up*1.73f;shoulder.SetActive(true);status="Tab으로 동료 전환 → 가까이서 E로 어깨를 밟고 → Space로 턱에 점프";return true;
        }
        public bool Mount()
        {
            if(!Running||Busy||Held||Braced<0||Braced==Active||!Near(bracePoint.position,2.7f))return false;
            StartCoroutine(MoveActor(Active,bracePoint.position+Vector3.up*1.82f,.65f,false));return true;
        }
        public bool CollectRope()
        {
            if(!Running||Busy||Held||RopeFound||!Near(ropeCoil.transform.position,2.5f)||Actors[Active].transform.position.z<111)return false;
            RopeFound=true;ropeCoil.SetActive(false);status="밧줄 획득. 반대편 구조용 고리에 설치하세요.";return true;
        }
        public bool AttachRope()
        {
            if(!RopeFound||RopeReady||!Near(ropeAnchor.position,2.5f))return false;
            RopeReady=true;ropeLine.SetActive(true);rescueGate.SetActive(false);status="동료를 아래 구조 표식으로 이동시킨 뒤, 위 고리에서 E로 끌어올리세요.";return true;
        }
        public bool Pull()
        {
            if(!Running||Busy||!RopeReady||Rescued||!Near(ropeAnchor.position,2.5f))return false;
            int other=1-Active;
            if(Vector3.Distance(Actors[other].transform.position,rescueBottom.position)>2.5f){status="동료가 아래 밧줄 표식에 있어야 합니다.";return false;}
            if(Braced==other){Actors[other].InputLocked=false;Braced=-1;shoulder.SetActive(false);}
            StartCoroutine(MoveActor(other,rescueTop.position,3.5f,true));status="동료를 끌어올리는 중…";return true;
        }
        IEnumerator MoveActor(int who,Vector3 end,float seconds,bool rescue)
        {
            Busy=true;var p=Actors[who];var cc=p.GetComponent<CharacterController>();p.InputLocked=true;cc.enabled=false;var start=p.transform.position;
            for(float t=0;t<seconds;t+=Time.deltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/seconds);
                // Rope first raises outside the wall, then brings the partner onto the upper dock.
                p.transform.position=rescue?(u<.8f?Vector3.Lerp(start,new Vector3(start.x,end.y,start.z),u/.8f):Vector3.Lerp(new Vector3(start.x,end.y,start.z),end,(u-.8f)/.2f)):Vector3.Lerp(start,end,u);
                yield return null;
            }
            cc.enabled=true;p.Respawn(end,Quaternion.identity);p.InputLocked=false;Busy=false;
            if(rescue){Rescued=true;exitGate.SetActive(false);status="두 플레이어 합류 완료. 열린 출구로 Z4에 진입하세요.";}
        }
        public void Interact()
        {
            if(!Running||Busy)return;
            if(Held){if(Near(assemblyPoint,4.5f))Insert();else{Held.transform.SetPositionAndRotation(Held.home,Held.homeRotation);Held.Collision(true);if(Held.needsTwo)Actors[1-Active].InputLocked=false;Held=null;}return;}
            if(JoinPush())return;
            if(CollectRope()||AttachRope()||Pull()||Mount())return;
            var piece=pieces.Where(p=>!p.placed&&(p.visibleRole<0||p.visibleRole==Active)).OrderBy(p=>Vector3.Distance(p.transform.position,Actors[Active].transform.position)).FirstOrDefault();if(piece)Pick(piece);
        }
        void Update()
        {
            var k=Keyboard.current;if(k==null||PlayerInputReader.PauseOpen)return;
            if(k.f6Key.wasPressedThisFrame)Begin();if(!Running)return;
            if(k.tabKey.wasPressedThisFrame)Switch(1-Active);
            if(k.f8Key.wasPressedThisFrame)ResetPuzzle();
            if(k.fKey.wasPressedThisFrame)Brace();if(k.eKey.wasPressedThisFrame)Interact();
        }
        void LateUpdate()
        {
            if(!Running)return;
            if(Pushing){bool forward=Keyboard.current!=null&&Keyboard.current.wKey.isPressed&&!PlayerInputReader.PauseOpen;PushTick(Time.deltaTime,forward&&pushGrip[0],forward&&pushGrip[1]);}
            for(int i=0;i<2;i++)if(!(Busy&&Actors[i].InputLocked)&&i!=Braced)Actors[i].Simulate(Time.deltaTime);
            if(Held)
            {
                Held.transform.position=Actors[Active].transform.position+Vector3.up*2.6f+Actors[Active].transform.forward*1.3f;
                if(Held.needsTwo){var other=Actors[1-Active];var target=Actors[Active].transform.position+Actors[Active].transform.right*1.8f;var delta=target-other.transform.position;delta.y=0;other.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta,Actors[Active].Tuning.moveSpeed*Time.deltaTime));}
            }
            if(Actors.Any(p=>p.IsDead||p.transform.position.y<0))ResetPuzzle();
        }
        void OnGUI()
        {
            GUI.Box(new Rect(15,Screen.height-105,Mathf.Min(1000,Screen.width-30),90),status+"\n"+(Running?"E 운반/사용/밀기 참여 · W 공동 밀기 · F 받쳐주기 · Tab 역할 전환 · C 엎드리기 · F8 초기화\n계단 "+StepCount+"/"+slots.Length+" | 밧줄 "+(RopeReady?"설치됨":RopeFound?"보유":"미발견"):"Play Mode에서 F6으로 시작 · 오프라인 전용 테스트"));
        }
        void OnDestroy()
        {
            if(Actors!=null)foreach(var p in Actors)if(p)Destroy(p.gameObject);
            if(tunings!=null)foreach(var t in tunings)if(t)Destroy(t);
            if(shoulder)Destroy(shoulder);
            if(Application.isPlaying&&oldPlayers!=null)foreach(var p in oldPlayers)if(p)p.SetActive(true);
        }
    }
}
