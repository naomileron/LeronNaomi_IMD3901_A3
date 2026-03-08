using UnityEngine;

//For describing which hospital a spawn point belongs to
public enum HospitalType : byte
{ 
    Blue = 0,
    Green = 1
}

[ExecuteAlways]
public class RoomSpawnPoint : MonoBehaviour
{

    //Which hospital the room belongs to
    [SerializeField] private HospitalType hospital;

    //Assign room numbers automatically based on hierarchy order.
    [SerializeField] private int roomNumber = 1;

    //Variables for other scripts to read and reference accordingly
    public HospitalType Hospital => hospital;
    public int RoomNumber => roomNumber;

    //Keeps room numbers automatically updated
    private void OnValidate()
    {
        AutoAssign();
    }

    //For when the component is first added to a GameObject
    private void Reset()
    {
        AutoAssign();
    }

    //Automaitcally assigns a room number and hospital type
    private void AutoAssign()
    {
        //+ 1 so that there isn't a "room 0"
        roomNumber = transform.GetSiblingIndex() + 1;

        Transform parentTransform = transform.parent;

        if (parentTransform != null)
        {
            //avoid case sensitivity
            string parentName = parentTransform.name.ToLower();

            if (parentName.Contains("one"))
                hospital = HospitalType.Blue;
            else if (parentName.Contains("two"))
                hospital = HospitalType.Green;
        }
    }
}