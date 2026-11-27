using UnityEngine;
namespace HiddenPath.Stage2Z3
{
    public class Z3Piece:MonoBehaviour
    {
        public int fittingStep=-1;
        public bool needsTwo;
        public int visibleRole=-1; // -1 common, 0 red, 1 blue; collision remains shared.
        [HideInInspector] public Vector3 home;
        [HideInInspector] public Quaternion homeRotation;
        [HideInInspector] public bool placed;
        void Awake(){home=transform.position;homeRotation=transform.rotation;}
        public void Collision(bool value){foreach(var c in GetComponentsInChildren<Collider>())c.enabled=value;}
        Renderer[] visuals;bool[] originallyEnabled;
        public void ViewAs(int role)
        {
            if(visuals==null){visuals=GetComponentsInChildren<Renderer>();originallyEnabled=new bool[visuals.Length];for(int i=0;i<visuals.Length;i++)originallyEnabled[i]=visuals[i].enabled;}
            for(int i=0;i<visuals.Length;i++)visuals[i].enabled=originallyEnabled[i]&&(visibleRole<0||visibleRole==role);
        }
    }
}
