using UnityEngine;

namespace IronCitadel
{
    /// <summary>The door the player came in by. It stays shut behind them; E on it says so.</summary>
    public class OuterDoor : MonoBehaviour
    {
        void Interact()
        {
            if (CitadelHUD.Instance != null)
                CitadelHUD.Instance.Announce("You came in this way.  The job is inside: the warlord's throne, to the north.", 4f);
        }
    }
}
