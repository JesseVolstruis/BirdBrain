using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MicManager : MonoBehaviour
{
    
     public AudioSource audioSource;
     int recordDuration = 10; //Max duration for clip
     int sampleRate = 16000; 
     string microphoneName;
    public AudioClip audioClip;
    public float actualDuration; //Length in seconds for the actual recorderd clip

    void Start()
    {
      
    }
    void Update()
    {
        
    }
    //Records clip with microphone
    public void StartRecording()
    {
        string device = Microphone.devices[0];
        audioClip = Microphone.Start(device, true, recordDuration, sampleRate);
        Debug.Log(device);
    }
    //Plays recording
    public void PlayRecording()
    {
        audioSource.clip = audioClip;
        audioSource.Play();
        //Finds currently selected bird and calls its PlayBird function
        Bird selectedBird = Bird.GetSelectedBird();
        if (selectedBird != null)
        {
            selectedBird.PlayBird();
        }
    }
    //Stops recording
    public void StopRecording()
    {
        int samplesRecorded = Microphone.GetPosition(null);

        Microphone.End(null);

        actualDuration = samplesRecorded / (float)sampleRate;

        Debug.Log("Actual Duration: " + actualDuration);
    }
}
