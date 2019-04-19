using System.Collections.Generic;
using UnityEngine;

public class GPUSkinAnimData : ScriptableObject
{
    public GPUSkinClipData[] clips = null;
    public Material material;
    public Mesh mesh;

    private Dictionary<string, GPUSkinClipData> clipMap;

    public Dictionary<string, GPUSkinClipData> ClipMap
    {
        get
        {
            if (clipMap == null)
            {
                clipMap = new Dictionary<string, GPUSkinClipData>(clips.Length);
                foreach (var clip in clips)
                {
                    clipMap.Add(clip.name, clip);
                }
            }
            return clipMap;
        }
    }

    public GPUSkinClipData GetClip(int index)
    {
        GPUSkinClipData ret = null;
        if (index >= 0 && index < this.clips.Length)
        {
            ret = this.clips[index];
        }
        return ret;
    }

    public GPUSkinClipData GetClip(string clipName)
    {
        GPUSkinClipData ret = null;
        ClipMap.TryGetValue(clipName, out ret);
        return ret;
    }

    public int FindClip(string clipName)
    {
        int targetIndex = -1;
        for (var i = 0; i < this.clips.Length; i++)
        {
            if (this.clips[i].name == clipName)
            {
                targetIndex = i;
                break;
            }
        }

        return targetIndex;
    }
}

[System.Serializable]
public class GPUSkinClipData
{
    public string name = null;

    public float length = 0.0f;
    public int frames = 0;
    public float fps = 0;
    public WrapMode wrapMode = WrapMode.Default;

    public float startV;
    public float endV;

    //public GPUSkinningFrame[] frames = null;

    //public int pixelSegmentation = 0;

    //public bool rootMotionEnabled = false;

    //public bool individualDifferenceEnabled = false;
}