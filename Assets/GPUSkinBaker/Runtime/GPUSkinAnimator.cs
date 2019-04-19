using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUSkinAnimator : MonoBehaviour
{
    public static readonly int shaderPropID_ClipTime = Shader.PropertyToID("_ClipTime");

    public static readonly int shaderPropID_ClipStart = Shader.PropertyToID("_ClipStart");

    public static readonly int shaderPorpID_ClipEnd = Shader.PropertyToID("_ClipEnd");

    public GPUSkinAnimData animData;
    public int defaultClipIndex = 0;
    private float speed = 1f;
    private MaterialPropertyBlock mpb;
    private MeshRenderer mRenderer;

    private GPUSkinClipData _playingClip;
    private GPUSkinClipData lastPlayedClip;
    private bool isPlaying = false;
    private float time;
    private Action onPlayFinish;

    // Use this for initialization
    void Start()
    {
        mRenderer = this.GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
        Play(defaultClipIndex);
    }

    void Update()
    {
        Update_Internal(Time.deltaTime);
    }

    public void UpdateSpeed(float speed)
    {
        this.speed = speed;
    }

    public void Play(string clipName, Action onFinish = null)
    {
        GPUSkinClipData targetClip = animData.GetClip(clipName);
        SetNewPlayingClip(targetClip, onFinish);
    }

    public void Play(int index, Action onFinish = null)
    {
        GPUSkinClipData targetClip = animData.GetClip(index);
        SetNewPlayingClip(targetClip, onFinish);
    }

    private void SetNewPlayingClip(GPUSkinClipData clip, Action onFinish = null)
    {
        lastPlayedClip = _playingClip;

        isPlaying = true;
        _playingClip = clip;
        time = 0;
        if (clip.wrapMode == WrapMode.Default)
        {
            onPlayFinish = onFinish;
        }
        else
        {
            onPlayFinish = null;
        }
    }

    private void Update_Internal(float dt)
    {
        if (!isPlaying || _playingClip == null)
        {
            return;
        }

        dt = dt * speed;
        if (_playingClip.wrapMode == WrapMode.Loop)
        {
            time += dt;
            if (time > _playingClip.length)
            {
                time = 0;
            }
            UpdateMaterial(dt);
        }
        else if (_playingClip.wrapMode == WrapMode.Default)
        {
            if (time > _playingClip.length)
            {
                time = _playingClip.length;
            }
            else
            {
                time += dt;
                if (time >= _playingClip.length)
                {
                    OnPlayClipFinish();
                }
                UpdateMaterial(dt);
            }
        }
    }

    private int GetTheLastFrameIndex_WrapMode_Once(GPUSkinClipData clip)
    {
        return (int)(clip.length * clip.fps) - 1;
    }
    private int GetFrameIndex_WrapMode_Loop(GPUSkinClipData clip, float time)
    {
        return (int)(time * clip.fps) % (int)(clip.length * clip.fps);
    }

    private int GetFrameIndex()
    {
        float time = this.time;
        if (Mathf.Abs(_playingClip.length - time) < 0.01f)
        {
            return GetTheLastFrameIndex_WrapMode_Once(_playingClip);
        }
        else
        {
            return GetFrameIndex_WrapMode_Loop(_playingClip, time);
        }
    }

    private void UpdateMaterial(float deltaTime)
    {
        //int frameIndex = GetFrameIndex();

        float normailizedTime = Mathf.Clamp01(this.time / _playingClip.length);
        mpb.SetFloat(shaderPropID_ClipTime, normailizedTime);
        mpb.SetFloat(shaderPropID_ClipStart, _playingClip.startV);
        mpb.SetFloat(shaderPorpID_ClipEnd, _playingClip.endV);
        mRenderer.SetPropertyBlock(mpb);
    }


    private void OnPlayClipFinish()
    {
        isPlaying = false;
        if (onPlayFinish != null)
        {
            Action onFinish = this.onPlayFinish;
            this.onPlayFinish = null;
            onFinish();
        }
    }
}
