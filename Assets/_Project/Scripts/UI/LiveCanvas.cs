using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LiveCanvas : MonoBehaviour
{
    // Start is called before the first frame update
    public TMP_Text HPText;
    private void Start()
    {
        // The CanvasCont prefab has no HPText child (only some scenes add one), so it's optional.
        if (HPText == null)
        {
            Transform found = transform.Find("HPText");
            if (found != null) HPText = found.GetComponent<TMP_Text>();
        }
    }
    public void ChangeHP(float hp)
    {
        if (HPText != null)
            HPText.text= hp.ToString(); //Scan object insted of full scene for object, do this when possible!!!!!!
    }

}
