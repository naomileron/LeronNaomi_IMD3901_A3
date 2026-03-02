using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{

    public Image crosshairImage;
    public Color normalColor = Color.white;
    public Color interactColor = Color.pink;

    public void SetInteract(bool canInteract)
    {
        crosshairImage.color = canInteract ? interactColor : normalColor;
    }

}
