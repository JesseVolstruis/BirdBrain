using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bird : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{

 
    MicManager mic;
    public AudioClip clip;
    public AudioClip originalClip;
    private AudioSource audioSource;
    [SerializeField]
    bool isSelected = false;
    [SerializeField]
    private static List<Bird> allBirds = new List<Bird>();
    [SerializeField]
    float hearingRadius = 4f;
    private GameObject radiusCircle;
    public Sprite closedBird;
    public Sprite openBird;

    private void Awake()
    {
        radiusCircle = transform.GetChild(0).gameObject; //Visual indicator for hearing radius
        radiusCircle.SetActive(false);
        mic = FindAnyObjectByType<MicManager>();
        allBirds.Add(this); //Adds this bird object to static list of all birds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnDestroy()
    {
        allBirds.Remove(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("Begin Drag");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("End drag");
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
        foreach (Bird bird in allBirds)
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

    public static Bird GetSelectedBird()
    {
        foreach (Bird bird in allBirds)
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

        audioSource.clip = clipToPlay;
        audioSource.Play();

        yield return new WaitForSeconds(clipToPlay.length);

        GetComponent<SpriteRenderer>().sprite = closedBird;

        // Broadcasts clip to all other birds in its radius
        foreach (Bird bird in allBirds)
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


}
