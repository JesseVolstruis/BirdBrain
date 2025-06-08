using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdManager : MonoBehaviour
{
    public List<Bird> allBirds = new List<Bird>();
    public int index=0;
    public string currentclip = "";
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetCurrentClip(string path)
    {
        currentclip = path;
    }

    public void sendToNextBird()
    {
        allBirds[index].ReceiveClip(currentclip);
        if (index < allBirds.Count-1) index++;
        else index = 0;

    }
}
