using UnityEngine;
namespace HiddenPath.Stage2Z2
{
    // Neutral boarding lift: whichever role's first special platform is skipped by
    // the opening exchange can still reach the first shuttle without a softlock.
    [RequireComponent(typeof(Z2Platform))]
    public class Z2EntryLift:MonoBehaviour
    {
        void Start(){GetComponent<Z2Platform>().SetState(true,0,false);}
    }
}
