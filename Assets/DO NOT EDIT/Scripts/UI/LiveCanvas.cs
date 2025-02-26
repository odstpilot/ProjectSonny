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
        HPText= transform.Find("HPText").GetComponent<TMP_Text>();
    }
    public void ChangeHP(float hp)
    {
        HPText.text= hp.ToString(); //Scan object insted of full scene for object, do this when possible!!!!!!
    }

}
