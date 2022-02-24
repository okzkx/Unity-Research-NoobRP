using UnityEngine.Rendering;

public class RenderStep {
    public static readonly ShaderTagId defaultPassName = new ShaderTagId("Both"); //The shader pass tag just for SRP0703

    public void ExcuteAndClearCommandBuffer(ScriptableRenderContext context, CommandBuffer cmb) {
        context.ExecuteCommandBuffer(cmb);
        cmb.Clear();
    }
}