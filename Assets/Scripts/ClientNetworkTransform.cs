using UnityEngine;
using Unity.Netcode.Components;

//class code
public class ClientNetworkTransform : NetworkTransform
{

    protected override bool OnIsServerAuthoritative()
    {

        return false;

    }

}
