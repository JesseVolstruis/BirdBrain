using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class Bird : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{

 
    MicManager mic;
    BirdManager birdManager;
    public AudioClip clip;
    public AudioClip originalClip;
    private AudioSource audioSource;
    [SerializeField]
    bool isSelected = false;
  
    [SerializeField]
    float hearingRadius = 4f;
    private GameObject radiusCircle;
    public Sprite closedBird;
    public Sprite openBird;
    public Animator notesAnimator;
    public float attackThreshold=0.01f, releaseThreshold=0.005f, peakPickThreshold=0.008f;
    public string input, birdSound;

    private void Awake()
    {
        radiusCircle = transform.GetChild(0).gameObject; //Visual indicator for hearing radius
        radiusCircle.SetActive(false);
        mic = FindAnyObjectByType<MicManager>();
        birdManager = FindAnyObjectByType<BirdManager>();
    
        audioSource = GetComponent<AudioSource>();
        notesAnimator.gameObject.SetActive(false);
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    private void Start()
    {
        birdManager.allBirds.Add(this);
    }

    private void OnDestroy()
    {
        birdManager.allBirds.Remove(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        UnityEngine.Debug.Log("Begin Drag");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        UnityEngine.Debug.Log("End drag");
    }

    //Drag and Drop code for bird objects
    public void OnDrag(PointerEventData eventData)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f; 
        transform.position = worldPos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetSelected(true);

        //Deselects all birds other than this one
        foreach (Bird bird in birdManager.allBirds)
        {
            if (bird != this)
            {
                bird.SetSelected(false);
            }
        }

        //Saves recorded clip as a local clip for potential manipulation
        if (mic.audioClip != null)
        {
            clip = mic.audioClip;
            originalClip = mic.audioClip;
        }

    }

    void SetSelected(bool value)
    {
        isSelected = value;
        if (radiusCircle != null)
        {
            radiusCircle.SetActive(value);
        }
    }

    public Bird GetSelectedBird()
    {
        foreach (Bird bird in birdManager.allBirds)
        {
            if (bird.isSelected)
                return bird;
        }
        return null;
    }

    //Coroutine for playing the bird sounds
    IEnumerator PlayBirdSound(AudioClip clipToPlay)
    {
       yield return new WaitForSeconds(mic.actualDuration + 1f);
        
        GetComponent<SpriteRenderer>().sprite = openBird;

        notesAnimator.gameObject.SetActive(true);
        notesAnimator = transform.Find("MusicNotes").GetComponent<Animator>();
        notesAnimator.Play("MusicNotesFloat");

        audioSource.clip = clipToPlay;
        audioSource.Play();

        yield return new WaitForSeconds(mic.actualDuration);

        GetComponent<SpriteRenderer>().sprite = closedBird;
        notesAnimator.gameObject.SetActive(false);
        notesAnimator.StopPlayback();
        birdManager.SetCurrentClip(gameObject.name);
        birdManager.sendToNextBird();
        // Broadcasts clip to all other birds in its radius
       
    }


    //function for the coroutine
    public void PlayBird()
    {
        if (clip != null)
            StartCoroutine(PlayBirdSound(clip));
    }

    public void ReceiveClip(string incomingClipName)
    {
        input = incomingClipName;

        StartCoroutine(ProcessAudioAndPlayCoroutine(input));
    }

    private IEnumerator ProcessAudioAndPlayCoroutine(string inputRawName)
    {
        // --- 1. Establish a robust path to the project's root directory ---
        // This works correctly in both the Unity Editor and in a built game.
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string exePath = Path.Combine(projectRoot, "birdSoundAlgo.exe");

        // Check if the executable exists before trying to run it
        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError($"Executable not found at {exePath}. Please ensure birdSoundAlgo.exe is in the project's root folder.");
            yield break;
        }

        // --- 2. Construct arguments for the C++ program ---
        // The C++ program will look for these files inside its "recordings" subfolder.
        string arguments = $"{inputRawName}.raw {gameObject.name}.raw {birdSound}.raw {attackThreshold.ToString(CultureInfo.InvariantCulture)} {releaseThreshold.ToString(CultureInfo.InvariantCulture)} {peakPickThreshold.ToString(CultureInfo.InvariantCulture)}";

        // --- 3. Configure the Process to use the command shell (cmd.exe) for redirection ---
        Process process = new Process();
        process.StartInfo.WorkingDirectory = projectRoot; // CRITICAL: Tell the process where to run from.
        process.StartInfo.FileName = "cmd.exe";           // We run the command shell itself.

        // The /C flag tells cmd.exe to run our command and then terminate.
        // We tell it to run the .exe with its arguments and redirect all output to log.txt.
        process.StartInfo.Arguments = $"/C \"{exePath}\" {arguments} > log.txt 2>&1";

        process.StartInfo.CreateNoWindow = true; // Hides the black console window.
        process.StartInfo.UseShellExecute = false; // Required to hide the window and redirect output.

        UnityEngine.Debug.Log($"Starting process from working directory: {projectRoot}");
        UnityEngine.Debug.Log($"Running command: {process.StartInfo.Arguments}");

        try
        {
            process.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start process for {gameObject.name}: {e.Message}");
            yield break;
        }

        // --- 4. Wait for the process to finish (without freezing Unity) ---
        while (!process.HasExited)
        {
            yield return null; // Wait for the next frame.
        }
        UnityEngine.Debug.Log($"Process for {gameObject.name} finished with exit code: {process.ExitCode}");
        process.Close();

        // Check the log file for any errors from the C++ application
        string logPath = Path.Combine(projectRoot, "log.txt");
        if (File.Exists(logPath))
        {
            UnityEngine.Debug.Log("Log file contents:\n" + File.ReadAllText(logPath));
        }

        // --- 5. Load the generated WAV file ---
        string outputWavPath = Path.Combine(projectRoot, "recordings", gameObject.name + ".wav");

        if (!File.Exists(outputWavPath))
        {
            UnityEngine.Debug.LogError($"Output file not found at {outputWavPath}. The external process might have failed. Check log.txt in your project's root folder for details.");
            yield break;
        }

        // Use a UnityWebRequest to load the local audio file
        string url = "file:///" + outputWavPath.Replace("\\", "/"); // Ensure forward slashes for URL
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Error loading audio file from {url}: {www.error}");
            }
            else
            {
                clip = DownloadHandlerAudioClip.GetContent(www);
                UnityEngine.Debug.Log($"Successfully loaded new clip for {gameObject.name}");
                StartCoroutine(PlayBirdSound(clip));
            }
        }
    }


}
