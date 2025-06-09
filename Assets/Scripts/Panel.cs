using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Panel : MonoBehaviour
{
    public TMP_InputField attackField, releaseField, peakPickField, birdSoundField;
    public Bird selectedBird;
    [SerializeField]
    Button saveButton;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setSelectedBird(Bird bird)
    {
        transform.localScale= Vector3.one;
        selectedBird = bird;
        attackField.text = bird.attackThreshold.ToString(CultureInfo.InvariantCulture);
        releaseField.text = bird.releaseThreshold.ToString(CultureInfo.InvariantCulture);
        peakPickField.text = bird.peakPickThreshold.ToString(CultureInfo.InvariantCulture);
        birdSoundField.text = bird.birdSound;
    }

    public void saveParams()
    {
       
        selectedBird.attackThreshold=float.Parse(attackField.text, CultureInfo.InvariantCulture);
        selectedBird.releaseThreshold=float.Parse(releaseField.text, CultureInfo.InvariantCulture);
        selectedBird.peakPickThreshold=float.Parse(peakPickField.text, CultureInfo.InvariantCulture);
        selectedBird.birdSound=birdSoundField.text;
        selectedBird=null;
        transform.localScale= Vector3.zero;
    }

}
