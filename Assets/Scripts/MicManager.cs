using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MicManager : MonoBehaviour
{
    
     public AudioSource audioSource;
     int recordDuration = 10;
     int sampleRate = 16000; 
     string microphoneName;
    public AudioClip audioClip;
    public float actualDuration;


    // Start is called before the first frame update
    void Start()
    {
      
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartRecording()
    {
        string device = Microphone.devices[0];
        audioClip = Microphone.Start(device, true, recordDuration, sampleRate);
    }

    public void PlayRecording()
    {
        audioSource.clip = audioClip;
        audioSource.Play();
    }

    public void StopRecording()
    {
        int samplesRecorded = Microphone.GetPosition(null);

        Microphone.End(null);

        actualDuration = samplesRecorded / (float)sampleRate;

        Debug.Log("Actual Duration: " + actualDuration);
    }
}
