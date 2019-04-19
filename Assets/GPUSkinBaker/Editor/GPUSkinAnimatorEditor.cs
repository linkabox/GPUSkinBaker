using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GPUSkinAnimator))]
public class GPUSkinAnimatorEditor : Editor
{
    private GPUSkinAnimator _player;
    private MeshRenderer _meshRenderer;
    private string[] clipsName = null;

    private SerializedProperty animDataProp;
    private SerializedProperty defaultClipProp;

    public override void OnInspectorGUI()
    {
        //base.DrawDefaultInspector();

        if (_player == null)
        {
            return;
        }

        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(animDataProp);
        if (EditorGUI.EndChangeCheck())
        {
            GPUSkinAnimData animData = animDataProp.objectReferenceValue as GPUSkinAnimData;
            ResetAnimData(_player, animData);
            RebuildClipNames(animData);
        }

        if (clipsName != null)
        {
            EditorGUI.BeginChangeCheck();
            defaultClipProp.intValue = EditorGUILayout.Popup("Default Playing", defaultClipProp.intValue, clipsName);
            if (EditorGUI.EndChangeCheck())
            {
                if (_meshRenderer != null)
                {
                    var clipData = _player.animData.GetClip(defaultClipProp.intValue);
                    _meshRenderer.sharedMaterial.SetFloat(GPUSkinAnimator.shaderPropID_ClipTime, 0);
                    _meshRenderer.sharedMaterial.SetFloat(GPUSkinAnimator.shaderPropID_ClipStart, clipData.startV);
                    _meshRenderer.sharedMaterial.SetFloat(GPUSkinAnimator.shaderPorpID_ClipEnd, clipData.endV);
                }
                if (Application.isPlaying)
                {
                    _player.Play(defaultClipProp.intValue);
                }
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.PrefixLabel("All Clips:");
            if (_player != null)
            {
                for (var i = 0; i < _player.animData.clips.Length; i++)
                {
                    var clip = _player.animData.clips[i];
                    if (GUILayout.Button(clip.name))
                    {
                        _player.Play(i);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnEnable()
    {
        _player = target as GPUSkinAnimator;
        _meshRenderer = _player.GetComponent<MeshRenderer>();
        animDataProp = serializedObject.FindProperty("animData");
        defaultClipProp = serializedObject.FindProperty("defaultClipIndex");

        RebuildClipNames(animDataProp.objectReferenceValue as GPUSkinAnimData);
    }

    public static void ResetAnimData(GPUSkinAnimator player, GPUSkinAnimData animData)
    {
        if (animData != null)
        {
            var meshFilter = player.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = animData.mesh;
            }

            var meshRenderer = player.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = animData.material;
            }
        }
    }

    public void RebuildClipNames(GPUSkinAnimData animData)
    {
        if (animData != null && animData.clips != null)
        {
            var clips = new string[animData.clips.Length];
            for (int i = 0; i < animData.clips.Length; ++i)
            {
                clips[i] = animData.clips[i].name;
            }
            this.clipsName = clips;

            defaultClipProp.intValue = Mathf.Clamp(defaultClipProp.intValue, 0, animData.clips.Length);
        }
        else
        {
            this.clipsName = null;
            defaultClipProp.intValue = 0;
        }
    }

    private void OnDisable()
    {

    }
}
