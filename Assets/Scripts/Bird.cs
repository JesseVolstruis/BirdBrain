using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bird : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{

 
    MicManager mic;
    public AudioClip clip;
    [SerializeField]
    bool isSelected = false;
    [SerializeField]
    private static List<Bird> allBirds = new List<Bird>();

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

        clip = mic.audioClip;
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

    IEnumerator PlayBirdSound()
    {
        yield return new WaitForSeconds(mic.actualDuration +1f);
        mic.audioSource.Play();
    }

    public void PlayBird()
    {
        StartCoroutine(PlayBirdSound());
    }

    
}
