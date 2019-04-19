using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using UnityEditor;
using System.IO;
using UnityEditor.Animations;

public class GPUSkinBaker : EditorWindow
{
    public const string RawPrefabsPath = "Assets/GameRes/Actors/RawPrefabs";
    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
    }

    public ComputeShader infoTexGen;
    public Shader playShader;
    public GameObject prefab;
    public AnimationClip[] animationClips;
    public float[] clipFps;
    public string outputFolder;

    [MenuItem("Window/AnimationBaker")]
    static void Init()
    {
        GetWindow(typeof(GPUSkinBaker));
    }

    public void OnGUI()
    {
        infoTexGen = (ComputeShader)EditorGUILayout.ObjectField(infoTexGen, typeof(ComputeShader), false);
        playShader = (Shader)EditorGUILayout.ObjectField(playShader, typeof(Shader), false);

        EditorGUI.BeginChangeCheck();
        prefab = (GameObject)EditorGUILayout.ObjectField(prefab, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
            Setup(prefab);

        EditorGUILayout.Space();
        if (prefab != null)
        {
            for (var i = 0; i < animationClips.Length; i++)
            {
                var animationClip = animationClips[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(i + "." + animationClip.name + " defaultFps:" + animationClip.frameRate);
                clipFps[i] = EditorGUILayout.FloatField(clipFps[i]);
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.LabelField(outputFolder);

        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Setup", GUILayout.Height(50f)))
        {
            Setup(Selection.activeGameObject);
        }

        EditorGUI.BeginDisabledGroup(prefab == null);
        if (GUILayout.Button("Bake", GUILayout.Height(50f)))
        {
            Bake(outputFolder, prefab, infoTexGen, playShader, animationClips, clipFps);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    public void Setup(GameObject go)
    {
        this.prefab = go;
        if (go != null)
        {
            var animator = go.GetComponent<Animator>();
            var clipSet = new HashSet<AnimationClip>(animator.runtimeAnimatorController.animationClips);
            animationClips = clipSet.ToArray();
            clipFps = new float[animationClips.Length];
            for (var i = 0; i < animationClips.Length; i++)
            {
                clipFps[i] = animationClips[i].frameRate;
            }

            string animatorPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            string roleResPath = Path.GetDirectoryName(animatorPath);
            outputFolder = Path.Combine(roleResPath, "SkinBaked");
        }
    }

    public static int GetAnimationClipFrames(AnimationClip clip, float fps)
    {
        int frame = (int)(clip.length * fps);
        //var bindings = AnimationUtility.GetCurveBindings(clip);
        //AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, bindings[0]);
        //frame = Mathf.Max(frame, curve.keys.Length - 1);
        return frame;
    }


    public static Texture2D ConvertRT(RenderTexture rt)
    {
        TextureFormat format;

        switch (rt.format)
        {
            case RenderTextureFormat.ARGBFloat:
                format = TextureFormat.RGBAFloat;
                break;
            case RenderTextureFormat.ARGBHalf:
                format = TextureFormat.RGBAHalf;
                break;
            case RenderTextureFormat.ARGBInt:
                format = TextureFormat.RGBA32;
                break;
            case RenderTextureFormat.ARGB32:
                format = TextureFormat.ARGB32;
                break;
            default:
                format = TextureFormat.ARGB32;
                Debug.LogWarning("Unsuported RenderTextureFormat.");
                break;
        }

        return ConvertRT(rt, format);
    }

    static Texture2D ConvertRT(RenderTexture rt, TextureFormat format)
    {
        var tex2d = new Texture2D(rt.width, rt.height, format, false);
        var rect = Rect.MinMaxRect(0f, 0f, tex2d.width, tex2d.height);
        RenderTexture.active = rt;
        tex2d.ReadPixels(rect, 0, 0);
        RenderTexture.active = null;
        return tex2d;
    }

    /// <summary>
    /// 拷贝原AnimatorController，添加GPUSkinAnimState用于驱动GPUSkinAnimator播放动画
    /// </summary>
    public static AnimatorController GenGPUSkinBaseAnimator(string outputFolder, Animator animator)
    {
        if (!AssetDatabase.IsValidFolder(outputFolder))
            Directory.CreateDirectory(outputFolder);

        var originAnimatorController = animator.runtimeAnimatorController;
        string defaultAnimatorPath = AssetDatabase.GetAssetPath(originAnimatorController);
        var newAnimatorPath = Path.Combine(outputFolder, originAnimatorController.name + "_gskin.controller");
        var newAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(newAnimatorPath);
        if (newAnimatorController == null)
        {
            AssetDatabase.CopyAsset(defaultAnimatorPath, newAnimatorPath);
            newAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(newAnimatorPath);
            var allStates = newAnimatorController.layers[0].stateMachine.states;
            foreach (var childState in allStates)
            {
                var animState = childState.state;
                var gpuSkinAnimState = newAnimatorController.AddEffectiveStateMachineBehaviour<GPUSkinAnimState>(animState, 0);
                if (animState.motion != null)
                {
                    gpuSkinAnimState.clipName = animState.motion.name;
                }
            }
        }

        return newAnimatorController;
    }

    //将锚点配件烘培动画Clip设置到overrideController中
    public static RuntimeAnimatorController GenGPUSkinOverrideController(string output, AnimatorController baseController, AnimationClip[] overrideClips)
    {
        if (overrideClips != null)
        {
            var overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, output);
            foreach (var animationClip in overrideClips)
            {
                AssetDatabase.AddObjectToAsset(animationClip, overrideController);
                overrideController[animationClip.name] = animationClip;
            }
            return overrideController;
        }

        return baseController;
    }

    public static List<Transform> FindAttachments(Transform root)
    {
        var ret = new List<Transform>();
        var allChilds = root.GetComponentsInChildren<Transform>(false);
        foreach (var child in allChilds)
        {
            if (child.name.Contains("_fx"))//|| child.CompareTag("body_fx"))
            {
                ret.Add(child);
            }
        }
        return ret;
    }

    public static Keyframe AddConstantKey(float time, float val)
    {
        var key = new Keyframe(time, val);
        return key;
    }

    public static void Bake(string outputFolder, GameObject go, ComputeShader infoTexGen, Shader playerShader, AnimationClip[] allClips, float[] customFPS)
    {
        if (!AssetDatabase.IsValidFolder(outputFolder))
            Directory.CreateDirectory(outputFolder);

        var root = go.transform;
        var animator = go.GetComponent<Animator>();
        //先检查是否生成BaseGPUSkinAnimator
        var baseController = GenGPUSkinBaseAnimator(outputFolder, animator);

        var animData = ScriptableObject.CreateInstance<GPUSkinAnimData>();
        animData.clips = new GPUSkinClipData[allClips.Length];
        var skinRender = go.GetComponentInChildren<SkinnedMeshRenderer>();
        //所有资源名以蒙皮网格为前缀
        string meshName = skinRender.sharedMesh.name;

        var vCount = skinRender.sharedMesh.vertexCount;
        var atlasWidth = vCount;//Mathf.NextPowerOfTwo(vCount);
        var mesh = new Mesh();

        var atlasHeight = 0;
        var posTexList = new List<Texture2D>();
        var normalTexList = new List<Texture2D>();
        var attachments = FindAttachments(go.transform);
        AnimationClip[] overrideClips = null;
        if (attachments.Count > 0)
        {
            overrideClips = new AnimationClip[allClips.Length];
        }

        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        for (var index = 0; index < allClips.Length; index++)
        {
            AnimationClip clip = allClips[index];
            float fps = customFPS[index];
            //初始化配件Clip相关数据
            AnimationClip attachmentClip = null;
            List<AnimationCurve[]> attachmentCurves = null;
            if (attachments.Count > 0)
            {
                attachmentClip = new AnimationClip();
                attachmentClip.frameRate = fps;
                attachmentClip.name = clip.name;
                var originClipSetting = AnimationUtility.GetAnimationClipSettings(clip);
                var newClipSetting = AnimationUtility.GetAnimationClipSettings(attachmentClip);
                newClipSetting.loopTime = originClipSetting.loopTime;
                AnimationUtility.SetAnimationClipSettings(attachmentClip, newClipSetting);

                //TRS一共9条AnimationCurve
                attachmentCurves = new List<AnimationCurve[]>(attachments.Count);
                for (var i = 0; i < attachments.Count; i++)
                {
                    var curves = new AnimationCurve[9];
                    for (int j = 0; j < 9; j++)
                    {
                        curves[j] = new AnimationCurve();
                    }
                    attachmentCurves.Add(curves);
                }
            }

            //var frames = Mathf.NextPowerOfTwo((int)(clip.length / fps));
            var frames = GetAnimationClipFrames(clip, fps);
            var texHeight = frames;
            atlasHeight += texHeight;
            var infoList = new List<VertInfo>();

            var pRt = new RenderTexture(atlasWidth, texHeight, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.{1}.posTex", meshName, clip.name);
            var nRt = new RenderTexture(atlasWidth, texHeight, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.{1}.normTex", meshName, clip.name);
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            for (var i = 0; i < texHeight; i++)
            {
                float time = i * (1 / fps);
                AnimationMode.SampleAnimationClip(go, clip, time);
                skinRender.BakeMesh(mesh);

                if (attachments.Count > 0)
                {
                    for (var aId = 0; aId < attachments.Count; aId++)
                    {
                        var attachment = attachments[aId];
                        var curves = attachmentCurves[aId];

                        var localPos = root.InverseTransformPoint(attachment.position);
                        curves[0].AddKey(AddConstantKey(time, localPos.x));
                        curves[1].AddKey(AddConstantKey(time, localPos.y));
                        curves[2].AddKey(AddConstantKey(time, localPos.z));
                        var localRot = root.InverseTransformDirection(attachment.eulerAngles);
                        curves[3].AddKey(AddConstantKey(time, localRot.x));
                        curves[4].AddKey(AddConstantKey(time, localRot.y));
                        curves[5].AddKey(AddConstantKey(time, localRot.z));
                        var localScale = root.InverseTransformVector(attachment.lossyScale);
                        curves[6].AddKey(AddConstantKey(time, localScale.x));
                        curves[7].AddKey(AddConstantKey(time, localScale.y));
                        curves[8].AddKey(AddConstantKey(time, localScale.z));
                    }
                }

                infoList.AddRange(Enumerable.Range(0, vCount)
                    .Select(idx => new VertInfo()
                    {
                        position = mesh.vertices[idx],
                        normal = mesh.normals[idx]
                    })
                );
            }

            var buffer = new ComputeBuffer(infoList.Count,
                System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("CSMain");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, texHeight / (int)y + 1, 1);

            buffer.Release();

            var posTex = ConvertRT(pRt);
            var normTex = ConvertRT(nRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);
            posTexList.Add(posTex);
            normalTexList.Add(normTex);

            if (attachments.Count > 0)
            {
                for (var aId = 0; aId < attachments.Count; aId++)
                {
                    var child = attachments[aId];
                    var nodeName = child.parent.name + "_" + child.name;
                    var curves = attachmentCurves[aId];
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localPosition.x", curves[0]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localPosition.y", curves[1]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localPosition.z", curves[2]);

                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localEulerAngles.x", curves[3]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localEulerAngles.y", curves[4]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localEulerAngles.z", curves[5]);

                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localScale.x", curves[6]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localScale.y", curves[7]);
                    attachmentClip.SetCurve(nodeName, typeof(Transform), "localScale.z", curves[8]);
                }

                overrideClips[index] = attachmentClip;
            }
        }

        AnimationMode.EndSampling();
        AnimationMode.StopAnimationMode();

        var padding = 0;
        var extraHeight = padding * (allClips.Length - 1);
        var atlasPosTex = new Texture2D(atlasWidth, atlasHeight + extraHeight, TextureFormat.RGBAHalf, false);
        atlasPosTex.filterMode = FilterMode.Point;
        var atlasNormalTex = new Texture2D(atlasWidth, atlasHeight + extraHeight, TextureFormat.RGBAHalf, false);
        atlasNormalTex.filterMode = FilterMode.Point;
        var srcY = 0;
        for (int i = 0; i < posTexList.Count; i++)
        {
            var clip = allClips[i];
            var clipData = new GPUSkinClipData();
            var posTex = posTexList[i];
            var normalTex = normalTexList[i];

            var frames = posTex.height;
            Graphics.CopyTexture(posTex, 0, 0, 0, 0, posTex.width, posTex.height, atlasPosTex, 0, 0, 0, srcY);
            Graphics.CopyTexture(normalTex, 0, 0, 0, 0, normalTex.width, normalTex.height, atlasNormalTex, 0, 0, 0, srcY);
            //初始帧稍微偏移一点,否则采样到padding上的纹理
            float paddingOffset = (padding > 0 && srcY != 0) ? 0.001f : 0f;

            clipData.name = clip.name;
            clipData.length = clip.length;
            clipData.frames = frames;
            clipData.fps = customFPS[i];
            clipData.startV = (float)srcY / (float)(atlasPosTex.height + paddingOffset - 1);
            clipData.endV = (float)(srcY + frames) / (float)atlasPosTex.height;
            var clipSetting = AnimationUtility.GetAnimationClipSettings(clip);
            clipData.wrapMode = clipSetting.loopTime ? WrapMode.Loop : WrapMode.Default;
            srcY = srcY + frames + padding;
            animData.clips[i] = clipData;
        }

        BakeMeshIndex(skinRender.sharedMesh, atlasWidth);

        var mat = new Material(skinRender.sharedMaterial);
        mat.shader = playerShader;
        mat.SetTexture("_PosTex", atlasPosTex);
        mat.SetTexture("_BakeNmlTex", atlasNormalTex);
        mat.enableInstancing = true;

        animData.material = mat;
        animData.mesh = skinRender.sharedMesh;

        AssetDatabase.CreateAsset(atlasPosTex, Path.Combine(outputFolder, meshName + "_pos.asset"));
        AssetDatabase.CreateAsset(atlasNormalTex, Path.Combine(outputFolder, meshName + "_n.asset"));
        AssetDatabase.CreateAsset(animData, Path.Combine(outputFolder, meshName + "_animData.asset"));

        var newAnimatorController = GenGPUSkinOverrideController(Path.Combine(outputFolder, go.name + "_gskin.overrideController"), baseController, overrideClips);

        var tempGo = new GameObject(go.name + "_gskin");
        tempGo.AddComponent<Animator>().runtimeAnimatorController = newAnimatorController;
        tempGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
        tempGo.AddComponent<MeshFilter>().sharedMesh = skinRender.sharedMesh;
        var gskinAnimator = tempGo.AddComponent<GPUSkinAnimator>();
        gskinAnimator.animData = animData;
        if (attachments.Count > 0)
        {
            foreach (var child in attachments)
            {
                var copyChild = Object.Instantiate(child, tempGo.transform, true);
                copyChild.name = child.parent.name + "_" + child.name;
            }
        }

        AssetDatabase.CreateAsset(mat, Path.Combine(outputFolder, string.Format("{0}_gskin.mat", meshName)));
        PrefabUtility.CreatePrefab(Path.Combine(RawPrefabsPath, tempGo.name + ".prefab").Replace("\\", "/"), tempGo, ReplacePrefabOptions.ConnectToPrefab);
        Object.DestroyImmediate(tempGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Bake GPUSkin Finish:" + animData.name, animData);
    }

    private static void BakeMeshIndex(Mesh mesh, float texWidth)
    {
        int numVertices = mesh.vertexCount;
        var posUV = new Vector2[numVertices];
        for (int i = 0; i < numVertices; ++i)
        {
            float u = (i + 0.5f) * (1 / texWidth);
            posUV[i] = new Vector2(u, 0);
        }
        mesh.uv2 = posUV;
        EditorUtility.SetDirty(mesh);
    }

}
