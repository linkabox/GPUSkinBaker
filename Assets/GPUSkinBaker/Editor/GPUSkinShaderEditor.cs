using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GPUSkinShaderEditor : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        base.OnGUI(materialEditor, props);

        if (Application.isPlaying)
        {
            materialEditor.Repaint();
        }
    }
}
