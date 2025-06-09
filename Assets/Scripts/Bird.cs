using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class Bird : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    // --- Public & Serialized Fields ---
    [Header("Bird Properties")]
    public AudioClip clip;
    public AudioClip originalClip;
    public Sprite closedBird;
    public Sprite openBird;
    public Animator notesAnimator;

    [Header("Audio Analysis Parameters")]
    public float attackThreshold = 0.01f;
    public float releaseThreshold = 0.005f;
    public float peakPickThreshold = 0.008f;
    public string input, birdSound;

    // --- Private Fields ---
    private MicManager mic;
    private BirdManager birdManager;
    private AudioSource audioSource;
    private bool isSelected = false;
    private Coroutine pbsCR; // PlayBirdSound Coroutine Reference

    private void Awake()
    {
        MicManager.OnStopNotRecording += stopPlayback;
        transform.GetChild(0).gameObject.SetActive(false); // Disable radius circle initially
        mic = FindAnyObjectByType<MicManager>();
        birdManager = FindAnyObjectByType<BirdManager>();
        audioSource = GetComponent<AudioSource>();
        if (notesAnimator != null) notesAnimator.gameObject.SetActive(false);
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        birdManager.allBirds.Add(this);
        audioSource.panStereo = transform.position.x / 9f;
        audioSource.volume = (transform.position.y + 3f) / 6f;
    }

    private void OnDestroy()
    {
        MicManager.OnStopNotRecording -= stopPlayback;
        birdManager.allBirds.Remove(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;
        transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        audioSource.panStereo = transform.position.x / 9f;
        audioSource.volume = (transform.position.y + 3f) / 6f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetSelected(true);
        foreach (Bird bird in birdManager.allBirds)
        {
            if (bird != this) bird.SetSelected(false);
        }
        if (mic.audioClip != null)
        {
            clip = mic.audioClip;
            originalClip = mic.audioClip;
        }
    }

    // Unused but required by interface
    public void OnBeginDrag(PointerEventData eventData) { }



    public void SetSelected(bool value)
    {
        isSelected = value;
        transform.GetChild(0).gameObject.SetActive(value);
        if (value)
            FindAnyObjectByType<Panel>().setSelectedBird(this);
    }

    public void PlayBird()
    {
        if (clip != null)
            pbsCR = StartCoroutine(PlayBirdSound(clip));
    }

    public void ReceiveClip(string incomingClipName)
    {
        input = incomingClipName;
        StartCoroutine(ProcessAudioAndPlayCoroutine(input));
    }
    
    private void stopPlayback()
    {
        if (pbsCR != null)
        {
            StopCoroutine(pbsCR);
            GetComponent<SpriteRenderer>().sprite = closedBird;
            if (notesAnimator != null)
            {
                notesAnimator.gameObject.SetActive(false);
                notesAnimator.StopPlayback();
            }
            pbsCR = null;
        }
    }

    private IEnumerator ProcessAudioAndPlayCoroutine(string inputRawName)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string exePath = Path.Combine(projectRoot, "birdSoundAlgo.exe");

        if (!File.Exists(exePath))
        {
            yield break;
        }

        string recordingsFolder = Path.Combine(projectRoot, "Recordings");
        if (!Directory.Exists(recordingsFolder)) Directory.CreateDirectory(recordingsFolder);

        string inputPath = Path.Combine(recordingsFolder, $"{inputRawName}.raw");
        string outputPath = Path.Combine(recordingsFolder, $"{gameObject.name}.raw");
        string birdSoundPath = Path.Combine(recordingsFolder, $"{birdSound}.raw");

        // 2. Construct arguments with full paths
        string arguments = $"\"{inputRawName}.raw\" \"{gameObject.name}.raw\" \"{birdSound}.raw\" " +
                           $"{attackThreshold.ToString(CultureInfo.InvariantCulture)} " +
                           $"{releaseThreshold.ToString(CultureInfo.InvariantCulture)} " +
                           $"{peakPickThreshold.ToString(CultureInfo.InvariantCulture)}";

        // 3. Configure and run the external process
        Process process = new Process();
        process.StartInfo.WorkingDirectory = projectRoot;
        process.StartInfo.FileName = exePath;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;

        UnityEngine.Debug.Log($"Running command: {exePath} {arguments}");

        try
        {
            process.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start process: {e.Message}");
            yield break;
        }

        // 4. Wait for the process to finish without blocking the main thread
        while (!process.HasExited)
        {
            yield return null;
        }
        int exitCode = process.ExitCode;
        process.Close();

        UnityEngine.Debug.Log($"Process finished with exit code: {exitCode}");

        if (exitCode != 0)
        {
            UnityEngine.Debug.LogError($"External process failed with exit code {exitCode}.");
            yield break;
        }

        // 5. Load the generated WAV file
        string outputWavPath = Path.Combine(recordingsFolder, gameObject.name + ".wav");

        // This helper coroutine waits until the file is no longer locked by the OS
        yield return StartCoroutine(WaitUntilFileIsReady(outputWavPath));

        var url = new System.Uri(outputWavPath).AbsoluteUri;
        UnityEngine.Debug.Log($"File is ready. Attempting to load from: {url}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Error loading audio file: {www.error}");
            }
            else
            {
                AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(www);
                if (loadedClip == null || loadedClip.loadState != AudioDataLoadState.Loaded)
                {
                    UnityEngine.Debug.LogError("Failed to get a valid AudioClip from the downloaded data.");
                    yield break;
                }
                UnityEngine.Debug.Log($"Successfully loaded new clip for {gameObject.name}");
                this.clip = loadedClip; 
                pbsCR = StartCoroutine(PlayBirdSound(loadedClip));
            }
        }
    }

    private IEnumerator WaitUntilFileIsReady(string filePath)
    {
        int maxRetries = 20; 
        for (int i = 0; i < maxRetries; i++)
        {
            if (!File.Exists(filePath))
            {
                yield return new WaitForSeconds(0.01f);
                continue;
            }
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    UnityEngine.Debug.Log($"File found and accessible at: {filePath}");
                    yield break;
                }
            }
            catch (IOException)
            {
                UnityEngine.Debug.LogWarning($"File is locked, retrying... ({i + 1}/{maxRetries})");
            
            }
        }
        UnityEngine.Debug.LogError($"File at {filePath} could not be accessed after {maxRetries} retries.");
    }

    private IEnumerator PlayBirdSound(AudioClip clipToPlay)
    {
        if (audioSource == null || clipToPlay == null)
        {
            UnityEngine.Debug.LogError("Cannot play sound, AudioSource or AudioClip is null.");
            yield break;
        }

        



        GetComponent<SpriteRenderer>().sprite = openBird;
        
            notesAnimator.gameObject.SetActive(true);
            notesAnimator.Play("MusicNotesFloat", -1, 0f);

        audioSource.PlayOneShot(clipToPlay);

        yield return new WaitForSeconds(mic.actualDuration);

        GetComponent<SpriteRenderer>().sprite = closedBird;
        if (notesAnimator != null)
        {
            notesAnimator.gameObject.SetActive(false);
        }

        birdManager.SetCurrentClip(gameObject.name);
        birdManager.sendToNextBird();
        pbsCR = null;
    }

    
}
