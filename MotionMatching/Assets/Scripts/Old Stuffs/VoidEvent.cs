using UnityEngine;
namespace MiniGame2.Events
{
    [CreateAssetMenu(fileName = "New VoidType Event", menuName = "ScriptableObject/Events/Void Event")]
    public class VoidEvent : BaseGameEvent<VoidType>
    {
        public void Raise() => Raise(new VoidType());

    }

}