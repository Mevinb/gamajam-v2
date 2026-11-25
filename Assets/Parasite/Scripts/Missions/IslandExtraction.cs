using Parasite.Possession;
using Parasite.Characters;
using Parasite.Stealth;
using UnityEngine;
namespace Parasite.Missions
{
    public sealed class IslandExtraction:MonoBehaviour,IInteractable
    {
        public Vector3 InteractPoint=>transform.position+Vector3.up*.35f;
        public float InteractRange=>2.5f;
        public bool IsAvailable=>isActiveAndEnabled&&!RunState.Has(IslandRoster.Departed);
        public string GetPrompt(in InteractContext ctx)=>"Board the rescue launch — everyone assembled";
        public static bool ReadyToDepart(out string reason)
        {
            reason=null;
            if(!GameManager.Instance||!GameManager.Instance.IsPlaying||!RunState.IslandStarted||RunState.Has(IslandRoster.Departed)){reason="The launch is unavailable.";return false;}
            if(!GameObject.Find("Island rescue launch")){reason="The rescue vessel is unavailable.";return false;}
            if(!RunState.Has(IslandRoster.SignalMounted)){reason="Recover and mount the rescue signal first.";return false;}
            if(!IslandRoster.AllBoarded){reason="Every survivor/courier must physically reach its deck slot.";return false;}
            if(!AfterlakeDeparture.AllAmoebaeFree(out reason))return false;
            var travelers=IslandTravelers.Instance;
            if(!travelers||travelers.Members.Count!=IslandRoster.Ids.Length){reason="The passenger roster is incomplete.";return false;}
            foreach(var person in travelers.Members)
                if(!person||!person.Host||person.Host.State!=HostState.Normal||person.Host.Possessor!=null||!IslandRoster.DeckBounds.Contains(person.transform.position))
                {reason="Every passenger must be awake and physically aboard the rescue deck.";return false;}
            var deck=IslandRoster.DeckBounds;
            foreach(var agent in PossessionManager.Instance.Agents)if(!deck.Contains(agent.Position)){reason="Bring your other free amoeba aboard with [Tab] before leaving.";return false;}
            if(SuspicionSystem.Instance&&SuspicionSystem.Instance.Level==SuspicionLevel.Pursuing){reason="Lose the pursuit before exposing the rescue launch.";return false;}
            return true;
        }
        public bool CanInteract(in InteractContext ctx,out string reason)=>ReadyToDepart(out reason);
        public void Interact(in InteractContext ctx){if(!ReadyToDepart(out var why)){GameEvents.RaiseToast(why,Palette.Warning);return;}GameManager.Instance.FinishIsland();}
        void OnEnable()=>Interactables.Register(this);
        void OnDisable()=>Interactables.Unregister(this);
    }
}
