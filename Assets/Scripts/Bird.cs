using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

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

    private void Awake()
    {
        radiusCircle = transform.GetChild(0).gameObject; //Visual indicator for hearing radius
        radiusCircle.SetActive(false);
        mic = FindAnyObjectByType<MicManager>();
        birdManager = FindAnyObjectByType<BirdManager>();
        birdManager.allBirds.Add(this);
        audioSource = GetComponent<AudioSource>();
        notesAnimator.gameObject.SetActive(false);
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
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
       // yield return new WaitForSeconds(mic.actualDuration + 1f);
        
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

        // Broadcasts clip to all other birds in its radius
        foreach (Bird bird in birdManager.allBirds)
        {
            if (bird != this && IsInHearingRange(bird))
            {
                bird.ReceiveClip(clipToPlay);
            }
        }
    }


    //function for the coroutine
    public void PlayBird()
    {
        if (clip != null)
            StartCoroutine(PlayBirdSound(clip));
    }

    public void ReceiveClip(AudioClip incomingClip)
    {
        // Save it as their new clip (they heard it)
        clip = incomingClip;

        // Play it after the delay using coroutine
        StartCoroutine(PlayBirdSound(incomingClip));
    }

    private bool IsInHearingRange(Bird other)
    {
        return Vector3.Distance(transform.position, other.transform.position) <= hearingRadius;
    }

    void RunAlgorithm(string input)
    {
        Process process = new Process();
        string path = Path.Combine(Application.dataPath, "birdSoundAlgo.exe");
        process.StartInfo.FileName = path;
        process.StartInfo.Arguments = "recordings/"+input+".raw"+" "+"recordings/"+gameObject.name+".raw"+" "+"recordings/birdSound.raw"+" "+attackThreshold+" "+releaseThreshold+" "+peakPickThreshold; // Optional: add command line arguments recordings/input.raw recordings/output.raw recordings/bird.raw 0.010 0.005 0.008
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.Start();
    }
}
