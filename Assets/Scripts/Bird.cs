using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks; // Required for Task-based async
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class Bird : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    // --- Your existing variables ---
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
    public float attackThreshold = 0.01f, releaseThreshold = 0.005f, peakPickThreshold = 0.008f;
    public string input, birdSound;

    // --- Your existing Awake, Start, OnDestroy, and Drag/Drop functions ---
    private void Awake()
    {
        MicManager.OnStopNotRecording += stopPlayback;
        radiusCircle = transform.GetChild(0).gameObject;
        radiusCircle.SetActive(false);
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
        audioSource.panStereo = transform.position.x / 9;
        audioSource.volume = (transform.position.y + 3) / 6;
    }

    private void OnDestroy()
    {
        MicManager.OnStopNotRecording -= stopPlayback;
        birdManager.allBirds.Remove(this);
        CleanupTempFiles(); // Clean up any temporary files we created
    }

    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData)
    {
        audioSource.panStereo = transform.position.x / 9;
        audioSource.volume = (transform.position.y + 3) / 6;
    }
    public void OnDrag(PointerEventData eventData)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f;
        transform.position = worldPos;
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

    void SetSelected(bool value)
    {
        isSelected = value;
        if (radiusCircle != null) radiusCircle.SetActive(value);
        if (value) FindAnyObjectByType<Panel>().setSelectedBird(this);
    }

    public Bird GetSelectedBird()
    {
        foreach (Bird bird in birdManager.allBirds)
        {
            if (bird.isSelected) return bird;
        }
        return null;
    }

    Coroutine pbsCR;
    void stopPlayback()
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
    public void PlayBird()
    {
        if (clip != null)
            pbsCR = StartCoroutine(PlayBirdSound(clip));
    }

    // --- This function now just kicks off the async task ---
    public void ReceiveClip(string incomingClipName)
    {
        input = incomingClipName;
        ProcessAudioAndPlayAsync(input); // Call the new async method
    }

    // --- FULLY REVISED ASYNC METHOD for processing audio ---
    private async void ProcessAudioAndPlayAsync(string inputRawName)
    {
        // 1. Establish paths
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string exePath = Path.Combine(projectRoot, "birdSoundAlgo.exe");

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError($"Executable not found at {exePath}.");
            return;
        }

        // 2. Construct arguments
        string arguments = $"{inputRawName}.raw {gameObject.name}.raw {birdSound}.raw " +
                           $"{attackThreshold.ToString(CultureInfo.InvariantCulture)} " +
                           $"{releaseThreshold.ToString(CultureInfo.InvariantCulture)} " +
                           $"{peakPickThreshold.ToString(CultureInfo.InvariantCulture)}";

        // 3. Configure the process
        Process process = new Process();
        process.StartInfo.WorkingDirectory = projectRoot;
        process.StartInfo.FileName = exePath;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        StringBuilder outputLog = new StringBuilder();
        StringBuilder errorLog = new StringBuilder();

        process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputLog.AppendLine(args.Data); };
        process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorLog.AppendLine(args.Data); };

        // 4. Run the process on a background thread using Task.Run
        int exitCode = -1;
        UnityEngine.Debug.Log($"Running command on background thread: {exePath} {arguments}");

        await Task.Run(() =>
        {
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(); // This blocking call now happens safely on a background thread.
                exitCode = process.ExitCode;
            }
            catch (System.Exception e)
            {
                errorLog.AppendLine($"\nCRITICAL ERROR: Failed to start or run process: {e.Message}");
                exitCode = -99; // Use a custom error code
            }
            finally
            {
                if (process != null) process.Close();
            }
        });

        // --- The code below this point runs back on the main Unity thread ---
        UnityEngine.Debug.Log($"--- Log for {gameObject.name} (Exit Code: {exitCode}) ---\n" +
                              $"Output:\n{outputLog}\n" +
                              $"Errors:\n{errorLog}\n" +
                              $"--------------------------------------------------");

        if (exitCode != 0)
        {
            UnityEngine.Debug.LogError($"Process exited with error code {exitCode}. Aborting audio load.");
            return;
        }

        // 5. Load the generated WAV file using robust loading
        string recordingsFolder = "recordings";
        string outputWavPath = Path.Combine(projectRoot, recordingsFolder, gameObject.name + ".wav");

        if (!File.Exists(outputWavPath))
        {
            UnityEngine.Debug.LogError($"Output file not found at {outputWavPath}. The external process might have failed.");
            return;
        }

        UnityEngine.Debug.Log($"Process completed successfully. Starting robust audio load from: {outputWavPath}");

        // Use the new robust loading method instead of the simple coroutine
        AudioClip loadedClip = await LoadAudioFileRobust(outputWavPath);

        if (loadedClip != null)
        {
            UnityEngine.Debug.Log($"Successfully loaded audio clip via robust method. Starting playback.");
            // Start playback directly here instead of using a coroutine
            pbsCR = StartCoroutine(PlayBirdSound(loadedClip));
        }
        else
        {
            UnityEngine.Debug.LogError($"Failed to load audio file using all robust methods: {outputWavPath}");
        }
    }

    // --- ROBUST AUDIO LOADING METHODS ---

    /// <summary>
    /// Loads an audio file with multiple fallback strategies to handle build-specific issues
    /// </summary>
    private async Task<AudioClip> LoadAudioFileRobust(string filePath, AudioType audioType = AudioType.WAV)
    {
        UnityEngine.Debug.Log($"Starting robust audio load for: {filePath}");

        // Strategy 1: Wait for file system stabilization
        if (!await WaitForFileStabilization(filePath))
        {
            UnityEngine.Debug.LogError($"File never stabilized: {filePath}");
            return null;
        }

        // Strategy 2: Try direct UnityWebRequest first
        AudioClip clip = await LoadWithUnityWebRequest(filePath, audioType);
        if (clip != null)
        {
            UnityEngine.Debug.Log("Successfully loaded with UnityWebRequest");
            return clip;
        }

        // Strategy 3: Copy to persistent data path and load
        clip = await LoadViaCopyToPersistentData(filePath, audioType);
        if (clip != null)
        {
            UnityEngine.Debug.Log("Successfully loaded via PersistentDataPath copy");
            return clip;
        }

        // Strategy 4: Try with explicit file refresh
        clip = await LoadWithFileRefresh(filePath, audioType);
        if (clip != null)
        {
            UnityEngine.Debug.Log("Successfully loaded with file refresh");
            return clip;
        }

        UnityEngine.Debug.LogError($"All loading strategies failed for: {filePath}");
        return null;
    }

    /// <summary>
    /// Waits for the file to be completely written and stable
    /// </summary>
    private async Task<bool> WaitForFileStabilization(string filePath)
    {
        const int maxAttempts = 50; // 5 seconds total
        const int delayMs = 100;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    await Task.Delay(delayMs);
                    continue;
                }

                // Check if file is still being written by trying to open it exclusively
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // If we can open it exclusively, it's not being written to
                    if (fs.Length > 0)
                    {
                        UnityEngine.Debug.Log($"File stabilized after {attempt * delayMs}ms: {filePath} ({fs.Length} bytes)");
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                // File is still being written or locked
                await Task.Delay(delayMs);
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                // Permission issue - try a different approach
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
                {
                    await Task.Delay(delayMs * 2); // Wait longer for permission issues
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Standard UnityWebRequest loading with proper async handling
    /// </summary>
    private async Task<AudioClip> LoadWithUnityWebRequest(string filePath, AudioType audioType)
    {
        try
        {
            string uri = GetFileUri(filePath);
            UnityEngine.Debug.Log($"Loading with UnityWebRequest: {uri}");

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                // Set additional properties that can help with file access
                request.timeout = 10;

                var operation = request.SendWebRequest();

                // Convert Unity operation to async
                while (!operation.isDone)
                {
                    await Task.Delay(50);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip != null && clip.length > 0)
                    {
                        return clip;
                    }
                }

                UnityEngine.Debug.LogWarning($"UnityWebRequest failed: {request.error} | Result: {request.result}");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"UnityWebRequest exception: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// Copy file to PersistentDataPath and load from there
    /// </summary>
    private async Task<AudioClip> LoadViaCopyToPersistentData(string sourceFilePath, AudioType audioType)
    {
        try
        {
            string persistentPath = Path.Combine(Application.persistentDataPath, "temp_audio");
            Directory.CreateDirectory(persistentPath);

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(persistentPath, fileName);

            // Copy the file
            File.Copy(sourceFilePath, destinationPath, true);

            // Wait a moment for the copy to settle
            await Task.Delay(200);

            // Load from the copied location
            return await LoadWithUnityWebRequest(destinationPath, audioType);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"PersistentDataPath copy strategy failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Try loading with explicit file system refresh
    /// </summary>
    private async Task<AudioClip> LoadWithFileRefresh(string filePath, AudioType audioType)
    {
        try
        {
            // Force a file system refresh by accessing file properties
            var fileInfo = new FileInfo(filePath);
            fileInfo.Refresh();

            UnityEngine.Debug.Log($"File refresh - Size: {fileInfo.Length}, Exists: {fileInfo.Exists}");

            // Wait a bit more
            await Task.Delay(300);

            // Try loading again
            return await LoadWithUnityWebRequest(filePath, audioType);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"File refresh strategy failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the proper file URI for the platform
    /// </summary>
    private string GetFileUri(string filePath)
    {
        // Ensure we have the absolute path
        string absolutePath = Path.GetFullPath(filePath);

        // Convert to proper URI format
        if (Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor)
        {
            return "file:///" + absolutePath.Replace('\\', '/');
        }
        else
        {
            return "file://" + absolutePath;
        }
    }

    /// <summary>
    /// Clean up temporary files
    /// </summary>
    private void CleanupTempFiles()
    {
        try
        {
            string persistentTempPath = Path.Combine(Application.persistentDataPath, "temp_audio");
            if (Directory.Exists(persistentTempPath))
            {
                Directory.Delete(persistentTempPath, true);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"Failed to cleanup temp files: {e.Message}");
        }
    }

    // --- REVISED PlayBirdSound Coroutine (unchanged from your original) ---
    IEnumerator PlayBirdSound(AudioClip clipToPlay)
    {
        if (audioSource == null || clipToPlay == null)
        {
            UnityEngine.Debug.LogError("Cannot play sound, AudioSource or AudioClip is null.");
            yield break;
        }

        // This short wait can help prevent sounds from stomping on each other if called in rapid succession.
        yield return new WaitForSeconds(clipToPlay.length);

        GetComponent<SpriteRenderer>().sprite = openBird;
        if (notesAnimator != null)
        {
            notesAnimator.gameObject.SetActive(true);
            notesAnimator.Play("MusicNotesFloat", -1, 0f);
        }

        audioSource.PlayOneShot(clipToPlay);

        // Wait for the duration of the NEW clip.
        yield return new WaitForSeconds(clipToPlay.length);

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