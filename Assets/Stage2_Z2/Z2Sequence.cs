using System;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Stage2Z2
{
    public interface IZ2Transport
    {
        void RequestStep(PlayerRole role,int index);
        void RequestReset();
    }
    public class Z2Sequence : MonoBehaviour
    {
        public Z2Platform[] redPlatforms,bluePlatforms;
        public Transform redStart,blueStart;
        public int RedIndex {get;private set;}
        public int BlueIndex {get;private set;}
        public int RedUsed {get;private set;}
        public int BlueUsed {get;private set;}
        public double RedTime {get;private set;}
        public double BlueTime {get;private set;}
        public int Revision {get;private set;}
        public event Action Changed;
        public IZ2Transport Transport {get;set;}
        public static double Now => GameServices.Clock!=null?GameServices.Clock.Now:Time.timeAsDouble;
        public int Count => Mathf.Min(redPlatforms.Length,bluePlatforms.Length);
        void Start(){if(!GameServices.IsOnline)ResetLocal();else Refresh();}
        public bool TryStep(PlayerRole role,int index)
        {
            if(GameServices.IsOnline){Transport?.RequestStep(role,index);return false;}
            return ApplyStep(role,index,Now);
        }
        public bool ApplyStep(PlayerRole role,int index,double now)
        {
            if(index<0||index>=Count||index>30)return false;
            bool red=role==PlayerRole.A_Red;
            if(!red&&role!=PlayerRole.B_Blue)return false;
            if(index!=(red?RedIndex:BlueIndex)||(((red?RedUsed:BlueUsed)&(1<<index))!=0))return false;
            if(red){RedUsed|=1<<index;if(BlueIndex<Count-1){BlueIndex++;BlueTime=now;}}
            else{BlueUsed|=1<<index;if(RedIndex<Count-1){RedIndex++;RedTime=now;}}
            Revision++;Refresh();return true;
        }
        public void RequestReset(){if(GameServices.IsOnline)Transport?.RequestReset();else ResetLocal();}
        public void ResetLocal(){SetSnapshot(0,0,0,0,Now,Now,Revision+1);}
        public void SetSnapshot(int r,int b,int rm,int bm,double rt,double bt,int revision)
        {RedIndex=r;BlueIndex=b;RedUsed=rm;BlueUsed=bm;RedTime=rt;BlueTime=bt;Revision=revision;Refresh();}
        public void Refresh()
        {
            for(int i=0;i<redPlatforms.Length;i++)if(redPlatforms[i])redPlatforms[i].SetState(i==RedIndex,RedTime,(RedUsed&(1<<i))!=0);
            for(int i=0;i<bluePlatforms.Length;i++)if(bluePlatforms[i])bluePlatforms[i].SetState(i==BlueIndex,BlueTime,(BlueUsed&(1<<i))!=0);
            Changed?.Invoke();
        }
    }
}
