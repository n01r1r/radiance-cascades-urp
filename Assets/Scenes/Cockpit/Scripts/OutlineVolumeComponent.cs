using UnityEngine.Rendering;

[VolumeComponentMenu("Sample Scene/Outline")]
public class OutlineVolumeComponent : VolumeComponent
{
    public BoolParameter Enabled = new BoolParameter(false);
}