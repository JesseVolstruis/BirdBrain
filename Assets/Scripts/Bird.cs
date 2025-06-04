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

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    private void Awake()
    {
        
        mic = FindAnyObjectByType<MicManager>();
        allBirds.Add(this);
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

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0f; 
        transform.position = worldPos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetSelected(true);


        foreach (Bird bird in allBirds)
        {
            if (bird != this)
            {
                bird.SetSelected(false);
            }
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

    IEnumerator PlayBirdSound(AudioClip clipToPlay)
    {
        yield return new WaitForSeconds(mic.actualDuration + 1f);

        audioSource.clip = clipToPlay;
        audioSource.Play();

        foreach (Bird bird in allBirds)
        {
            if (bird != this && IsInHearingRange(bird))
            {
                bird.ReceiveClip(clipToPlay);
            }
        }
    }

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
