using System.Collections;
using System;
using System.IO;
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
    public int inputNumber = 0;
    string fileName;
    [SerializeField]
    BirdManager birdManager;
    public static event Action OnStopNotRecording;
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
        birdManager.SetCurrentClip(fileName);
        StartCoroutine(playAndSend());
    }

    IEnumerator playAndSend()
    {
        yield return new WaitForSeconds(actualDuration);
        birdManager.sendToNextBird();
    }
    //Stops recording
    public void StopRecording()
    {
        if (Microphone.IsRecording(null))
        {
            int samplesRecorded = Microphone.GetPosition(null);

            Microphone.End(null);

            actualDuration = samplesRecorded / (float)sampleRate;

            Debug.Log("Actual Duration: " + actualDuration);
            fileName = "input" + inputNumber;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "Recordings");
            Directory.CreateDirectory(folder);
            SaveRawFile(Path.Combine(folder, fileName + ".raw"), audioClip);
            inputNumber++;
        }
        else
        {
            OnStopNotRecording?.Invoke();
        }
        
    }
    void SaveRawFile(string filePath, AudioClip clip)
    {
        int sampleCount = clip.samples * clip.channels;
        float[] samples = new float[sampleCount];
        clip.GetData(samples, 0);

        using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                writer.Write(s);
            }
        }

        Debug.Log($"Saved RAW audio to: {filePath}");
    }
}
